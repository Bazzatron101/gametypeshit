using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The overworld as a place (ticket 38, ADR 0015). The guild town is part of the world: the party
    /// walks out of it and roams apart. Walking near a hidden gate finds it. The host takes the party
    /// in from a gate's entrance with the ready check (everyone goes in, wherever they stand), or from
    /// the board for a found gate in an open region, and after the run they come back out at the
    /// gate's entrance. A gate in a region that is not open gives 25 % XP. A player joining out in the
    /// world appears beside the host.
    /// </summary>
    public class OverworldTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);
        private static readonly PlayerId P2 = new PlayerId(2);

        private static readonly Vec3 Town = new Vec3(-80f, 0f, 0f);
        private static readonly Vec3 MillEntrance = new Vec3(-60f, 0f, 0f);   // known from the start, open region
        private static readonly Vec3 BridgeEntrance = new Vec3(-20f, 0f, 0f); // hidden, open region
        private static readonly Vec3 SteppeEntrance = new Vec3(40f, 0f, 0f);  // hidden, region not open

        private static EnemyDefinition Goblin => new EnemyDefinition
        {
            Name = "Goblin", MaxHealth = 10, MoveSpeed = 0f, AttackDamage = 0, AttackRange = 0f, XpReward = 40,
        };

        private static GateDefinition OneRoomGate(string id) => new GateDefinition
        {
            Id = id, Name = id,
            Rooms = { new RoomDefinition { Width = 10f, Length = 10f, Spawns = { new RoomSpawn(Goblin, new Vec3(0f, 0f, 3f)) } } },
        };

        private static OverworldDefinition Overworld()
        {
            var overworld = new OverworldDefinition { EntranceRadius = 3f, FindRadius = 20f, UnopenedRegionXpPercent = 25 };
            overworld.StartingRegions.Add("green");
            overworld.Gates.Add(new GateSite { GateId = "mill", Entrance = MillEntrance, Region = "green", KnownFromStart = true });
            overworld.Gates.Add(new GateSite { GateId = "bridge", Entrance = BridgeEntrance, Region = "green" });
            overworld.Gates.Add(new GateSite { GateId = "steppe", Entrance = SteppeEntrance, Region = "ash" });
            return overworld;
        }

        private static SimulationConfig Config() => new SimulationConfig
        {
            TickRate = 60, PlayerMoveSpeed = 5f,
            Gates = new[] { OneRoomGate("mill"), OneRoomGate("bridge"), OneRoomGate("steppe"), OneRoomGate("offmap") },
            Hub = new HubDefinition { Centre = Town, Width = 20f, Length = 16f, BoardOffset = new Vec3(0f, 0f, 6f), ReturnCountdownSeconds = 2f },
            Overworld = Overworld(),
        };

        private static SimulationWorld World(int players = 1)
        {
            var world = new SimulationWorld(Config());
            for (var i = 1; i <= players; i++) world.AddPlayer(new PlayerId(i), world.HubJoinPosition(new PlayerId(i)));
            return world;
        }

        private static List<SimulationEvent> Step(SimulationWorld world, int ticks = 1, Dictionary<PlayerId, PlayerCommand> commands = null)
        {
            var all = new List<SimulationEvent>();
            for (var i = 0; i < ticks; i++)
            {
                world.Step(commands ?? new Dictionary<PlayerId, PlayerCommand>());
                all.AddRange(world.Events);
            }
            return all;
        }

        private static void AllReady(SimulationWorld world)
        {
            foreach (var id in world.PlayerIds)
                Step(world, 1, new Dictionary<PlayerId, PlayerCommand> { [id] = new PlayerCommand(0f, 0f, setReady: true) });
        }

        private static float FlatDistance(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, 0f, a.Z - b.Z).Length;

        // --- Leaving town ---------------------------------------------------------------------------

        [Test]
        public void TheTownIsPartOfTheWorldSoThePartyWalksOutOfIt()
        {
            var world = World(players: 2);

            Assert.That(world.InOverworld, Is.True);
            Assert.That(world.InHub, Is.True, "the town's board and ready check work out here");
            Step(world, 600, new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(1f, 0f), [P2] = new PlayerCommand(0f, -1f) });

            Assert.That(world.GetPlayer(P1).Position.X, Is.GreaterThan(Town.X + 40f), "no town wall: out east into the world");
            Assert.That(world.GetPlayer(P2).Position.Z, Is.LessThan(Town.Z - 40f), "and the party roams apart");
        }

        [Test]
        public void AnOverworldNeedsATown()
        {
            var config = Config();
            config.Hub = null;

            Assert.Throws<ArgumentException>(() => new SimulationWorld(config));
        }

        // --- Finding gates --------------------------------------------------------------------------

        [Test]
        public void WalkingNearAHiddenGateFindsIt()
        {
            var world = World();
            world.PlacePlayer(P1, BridgeEntrance + new Vec3(-25f, 0f, 0f));
            Assert.That(Step(world).OfType<GateFound>(), Is.Empty, "25 m away: not yet");
            Assert.That(world.IsGateFound("bridge"), Is.False);

            var events = Step(world, 61, new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(1f, 0f) });

            var found = events.OfType<GateFound>().Single();
            Assert.That(found.GateId, Is.EqualTo("bridge"));
            Assert.That(found.Player, Is.EqualTo(P1));
            Assert.That(world.IsGateFound("bridge"), Is.True);
            Assert.That(world.FoundGates, Is.EqualTo(new[] { "bridge" }));
            Assert.That(Step(world, 60).OfType<GateFound>(), Is.Empty, "found once");
        }

        [Test]
        public void SomeGatesAreKnownFromTheStartAndGatesOffTheMapAreAlwaysKnown()
        {
            var world = World();

            Assert.That(world.IsGateFound("mill"), Is.True);
            Assert.That(world.FoundGates, Is.Empty, "known gates are not 'found' ones");
            Assert.That(world.IsGateFound("offmap"), Is.True);
            Assert.That(world.IsGateFound("steppe"), Is.False);
        }

        // --- The board ------------------------------------------------------------------------------

        [Test]
        public void TheBoardOnlyOffersFoundGatesInOpenRegions()
        {
            var world = World();
            AllReady(world);

            Assert.That(world.IsOnBoard("mill"), Is.True);
            Assert.That(world.IsOnBoard("bridge"), Is.False, "hidden");
            Assert.That(world.TryStartGateFromHub("bridge", out var reason), Is.False);
            Assert.That(reason, Does.Contain("found"));

            world.SetFoundGates(new[] { "bridge", "steppe" });
            Assert.That(world.IsOnBoard("bridge"), Is.True);
            Assert.That(world.IsOnBoard("steppe"), Is.False, "found, but its region is not open");
            Assert.That(world.TryStartGateFromHub("steppe", out reason), Is.False);
            Assert.That(reason, Does.Contain("region"));

            world.SetOpenRegions(new[] { "ash" });
            Assert.That(world.IsOnBoard("steppe"), Is.True);
            Assert.That(world.TryStartGateFromHub("steppe", out _), Is.True);
            Assert.That(world.Gate.Definition.Id, Is.EqualTo("steppe"));
        }

        [Test]
        public void AGateStartedFromTheBoardComesOutAtItsEntrance()
        {
            var world = World();
            AllReady(world);
            Assert.That(world.TryStartGateFromHub("mill", out _), Is.True);

            world.LeaveGate();
            var events = Step(world);

            Assert.That(events.OfType<ReturnedToHub>().Single().GateId, Is.EqualTo("mill"));
            Assert.That(world.InOverworld, Is.True);
            Assert.That(FlatDistance(world.GetPlayer(P1).Position, MillEntrance), Is.LessThan(2f), "the board saved the walk there, not the walk back");
        }

        // --- Entering at an entrance ----------------------------------------------------------------

        [Test]
        public void TheHostTakesTheWholePartyInFromAGatesEntranceOnceEveryoneIsReady()
        {
            var world = World(players: 2);
            world.PlacePlayer(P1, BridgeEntrance + new Vec3(1f, 0f, 0f)); // the host at the gate; P2 is still in town
            Step(world);

            Assert.That(world.GateEntranceNear(P1).GateId, Is.EqualTo("bridge"));
            Assert.That(world.GateEntranceNear(P2), Is.Null);
            Assert.That(world.TryStartGateAtEntrance(P2, out var reason), Is.False, "nobody can start it from town");
            Assert.That(world.TryStartGateAtEntrance(P1, out reason), Is.False, "not everyone is ready");
            Assert.That(reason, Does.Contain("not ready"));

            AllReady(world);
            Assert.That(world.TryStartGateAtEntrance(P1, out reason), Is.True, reason);

            Assert.That(world.Gate.Definition.Id, Is.EqualTo("bridge"));
            Assert.That(world.InOverworld, Is.False);
            foreach (var id in world.PlayerIds)
                Assert.That(FlatDistance(world.GetPlayer(id).Position, world.Gate.Layout.Entrance), Is.LessThan(2f), "everyone goes in, wherever they stood");
        }

        [Test]
        public void LeaveTownFindAGateEnterClearAndComeBackOut()
        {
            var world = World(players: 2);
            var walkEast = new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(1f, 0f) };

            // Out of town and along to the bridge, finding it on the way.
            var events = Step(world, 60 * 12, walkEast);
            Assert.That(events.OfType<GateFound>().Select(f => f.GateId), Is.EqualTo(new[] { "bridge" }));
            Assert.That(world.GateEntranceNear(P1)?.GateId, Is.EqualTo("bridge"), "standing at it after ~60 m");

            AllReady(world);
            Assert.That(world.TryStartGateAtEntrance(P1, out var reason), Is.True, reason);
            Step(world, 5); // the room fills
            foreach (var id in world.EnemyIds.ToList()) world.DamageEnemy(id, P1, 1000);
            events = Step(world);
            Assert.That(events.OfType<GateCleared>().Single().GateId, Is.EqualTo("bridge"));

            events = Step(world, 60 * 3); // the countdown brings them out
            Assert.That(events.OfType<ReturnedToHub>().Single().Cleared, Is.True);
            Assert.That(world.InOverworld, Is.True);
            foreach (var id in world.PlayerIds)
                Assert.That(FlatDistance(world.GetPlayer(id).Position, BridgeEntrance), Is.LessThan(2f), "out at the gate's entrance, not back in town");
            Assert.That(world.ClearedGates, Does.Contain("bridge"));
        }

        // --- Gates in regions that are not open -----------------------------------------------------

        private static int XpFromTheGoblinIn(string gateId, Vec3 entrance)
        {
            var world = World();
            world.SetFoundGates(new[] { gateId });
            world.PlacePlayer(P1, entrance);
            AllReady(world);
            Assert.That(world.TryStartGateAtEntrance(P1, out var reason), Is.True, reason);
            Step(world, 5);
            var goblin = world.EnemyIds.Single();
            world.DamageEnemy(goblin, P1, 1000);
            return Step(world).OfType<XpGained>().Where(x => x.Player == P1).Sum(x => x.Amount); // the damage lands on the next tick
        }

        [Test]
        public void AGateInARegionThatIsNotOpenCanBeEnteredButGivesAQuarterOfTheXp()
        {
            Assert.That(XpFromTheGoblinIn("bridge", BridgeEntrance), Is.EqualTo(40), "an open region: full XP");
            Assert.That(XpFromTheGoblinIn("steppe", SteppeEntrance), Is.EqualTo(10), "not open: 25 %, but it can be entered");
        }

        // --- Joining ----------------------------------------------------------------------------------

        [Test]
        public void AJoinerOutInTheWorldAppearsBesideTheHost()
        {
            var world = World();
            world.PlacePlayer(P1, new Vec3(10f, 0f, 30f));

            Assert.That(world.JoinPosition(P2), Is.Null, "out in the world, not a fixed spot");
            var at = JoinPoint.For(world, P2, fallback: Vec3.Zero);
            Assert.That(FlatDistance(at, new Vec3(10f, 0f, 30f)), Is.LessThan(2.1f), "beside the host");
        }

        [Test]
        public void DuringARunAJoinerStillAppearsAtTheLatestCheckpoint()
        {
            var world = World();
            AllReady(world);
            world.TryStartGateFromHub("mill", out _);

            Assert.That(world.JoinPosition(P2), Is.Not.Null);
            Assert.That(FlatDistance(world.JoinPosition(P2).Value, world.Gate.Layout.Entrance), Is.LessThan(2f));
        }

        // --- Guests see the host's world ---------------------------------------------------------------

        [Test]
        public void SnapshotsCarryFoundGatesOpenRegionsAndTheXpCut()
        {
            var world = World();
            world.SetFoundGates(new[] { "steppe" });
            world.SetOpenRegions(new[] { "frost" });
            world.PlacePlayer(P1, SteppeEntrance);
            AllReady(world);
            world.TryStartGateAtEntrance(P1, out _);

            var guest = SimulationWorld.FromSnapshot(Config(), world.CaptureSnapshot());

            Assert.That(guest.FoundGates, Is.EqualTo(new[] { "steppe" }));
            Assert.That(guest.OpenRegions, Is.EqualTo(new[] { "frost" }));
            Step(world, 5);
            Step(guest, 5);
            var goblin = world.EnemyIds.Single();
            world.DamageEnemy(goblin, P1, 1000);
            guest.DamageEnemy(goblin, P1, 1000);
            var hostXp = Step(world).OfType<XpGained>().Sum(x => x.Amount);
            var guestXp = Step(guest).OfType<XpGained>().Sum(x => x.Amount);
            Assert.That(hostXp, Is.EqualTo(10));
            Assert.That(guestXp, Is.EqualTo(hostXp), "the guest's world cuts the same XP");
            Assert.That(guest.GetPlayer(P1).Xp, Is.EqualTo(world.GetPlayer(P1).Xp));
        }
    }
}
