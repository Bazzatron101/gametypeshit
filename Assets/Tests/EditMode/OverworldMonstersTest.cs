using System.Collections.Generic;
using System.Linq;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The overworld's monsters and moving across real ground (ticket 43). Each gate leaks a few of its own
    /// kinds round its entrance while its tile is loaded; a cleared gate stops; a kill is replaced after a
    /// while; a leaked monster only chases near its gate. Monsters and bot teammates find their way round what
    /// they cannot cross; teammates follow their leader through passes and ride when the leader rides.
    /// </summary>
    public class OverworldMonstersTest
    {
        private static readonly PlayerId P = new PlayerId(1);
        private static readonly Vec3 DenEntrance = new Vec3(200f, 0f, 0f);

        private static EnemyDefinition Goblin => new EnemyDefinition
        {
            Name = "Goblin", MaxHealth = 10, MoveSpeed = 4f, AttackDamage = 0, AttackRange = 1f, StopDistance = 1f, XpReward = 5,
        };

        private static EnemyDefinition Hobgoblin => new EnemyDefinition
        {
            Name = "Hobgoblin", MaxHealth = 20, MoveSpeed = 3f, AttackDamage = 0, AttackRange = 1f, StopDistance = 1f, XpReward = 10,
        };

        private static EnemyDefinition Warlord => new EnemyDefinition
        {
            Name = "Warlord", MaxHealth = 100, Boss = new BossDefinition(),
        };

        private static readonly EnemyDefinition GoblinKind = Goblin;
        private static readonly EnemyDefinition HobgoblinKind = Hobgoblin;

        private static GateDefinition Den() => new GateDefinition
        {
            Id = "den", Name = "Den",
            Rooms =
            {
                new RoomDefinition { Spawns = { new RoomSpawn(GoblinKind, Vec3.Zero), new RoomSpawn(HobgoblinKind, Vec3.Zero) } },
                new RoomDefinition { Spawns = { new RoomSpawn(Warlord, Vec3.Zero) } },
            },
        };

        private static SimulationConfig Config(IGround ground = null)
        {
            var overworld = new OverworldDefinition { LeakCount = 3, LeakRadius = 10f, LeakLeash = 30f, LeakRespawnSeconds = 5f };
            overworld.StartingRegions.Add("green");
            overworld.Gates.Add(new GateSite { GateId = "den", Entrance = DenEntrance, Region = "green", KnownFromStart = true });
            return new SimulationConfig
            {
                TickRate = 60, PlayerMoveSpeed = 5f, MountSpeed = 16f,
                Hub = new HubDefinition { Centre = Vec3.Zero }, Overworld = overworld, Ground = ground, Gates = new[] { Den() },
            };
        }

        private static SimulationWorld World(Vec3 at, IGround ground = null)
        {
            var world = new SimulationWorld(Config(ground));
            world.AddPlayer(P, at);
            return world;
        }

        private static void Step(SimulationWorld world, PlayerCommand command = default, int ticks = 1)
        {
            for (var i = 0; i < ticks; i++) world.Step(new Dictionary<PlayerId, PlayerCommand> { [P] = command });
        }

        private static List<EnemyState> Leaks(SimulationWorld world) => world.EnemyIds.Select(world.GetEnemy).Where(e => !e.IsDefeated).ToList();

        // --- Leaks ------------------------------------------------------------------------------------

        [Test]
        public void AGateLeaksItsOwnKindsRoundItsEntrance()
        {
            var world = World(new Vec3(100f, 0f, 0f));

            Step(world);

            var leaks = Leaks(world);
            Assert.That(leaks, Has.Count.EqualTo(3));
            Assert.That(leaks.Select(e => e.Definition.Name).Distinct(), Is.EquivalentTo(new[] { "Goblin", "Hobgoblin" }), "its own kinds");
            Assert.That(leaks.All(e => e.Definition.Boss == null), Is.True, "never its boss");
            foreach (var e in leaks)
                Assert.That((e.Position - DenEntrance).Length, Is.LessThan(11f), "round its entrance");
        }

        [Test]
        public void AClearedGateStopsLeaking()
        {
            var world = World(new Vec3(100f, 0f, 0f));
            world.SetClearedGates(new[] { "den" });

            Step(world, ticks: 60);

            Assert.That(Leaks(world), Is.Empty);
        }

        [Test]
        public void AKilledMonsterIsReplacedAfterAWhile()
        {
            var world = World(new Vec3(100f, 0f, 0f));
            Step(world);
            world.DamageEnemy(world.EnemyIds.First(), P, 1000);
            Step(world);
            Assert.That(Leaks(world), Has.Count.EqualTo(2), "one down");

            Step(world, ticks: 60 * 3);
            Assert.That(Leaks(world), Has.Count.EqualTo(2), "not straight away");
            Step(world, ticks: 60 * 3);
            Assert.That(Leaks(world), Has.Count.EqualTo(3), "replaced after the delay");
        }

        [Test]
        public void MonstersExistOnlyWhileTheirGatesTileIsLoaded()
        {
            var map = new TiledGround(-250f, -125f, 250f, 2, 1); // the player's tile west of 0, the den's east of it
            var tile = map.TileAt(DenEntrance);
            map.SetTile(new TileCoord(0, 0), new HeightmapGround(-250f, -125f, 250f, 1, 1, new float[4]));
            var world = World(new Vec3(-100f, 0f, 0f), map);

            Step(world, ticks: 10);
            Assert.That(world.EnemyIds, Is.Empty, "its tile is not loaded");

            map.SetTile(tile, new HeightmapGround(0f, -125f, 250f, 1, 1, new float[4]));
            Step(world);
            Assert.That(Leaks(world), Has.Count.EqualTo(3), "loaded: they are out");

            map.RemoveTile(tile);
            Step(world);
            Assert.That(world.EnemyIds, Is.Empty, "unloaded: gone");
        }

        [Test]
        public void AGuestsWorldNeverMakesItsOwnMonsters()
        {
            var world = World(new Vec3(100f, 0f, 0f));
            world.RunsLeaks = false;

            Step(world, ticks: 60);

            Assert.That(world.EnemyIds, Is.Empty, "they come from the host's snapshots");
        }

        [Test]
        public void ALeakedMonsterChasesOnlyNearItsGateAndGoesBack()
        {
            var world = World(DenEntrance + new Vec3(-20f, 0f, 0f));
            Step(world);
            var goblin = world.EnemyIds.Select(world.GetEnemy).First(e => e.Definition.Name == "Goblin").Id;
            var start = world.GetEnemy(goblin).Position;

            Step(world, ticks: 60);
            Assert.That((world.GetEnemy(goblin).Position - world.GetPlayer(P).Position).Length,
                Is.LessThan((start - world.GetPlayer(P).Position).Length - 2f), "it comes for a player near its gate");

            world.PlacePlayer(P, DenEntrance + new Vec3(-80f, 0f, 0f)); // well past its leash
            Step(world, ticks: 60 * 10);
            Assert.That((world.GetEnemy(goblin).Position - DenEntrance).Length, Is.LessThan(1.5f), "and goes home when they leave");
        }

        // --- Finding a way round ----------------------------------------------------------------------

        /// <summary>400 × 200 m of 2 m cells from (0, -100), flat, with a wall at x 208–212 from z -40 to 40.</summary>
        private static HeightmapGround Walled()
        {
            const int columns = 200, rows = 100;
            var kinds = new GroundKind[columns * rows];
            for (var r = 0; r < rows; r++)
            for (var c = 0; c < columns; c++)
            {
                var x = c * 2f + 1f;
                var z = -100f + r * 2f + 1f;
                if (x >= 208f && x <= 212f && z >= -40f && z <= 40f) kinds[r * columns + c] = GroundKind.Blocked;
            }
            return new HeightmapGround(0f, -100f, 2f, columns, rows, new float[(columns + 1) * (rows + 1)], kinds);
        }

        [Test]
        public void AWayIsFoundRoundAWall()
        {
            var ground = Walled();
            var from = new Vec3(190f, 0f, 0f);
            var to = new Vec3(230f, 0f, 0f);

            Assert.That(GroundPath.IsClear(ground, from, to), Is.False);
            var path = GroundPath.Find(ground, from, to);
            Assert.That(path, Is.Not.Null);
            Assert.That(path.Last(), Is.EqualTo(to));
            Assert.That(path.All(p => ground.IsWalkable(p)), Is.True);
            Assert.That(GroundPath.NextWaypoint(ground, from, new Vec3(190f, 0f, 30f)), Is.EqualTo(new Vec3(190f, 0f, 30f)), "a clear line is just the line");
        }

        [Test]
        public void AMonsterWalksRoundAWallToReachAPlayer()
        {
            var world = World(new Vec3(230f, 0f, 0f), Walled());
            world.SetClearedGates(new[] { "den" }); // no leaks: just this one
            var wolf = world.SpawnEnemy(new EnemyDefinition { Name = "Wolf", MaxHealth = 10, MoveSpeed = 6f, AttackRange = 1f, StopDistance = 1f }, new Vec3(190f, 0f, 0f));

            Step(world, ticks: 60 * 30);

            Assert.That((world.GetEnemy(wolf).Position - world.GetPlayer(P).Position).Length, Is.LessThan(2f), "round the wall and up to the player");
        }

        // --- Bot teammates ---------------------------------------------------------------------------

        private static (HostSession host, PlayerId mate) HostWithTeammate(IGround ground, Vec3 start)
        {
            var host = new HostSession(Config(ground), new SimulatedNetwork(0).HostEndpoint, id => start + new Vec3(id.Value * 2f, 0f, 0f), 3);
            host.AddLocalPlayer();
            host.World.SetClearedGates(new[] { "den" }); // no leaks in the way
            var mate = host.AddCompanion(new CompanionRecord(), seed: 1);
            return (host, mate);
        }

        private static float Apart(SimulationWorld world, PlayerId a, PlayerId b) => (world.GetPlayer(a).Position - world.GetPlayer(b).Position).Length;

        [Test]
        public void ATeammateFollowsItsLeaderRoundAWall()
        {
            var (host, mate) = HostWithTeammate(Walled(), new Vec3(190f, 0f, 0f));
            var me = host.LocalPlayerId;

            // The leader walks north round the wall's end and back down the far side.
            void WalkTo(Vec3 goal)
            {
                for (var i = 0; i < 60 * 30; i++)
                {
                    var at = host.World.GetPlayer(me).Position;
                    var offset = new Vec3(goal.X - at.X, 0f, goal.Z - at.Z);
                    if (offset.Length < 1f) break;
                    host.Tick(new PlayerCommand(offset.X / offset.Length, offset.Z / offset.Length));
                }
            }
            WalkTo(new Vec3(190f, 0f, 50f));
            WalkTo(new Vec3(230f, 0f, 50f));
            WalkTo(new Vec3(230f, 0f, 0f));
            for (var i = 0; i < 60 * 10; i++) host.Tick(PlayerCommand.None);

            Assert.That(host.World.GetPlayer(mate).Position.X, Is.GreaterThan(212f), "it came round to the far side");
            Assert.That(Apart(host.World, me, mate), Is.LessThan(8f), "and is beside its leader");
        }

        [Test]
        public void ATeammateRidesWhenItsLeaderRides()
        {
            var (host, mate) = HostWithTeammate(null, new Vec3(100f, 0f, 0f)); // open ground, outside the town
            var me = host.LocalPlayerId;
            host.World.GrantMount(me);
            for (var i = 0; i < 30; i++) host.Tick(PlayerCommand.None);
            Assert.That(host.World.GetPlayer(mate).HasMount, Is.True, "it gets a mount when its leader has one");

            host.Tick(new PlayerCommand(0f, 0f, toggleMount: true));
            for (var i = 0; i < 60 * 20; i++) host.Tick(new PlayerCommand(1f, 0f)); // 320 m at 16 m/s

            Assert.That(host.World.GetPlayer(me).IsMounted, Is.True);
            Assert.That(host.World.GetPlayer(mate).IsMounted, Is.True, "it rides too");
            Assert.That(Apart(host.World, me, mate), Is.LessThan(30f), "and keeps up");
        }
    }
}
