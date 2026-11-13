using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The hub (ticket 19): a small shared space that is also the lobby. Players walk around it
    /// together and mark themselves ready at the gate board; when everyone present is ready the
    /// host can take the party into a gate. After a clear a return portal opens (walking into it,
    /// or a countdown, brings the party home), and the host can leave a run at any time.
    /// </summary>
    public class HubTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);
        private static readonly PlayerId P2 = new PlayerId(2);

        private static EnemyDefinition Dummy => new EnemyDefinition
        {
            Name = "Dummy", MaxHealth = 10, MoveSpeed = 0f, AttackDamage = 0, AttackRange = 0f, XpReward = 0,
        };

        private static GateDefinition SmallGate() => new GateDefinition
        {
            Id = "small", Name = "Small Gate",
            Rooms =
            {
                new RoomDefinition { Width = 10f, Length = 10f },
                new RoomDefinition { Width = 10f, Length = 10f, HasCheckpoint = true, Spawns = { new RoomSpawn(Dummy, Vec3.Zero) } },
            },
        };

        private static HubDefinition Hub() => new HubDefinition
        {
            Width = 20f, Length = 16f, Centre = new Vec3(0f, 0f, -40f), BoardOffset = new Vec3(0f, 0f, 6f), ReturnCountdownSeconds = 2f,
        };

        private static SimulationWorld World(int players = 1)
        {
            var world = new SimulationWorld(new SimulationConfig
            {
                TickRate = 60, PlayerMoveSpeed = 5f, Gates = new[] { SmallGate() }, Hub = Hub(),
            });
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

        private static List<SimulationEvent> Press(SimulationWorld world, PlayerId who, PlayerCommand command) =>
            Step(world, 1, new Dictionary<PlayerId, PlayerCommand> { [who] = command });

        private static readonly PlayerCommand Ready = new PlayerCommand(0f, 0f, setReady: true);
        private static readonly PlayerCommand NotReady = new PlayerCommand(0f, 0f, setReady: false);

        private static void AllReady(SimulationWorld world)
        {
            foreach (var id in world.PlayerIds) Press(world, id, Ready);
        }

        private static void ClearTheGate(SimulationWorld world)
        {
            world.PlacePlayer(P1, new Vec3(3f, 0f, 12f)); // in the last room, but not where the portal will open
            Step(world);
            foreach (var id in world.EnemyIds.ToList()) world.DamageEnemy(id, P1, 1000);
            Step(world);
            Assert.That(world.Gate.IsCleared, Is.True, "test setup: the gate should be cleared");
        }

        // --- The hub ------------------------------------------------------------------------------

        [Test]
        public void PlayersArriveInTheHubAndWalkAroundItTogether()
        {
            var world = World(players: 3);

            Assert.That(world.InHub, Is.True);
            var positions = world.PlayerIds.Select(id => world.GetPlayer(id).Position).ToList();
            Assert.That(positions.Distinct().Count(), Is.EqualTo(3), "not on top of each other");
            foreach (var p in positions) Assert.That((p - Hub().Centre).Length, Is.LessThan(4f), "near the middle");

            Step(world, 600, new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(1f, 0f), [P2] = new PlayerCommand(0f, 1f) });

            Assert.That(world.GetPlayer(P1).Position.X, Is.EqualTo(10f).Within(0.01f), "the hub's east wall");
            Assert.That(world.GetPlayer(P2).Position.Z, Is.EqualTo(-32f).Within(0.01f), "the hub's north wall");
        }

        [Test]
        public void WithoutAHubTheWorldIsOpenAsBefore()
        {
            var world = new SimulationWorld(new SimulationConfig { Gates = new[] { SmallGate() } });
            world.AddPlayer(P1, Vec3.Zero);

            Assert.That(world.InHub, Is.False);
            Step(world, 300, new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(1f, 0f) });
            Assert.That(world.GetPlayer(P1).Position.X, Is.GreaterThan(20f));
        }

        // --- Ready and starting --------------------------------------------------------------------

        [Test]
        public void PlayersMarkThemselvesReadyAndCanChangeTheirMinds()
        {
            var world = World(players: 2);

            var events = Press(world, P1, Ready);
            Assert.That(world.GetPlayer(P1).IsReady, Is.True);
            Assert.That(events.OfType<ReadyChanged>().Single().Ready, Is.True);

            Press(world, P1, NotReady);
            Assert.That(world.GetPlayer(P1).IsReady, Is.False);
            Assert.That(Press(world, P1, NotReady).OfType<ReadyChanged>(), Is.Empty, "no news when nothing changed");
        }

        [Test]
        public void AGateStartsOnlyWhenEveryonePresentIsReady()
        {
            var world = World(players: 2);
            Press(world, P1, Ready);

            Assert.That(world.CanStartGate(out var why), Is.False);
            Assert.That(why, Does.Contain("Player 2"));
            Assert.That(world.TryStartGateFromHub("small", out _), Is.False);
            Assert.That(world.InHub, Is.True);

            Press(world, P2, Ready);
            Assert.That(world.CanStartGate(out _), Is.True);
            Assert.That(world.TryStartGateFromHub("small", out _), Is.True);

            Assert.That(world.HasGate, Is.True);
            Assert.That(world.InHub, Is.False);
            foreach (var id in world.PlayerIds) Assert.That((world.GetPlayer(id).Position - world.Gate.Layout.Entrance).Length, Is.LessThan(2f));
        }

        [Test]
        public void AnAwayPlayerDoesNotHoldThePartyUp()
        {
            var world = World(players: 2);
            world.SetAway(P2, true);
            Press(world, P1, Ready);

            Assert.That(world.CanStartGate(out _), Is.True);
        }

        [Test]
        public void ReadinessIsOnlyForTheHub()
        {
            var world = World(players: 1);
            AllReady(world);
            world.TryStartGateFromHub("small", out _);

            Assert.That(world.GetPlayer(P1).IsReady, Is.False, "cleared on the way in");
            Assert.That(Press(world, P1, Ready).OfType<ReadyChanged>(), Is.Empty, "and cannot be set in a gate");
        }

        // --- Coming home ---------------------------------------------------------------------------

        [Test]
        public void AfterTheClearAPortalOpensAndWalkingInBringsTheWholePartyHome()
        {
            var world = World(players: 2);
            AllReady(world);
            world.TryStartGateFromHub("small", out _);
            ClearTheGate(world);

            Assert.That(world.Gate.ReturnPortal.HasValue, Is.True);
            var portal = world.Gate.ReturnPortal.Value;
            world.PlacePlayer(P1, portal);
            var events = Step(world);

            Assert.That(world.InHub, Is.True);
            Assert.That(world.HasGate, Is.False);
            var home = events.OfType<ReturnedToHub>().Single();
            Assert.That((home.GateId, home.Cleared), Is.EqualTo(("small", true)));
            foreach (var id in world.PlayerIds)
            {
                Assert.That((world.GetPlayer(id).Position - Hub().Centre).Length, Is.LessThan(4f), "both, including the one who did not walk in");
                Assert.That(world.GetPlayer(id).IsDefeated, Is.False);
            }
            Assert.That(world.EnemyIds, Is.Empty, "nothing from the gate follows them");
        }

        [Test]
        public void NoPortalBeforeTheClear()
        {
            var world = World();
            AllReady(world);
            world.TryStartGateFromHub("small", out _);

            Assert.That(world.Gate.ReturnPortal.HasValue, Is.False);
            Assert.That(world.Gate.SecondsUntilReturn, Is.Null);
        }

        [Test]
        public void IfNobodyWalksInTheCountdownBringsThePartyHome()
        {
            var world = World(players: 2);
            AllReady(world);
            world.TryStartGateFromHub("small", out _);
            ClearTheGate(world);
            Assert.That(world.Gate.SecondsUntilReturn, Is.EqualTo(2f).Within(0.05f));

            var events = Step(world, 130);

            Assert.That(world.InHub, Is.True);
            Assert.That(events.OfType<ReturnedToHub>().Single().Cleared, Is.True);
        }

        [Test]
        public void TheHostCanLeaveARunAtAnyTimeAndTheFallenComeHomeToo()
        {
            var world = World(players: 2);
            AllReady(world);
            world.TryStartGateFromHub("small", out _);
            world.PlacePlayer(P1, new Vec3(0f, 0f, 15f));
            Step(world);
            var killer = world.SpawnEnemy(new EnemyDefinition
            {
                Name = "Killer", MaxHealth = 1, MoveSpeed = 0f, AttackDamage = 100000, AttackRange = 1f, AttackWindupSeconds = 0.05f,
            }, world.GetPlayer(P2).Position);
            for (var i = 0; i < 60 && !world.GetPlayer(P2).IsDefeated; i++) Step(world);
            world.DespawnEnemy(killer);

            world.LeaveGate();
            var events = Step(world);

            Assert.That(world.InHub, Is.True);
            Assert.That(events.OfType<ReturnedToHub>().Single().Cleared, Is.False);
            Assert.That(world.GetPlayer(P2).IsDefeated, Is.False, "back on their feet at home");
            Assert.That(world.EnemyIds, Is.Empty);
        }

        [Test]
        public void ComingHomeNeverCostsTheCharacter()
        {
            var world = World();
            world.GrantXp(P1, 30);
            Step(world);
            AllReady(world);
            world.TryStartGateFromHub("small", out _);
            ClearTheGate(world);
            var before = world.GetPlayer(P1);

            world.LeaveGate();
            Step(world);

            Assert.That((world.GetPlayer(P1).Level, world.GetPlayer(P1).Xp), Is.EqualTo((before.Level, before.Xp)));
        }

        [Test]
        public void StandingOnThePortalsSpotWhenTheBossFallsIsNotWalkingIn()
        {
            // The portal opens where the fight was: someone standing there must step out and walk in, so
            // the party sees the clear (and its reward) before anyone is taken home.
            var world = World(players: 1);
            AllReady(world);
            world.TryStartGateFromHub("small", out _);
            world.PlacePlayer(P1, new Vec3(0f, 0f, 15f)); // the last room's centre: where the portal will open
            Step(world);
            foreach (var id in world.EnemyIds.ToList()) world.DamageEnemy(id, P1, 1000);
            Step(world, 10);
            Assert.That(world.HasGate && world.Gate.IsCleared, Is.True, "cleared, and still in the gate");

            world.PlacePlayer(P1, new Vec3(0f, 0f, 19f)); // step out...
            Step(world);
            world.PlacePlayer(P1, new Vec3(0f, 0f, 15f)); // ...and walk in
            Step(world);

            Assert.That(world.InHub, Is.True);
        }

        [Test]
        public void ADisconnectDropsAReadyMark()
        {
            var world = World(players: 2);
            Press(world, P2, Ready);

            world.SetAway(P2, true);
            world.SetAway(P2, false);

            Assert.That(world.GetPlayer(P2).IsReady, Is.False, "coming back, they choose again");
        }

        [Test]
        public void TheWorldRemembersClearedGatesAndAGuestSeesThem()
        {
            var world = World(players: 1);
            world.SetClearedGates(new[] { "old-gate" });
            AllReady(world);
            world.TryStartGateFromHub("small", out _);
            var cleared = new List<SimulationEvent>();
            world.PlacePlayer(P1, new Vec3(3f, 0f, 12f));
            Step(world);
            foreach (var id in world.EnemyIds.ToList()) world.DamageEnemy(id, P1, 1000);
            cleared.AddRange(Step(world));

            Assert.That(cleared.OfType<GateCleared>().Single().GateId, Is.EqualTo("small"), "the clear names its gate");
            Assert.That(world.ClearedGates, Is.EquivalentTo(new[] { "old-gate", "small" }));
            var guest = SimulationWorld.FromSnapshot(new SimulationConfig { TickRate = 60, PlayerMoveSpeed = 5f, Gates = new[] { SmallGate() }, Hub = Hub() },
                world.CaptureSnapshot());
            Assert.That(guest.ClearedGates, Is.EquivalentTo(new[] { "old-gate", "small" }), "it travels to guests");
        }

        [Test]
        public void AGateCannotBeStartedOverOneStillBeingRun()
        {
            var world = World(players: 1);
            AllReady(world);
            world.TryStartGateFromHub("small", out _);

            Assert.Throws<System.InvalidOperationException>(() => world.StartGate("small"));
        }

        // --- Joining -------------------------------------------------------------------------------

        [Test]
        public void SomeoneJoiningWhileThePartyIsInTheHubArrivesInTheHub()
        {
            var world = World(players: 1);
            var p3 = new PlayerId(3);

            world.AddPlayer(p3, world.JoinPosition(p3).Value);

            Assert.That((world.GetPlayer(p3).Position - Hub().Centre).Length, Is.LessThan(4f));
        }

        [Test]
        public void SomeoneJoiningDuringARunArrivesAtItsLatestCheckpoint()
        {
            var world = World(players: 1);
            AllReady(world);
            world.TryStartGateFromHub("small", out _);
            world.PlacePlayer(P1, new Vec3(0f, 0f, 15f));
            Step(world);
            var p3 = new PlayerId(3);

            world.AddPlayer(p3, world.JoinPosition(p3).Value);

            Assert.That((world.GetPlayer(p3).Position - world.Gate.LatestCheckpointPosition).Length, Is.LessThan(1.5f));
        }

        // --- Snapshots -----------------------------------------------------------------------------

        [Test]
        public void ReadinessAndTheReturnCountdownSurviveASnapshot()
        {
            var world = World(players: 2);
            Press(world, P1, Ready);
            var copy = SimulationWorld.FromSnapshot(new SimulationConfig { TickRate = 60, PlayerMoveSpeed = 5f, Gates = new[] { SmallGate() }, Hub = Hub() },
                world.CaptureSnapshot());
            Assert.That(copy.GetPlayer(P1).IsReady, Is.True);
            Assert.That(copy.GetPlayer(P2).IsReady, Is.False);

            AllReady(world);
            world.TryStartGateFromHub("small", out _);
            ClearTheGate(world);
            Step(world, 30);
            var mid = SimulationWorld.FromSnapshot(new SimulationConfig { TickRate = 60, PlayerMoveSpeed = 5f, Gates = new[] { SmallGate() }, Hub = Hub() },
                world.CaptureSnapshot());
            Assert.That(mid.Gate.SecondsUntilReturn, Is.EqualTo(world.Gate.SecondsUntilReturn));
            Step(world, 100);
            Step(mid, 100);
            Assert.That(mid.CaptureSnapshot(), Is.EqualTo(world.CaptureSnapshot()));
        }
    }
}
