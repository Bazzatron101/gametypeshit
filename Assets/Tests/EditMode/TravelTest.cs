using System.Collections.Generic;
using System.Linq;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Travel by species (ticket 90). Mounting up suits the species: cockatoos and sugar gliders fly, kangaroos and
    /// Tasmanian devils run, the rest ride a bicycle. Every style goes 16 m/s. Runners and riders keep the ground
    /// rules of ticket 42; flyers keep a height above the ground and cross cliffs and water, but not the map's edge.
    /// A flyer skims low at first; each rank lets them climb higher and dive within it.
    /// </summary>
    public class TravelTest
    {
        private static readonly PlayerId P = new PlayerId(1);
        private static readonly Vec3 Start = new Vec3(100f, 0f, 0f);

        private static readonly PlayerCommand Toggle = new PlayerCommand(0f, 0f, toggleMount: true);
        private static readonly PlayerCommand East = new PlayerCommand(1f, 0f);
        private static PlayerCommand Climb(int way) => new PlayerCommand(0f, 0f, climb: way);

        /// <summary>20 × 1 cells of 10 m from (0, -5): the map runs from x 0 to 200. Cell 15 (x 150 to 160) is <paramref name="obstacle"/>.</summary>
        private static HeightmapGround Ground(GroundKind obstacle) =>
            new HeightmapGround(0f, -5f, 10f, 20, 1, new float[42], Enumerable.Range(0, 20).Select(i => i == 15 ? obstacle : GroundKind.Walkable).ToArray());

        private static SimulationConfig Config(IGround ground = null) => new SimulationConfig
        {
            TickRate = 60, PlayerMoveSpeed = 5f, MountSpeed = 16f, FlightSkimHeight = 3f, FlightClimbSpeed = 6f,
            Hub = new HubDefinition { Centre = new Vec3(-500f, 0f, 0f) },
            Overworld = new OverworldDefinition { TownRadius = 40f },
            Ground = ground,
            Gates = new[] { new GateDefinition { Id = "den", Name = "Den", Rooms = { new RoomDefinition() } } },
        };

        private static CharacterRecord Hunter(string species, int level = 16) =>
            new CharacterRecord { Name = "Tester", SpeciesId = species, Level = level, HasMount = true };

        private static SimulationWorld World(string species, int level = 16, IGround ground = null)
        {
            var world = new SimulationWorld(Config(ground));
            world.AddPlayer(P, Start, Hunter(species, level));
            Step(world, PlayerCommand.None);
            return world;
        }

        private static void Step(SimulationWorld world, PlayerCommand command, int ticks = 1)
        {
            for (var i = 0; i < ticks; i++) world.Step(new Dictionary<PlayerId, PlayerCommand> { [P] = command });
        }

        private static PlayerState Me(SimulationWorld world) => world.GetPlayer(P);

        [Test]
        public void EachSpeciesTravelsItsOwnWay()
        {
            var expected = new Dictionary<string, TravelStyle>
            {
                ["cockatoo"] = TravelStyle.Fly, ["sugar-glider"] = TravelStyle.Fly,
                ["kangaroo"] = TravelStyle.Run, ["tasmanian-devil"] = TravelStyle.Run,
                ["wombat"] = TravelStyle.Bicycle, ["koala"] = TravelStyle.Bicycle, ["platypus"] = TravelStyle.Bicycle, ["quokka"] = TravelStyle.Bicycle,
            };
            foreach (var pair in expected)
            {
                var world = World(pair.Key);
                Assert.That(Me(world).Travel, Is.EqualTo(pair.Value), pair.Key);
                Step(world, Toggle);
                Assert.That(Me(world).IsMounted, Is.True, pair.Key);
                Assert.That(Me(world).IsFlying, Is.EqualTo(pair.Value == TravelStyle.Fly), pair.Key);
            }
            Assert.That(World(null).GetPlayer(P).Travel, Is.EqualTo(TravelStyle.Run), "a hunter with no species runs on a mount, as before");
        }

        [Test]
        public void EveryStyleGoesSixteenMetresASecond()
        {
            foreach (var species in new[] { "cockatoo", "kangaroo", "wombat" })
            {
                var world = World(species);
                Step(world, Toggle);
                Step(world, East, 60);
                Assert.That(Me(world).Position.X - Start.X, Is.EqualTo(16f).Within(0.05f), species);
            }
        }

        [Test]
        public void RunnersAndRidersStopAtACliffWhereAFlyerCrossesIt()
        {
            var cliff = Ground(GroundKind.Blocked);
            foreach (var species in new[] { "kangaroo", "wombat" })
            {
                var world = World(species, ground: cliff);
                Step(world, Toggle);
                Step(world, East, 60 * 5);
                Assert.That(Me(world).Position.X, Is.LessThan(150f), species + " stops at the cliff");
            }
            var flyer = World("cockatoo", ground: cliff);
            Step(flyer, Toggle);
            Step(flyer, East, 60 * 5);
            Assert.That(Me(flyer).Position.X, Is.GreaterThan(160f), "a flyer crosses it");

            var lake = World("cockatoo", ground: Ground(GroundKind.Water));
            Step(lake, Toggle);
            Step(lake, East, 60 * 5);
            Assert.That(Me(lake).Position.X, Is.GreaterThan(160f), "and water");
        }

        [Test]
        public void NobodyFliesOffTheMap()
        {
            var world = World("cockatoo", ground: Ground(GroundKind.Walkable));
            Step(world, Toggle);
            Step(world, East, 60 * 10);
            Assert.That(Me(world).Position.X, Is.LessThanOrEqualTo(200f), "the map ends at x 200");
        }

        [Test]
        public void AFlyerSkimsLowAtFirstAndClimbsHigherWithEachRank()
        {
            var d = World("cockatoo", level: 16);
            Step(d, Toggle);
            Assert.That(Me(d).FlightHeight, Is.EqualTo(3f), "takes off to the skimming height");
            Assert.That(Me(d).Position.Y, Is.EqualTo(3f).Within(0.01f), "and is drawn there");
            Step(d, Climb(1), 60 * 5);
            Assert.That(Me(d).FlightHeight, Is.EqualTo(3f), "at D-rank it only skims");
            Assert.That(d.FlightCeiling(P), Is.EqualTo(3f));

            var c = World("cockatoo", level: 36);
            Step(c, Toggle);
            Step(c, Climb(1), 60);
            Assert.That(Me(c).FlightHeight, Is.EqualTo(9f).Within(0.01f), "climbs at 6 m/s");
            Step(c, Climb(1), 60 * 5);
            Assert.That(Me(c).FlightHeight, Is.EqualTo(10f), "up to 10 m at C-rank");
            Step(c, Climb(-1), 60 * 5);
            Assert.That(Me(c).FlightHeight, Is.EqualTo(3f), "a dive stops at the skimming height");

            var a = World("cockatoo", level: 76);
            Step(a, Toggle);
            Step(a, Climb(1), 60 * 10);
            Assert.That(Me(a).FlightHeight, Is.EqualTo(35f), "35 m at A-rank");
        }

        [Test]
        public void AFlyersJumpKeyClimbsInsteadOfJumping()
        {
            var world = World("cockatoo", level: 36);
            Step(world, Toggle);
            Step(world, new PlayerCommand(0f, 0f, jump: true, climb: 1));
            Assert.That(Me(world).IsAirborne, Is.False, "no hop in the air");
        }

        [Test]
        public void AHitDropsAFlyerToTheGround()
        {
            var world = World("cockatoo", level: 36);
            Step(world, Toggle);
            Step(world, Climb(1), 60);
            world.SpawnEnemy(new EnemyDefinition
            {
                Name = "Biter", MaxHealth = 100, MoveSpeed = 0f, AttackDamage = 5, AttackRange = 2f, AttackWindupSeconds = 0.1f, AttackCooldownSeconds = 10f, XpReward = 0,
            }, Start + new Vec3(0f, 0f, 1f));
            Step(world, PlayerCommand.None, 30);
            Assert.That(Me(world).IsMounted, Is.False, "knocked out of the air");
            Assert.That(Me(world).FlightHeight, Is.Zero);
            Assert.That(Me(world).Position.Y, Is.EqualTo(0f).Within(0.01f), "on the ground");
        }

        [Test]
        public void AFlyerLandsOnlyOnGroundTheyCanStandOn()
        {
            var world = World("cockatoo", ground: Ground(GroundKind.Water));
            Step(world, Toggle);
            world.PlacePlayer(P, new Vec3(155f, 0f, 0f));
            Step(world, PlayerCommand.None);
            Assert.That(world.CanLand(P), Is.False);
            Step(world, Toggle);
            Assert.That(Me(world).IsMounted, Is.True, "R over the lake does nothing");
            world.PlacePlayer(P, new Vec3(120f, 0f, 0f));
            Step(world, PlayerCommand.None);
            Step(world, Toggle);
            Assert.That(Me(world).IsMounted, Is.False, "over the shore they land");
            Assert.That(Me(world).Position.Y, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void AGateStillPutsAFlyerOnFoot()
        {
            var world = World("cockatoo");
            Step(world, Toggle);
            world.StartGate("den");
            Step(world, PlayerCommand.None, 2);
            Assert.That(Me(world).IsMounted, Is.False);
            Assert.That(Me(world).FlightHeight, Is.Zero);
        }

        [Test]
        public void TheStyleComesFromTheSpeciesSoItSurvivesASave()
        {
            var world = World("sugar-glider");
            var saved = CharacterCodec.Decode(CharacterCodec.Encode(world.CaptureCharacter(P)));
            var again = new SimulationWorld(Config());
            again.AddPlayer(P, Start, saved);
            Assert.That(again.GetPlayer(P).Travel, Is.EqualTo(TravelStyle.Fly));
        }

        [Test]
        public void AGuestsFlightIsPredictedAndAgreesWithTheHost()
        {
            const int latency = 4;
            var network = new SimulatedNetwork(latency);
            var host = new HostSession(Config(), network.HostEndpoint, _ => Start, 3);
            host.AddLocalPlayer();
            var guest = new ClientSession(Config(), network.Connect(), "flyer", Hunter("cockatoo", 36));

            void Tick(PlayerCommand command, int times = 1)
            {
                for (var i = 0; i < times; i++)
                {
                    network.Advance();
                    host.Tick(PlayerCommand.None);
                    guest.Tick(command);
                }
            }

            Tick(PlayerCommand.None, latency * 3);
            var me = guest.PlayerId.Value;
            Tick(Toggle);
            Assert.That(guest.World.GetPlayer(me).IsFlying, Is.True, "predicted at once");
            Tick(new PlayerCommand(1f, 0f, climb: 1), 60);
            Tick(PlayerCommand.None, latency * 3);

            var onHost = host.World.GetPlayer(me);
            Assert.That(onHost.IsFlying, Is.True);
            Assert.That(onHost.FlightHeight, Is.EqualTo(9f).Within(0.05f), "climbed for a second on the host");
            Assert.That(guest.World.GetPlayer(me).FlightHeight, Is.EqualTo(onHost.FlightHeight).Within(1e-3f));
            Assert.That(guest.World.GetPlayer(me).Position.X, Is.EqualTo(onHost.Position.X).Within(1e-3f));
            Assert.That(guest.LargestCorrection, Is.LessThan(1e-3f), "never pulled back");
        }
    }
}
