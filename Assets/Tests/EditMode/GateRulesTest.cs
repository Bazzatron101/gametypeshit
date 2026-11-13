using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// A gate is the way into a dungeon: rooms from a kit chained end to end, with enemies that
    /// appear as the party reaches each room, checkpoints, and the death rules (ticket 17).
    /// <para>
    /// Solo: three revives at the latest checkpoint, then the gate starts again from the entrance.
    /// Co-op: a fallen player comes back when a living teammate reaches a checkpoint. If the
    /// whole party is down, everyone returns at the latest checkpoint and unfinished rooms refill.
    /// </para>
    /// </summary>
    public class GateRulesTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);
        private static readonly PlayerId P2 = new PlayerId(2);

        private static EnemyDefinition Dummy => new EnemyDefinition
        {
            Name = "Dummy", MaxHealth = 10, MoveSpeed = 0f, AttackDamage = 0, AttackRange = 0f, XpReward = 60,
        };

        private static EnemyDefinition Killer => new EnemyDefinition
        {
            Name = "Killer", MaxHealth = 1, MoveSpeed = 0f, AttackDamage = 100000, AttackRange = 1f, AttackWindupSeconds = 0.05f, AttackCooldownSeconds = 0.05f,
        };

        /// <summary>
        /// Three rooms in a row, each 10 wide: an empty entrance hall (z 0 to 10), a fight with a
        /// checkpoint in the middle (z 10 to 30), and a last fight with a checkpoint (z 30 to 50).
        /// </summary>
        private static GateDefinition TestGate() => new GateDefinition
        {
            Id = "test-gate", Name = "Test Gate", WipeDelaySeconds = 0.5f,
            Rooms =
            {
                new RoomDefinition { Width = 10f, Length = 10f },
                new RoomDefinition
                {
                    Width = 10f, Length = 20f, HasCheckpoint = true,
                    Spawns = { new RoomSpawn(Dummy, new Vec3(-3f, 0f, 6f)), new RoomSpawn(Dummy, new Vec3(3f, 0f, 6f)) },
                },
                new RoomDefinition { Width = 10f, Length = 20f, HasCheckpoint = true, Spawns = { new RoomSpawn(Dummy, new Vec3(0f, 0f, 6f)) } },
            },
        };

        private static readonly Vec3 Checkpoint1 = new Vec3(0f, 0f, 20f); // the middle of the second room
        private static readonly Vec3 Checkpoint2 = new Vec3(0f, 0f, 40f);

        private static SimulationWorld World(int players = 1)
        {
            var world = new SimulationWorld(new SimulationConfig
            {
                TickRate = 60,
                PlayerMoveSpeed = 5f,
                Progression = new ProgressionDefinition
                {
                    LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 }, FreePointsPerLevel = 3, StartingStatValue = 10,
                },
                Gates = new[] { TestGate() },
            });
            for (var i = 1; i <= players; i++) world.AddPlayer(new PlayerId(i), new Vec3(100f, 0f, 100f));
            world.StartGate("test-gate");
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

        private static readonly PlayerCommand Respawn = new PlayerCommand(0f, 0f, respawn: true);

        /// <summary>Puts a player somewhere and lets a tick pass so the gate notices.</summary>
        private static List<SimulationEvent> Walk(SimulationWorld world, PlayerId who, Vec3 to)
        {
            world.PlacePlayer(who, to);
            return Step(world);
        }

        /// <summary>Defeats a player where they stand with a one-hit enemy, then removes it.</summary>
        private static void Defeat(SimulationWorld world, PlayerId who)
        {
            var killer = world.SpawnEnemy(Killer, world.GetPlayer(who).Position);
            for (var i = 0; i < 60 && !world.GetPlayer(who).IsDefeated; i++) Step(world);
            world.DespawnEnemy(killer);
            Assert.That(world.GetPlayer(who).IsDefeated, Is.True, "test setup: the player should be down");
        }

        private static IEnumerable<EnemyId> Living(SimulationWorld world) => world.EnemyIds.Where(id => !world.GetEnemy(id).IsDefeated);

        // --- Rooms -------------------------------------------------------------------------------

        [Test]
        public void RoomsFromTheKitChainEndToEnd()
        {
            var layout = World().Gate.Layout;

            Assert.That(layout.Rooms.Select(r => (r.MinZ, r.MaxZ)), Is.EqualTo(new[] { (0f, 10f), (10f, 30f), (30f, 50f) }));
            Assert.That(layout.Rooms.Select(r => (r.MinX, r.MaxX)), Has.All.EqualTo((-5f, 5f)));
            Assert.That(layout.Checkpoints.Select(c => c.Position), Is.EqualTo(new[] { Checkpoint1, Checkpoint2 }));
            Assert.That(layout.Entrance.Z, Is.InRange(0.5f, 5f), "just inside the first room");
        }

        [Test]
        public void StartingAGatePutsThePartyAtTheEntranceOnTheirFeetAndClearsOldEnemies()
        {
            var world = new SimulationWorld(new SimulationConfig { Gates = new[] { TestGate() } });
            world.AddPlayer(P1, new Vec3(100f, 0f, 100f));
            world.AddPlayer(P2, new Vec3(-100f, 0f, 100f));
            world.SpawnEnemy(Dummy, new Vec3(100f, 0f, 101f));
            Defeat(world, P2);

            world.StartGate("test-gate");

            var gate = world.Gate;
            foreach (var id in new[] { P1, P2 })
            {
                var player = world.GetPlayer(id);
                Assert.That((player.Position - gate.Layout.Entrance).Length, Is.LessThan(2f));
                Assert.That(player.IsDefeated, Is.False);
                Assert.That(player.Health, Is.EqualTo(player.MaxHealth));
            }
            Assert.That(world.GetPlayer(P1).Position, Is.Not.EqualTo(world.GetPlayer(P2).Position), "not on top of each other");
            Assert.That(world.EnemyIds, Is.Empty);
            Assert.That(world.HasGate, Is.True);
        }

        [Test]
        public void ARoomsEnemiesAppearWhenThePartyReachesIt()
        {
            var world = World();
            Step(world, 5);
            Assert.That(world.EnemyIds, Is.Empty, "nothing in the entrance hall");
            Assert.That(world.Gate.RoomState(1), Is.EqualTo(RoomState.NotEntered));

            var events = Walk(world, P1, new Vec3(0f, 0f, 11f));

            Assert.That(world.EnemyIds.Count, Is.EqualTo(2));
            Assert.That(world.EnemyIds.Select(id => world.GetEnemy(id).Position), Is.EquivalentTo(new[] { new Vec3(-3f, 0f, 26f), new Vec3(3f, 0f, 26f) }),
                "at their places in the room");
            Assert.That(events.OfType<RoomEntered>().Single().Room, Is.EqualTo(1));
            Assert.That(world.Gate.RoomState(1), Is.EqualTo(RoomState.Entered));
        }

        [Test]
        public void ARoomIsClearedWhenItsEnemiesAreDefeatedAndTheGateWhenEveryRoomIs()
        {
            var world = World();
            Assert.That(world.Gate.RoomState(0), Is.EqualTo(RoomState.Cleared), "an empty room has nothing to clear");

            var events = Walk(world, P1, new Vec3(-3f, 0f, 25f));
            for (var i = 0; i < 4; i++) // two dummies, 10 health each, 10 a swing
            {
                events.AddRange(Press(world, P1, new PlayerCommand(0f, 0f, lightAttack: true)));
                events.AddRange(Step(world, 40));
                if (i == 1) events.AddRange(Walk(world, P1, new Vec3(3f, 0f, 25f)));
            }
            Assert.That(world.Gate.RoomState(1), Is.EqualTo(RoomState.Cleared));
            Assert.That(events.OfType<RoomCleared>().Single().Room, Is.EqualTo(1));
            Assert.That(world.Gate.IsCleared, Is.False);

            events = Walk(world, P1, new Vec3(0f, 0f, 45f));
            events.AddRange(Press(world, P1, new PlayerCommand(0f, 0f, lightAttack: true)));
            events.AddRange(Step(world, 40));

            Assert.That(world.Gate.IsCleared, Is.True);
            Assert.That(events.OfType<GateCleared>().Count(), Is.EqualTo(1));
            Assert.That(Step(world, 10).OfType<GateCleared>(), Is.Empty, "announced once");
        }

        [Test]
        public void WallsKeepPlayersInAndTheWayOnwardIsAlwaysOpen()
        {
            var world = World();

            Step(world, 300, new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(-1f, 0f) }); // west into the wall
            Assert.That(world.GetPlayer(P1).Position.X, Is.EqualTo(-5f).Within(0.01f));

            Step(world, 300, new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(0f, -1f) }); // back out of the entrance
            Assert.That(world.GetPlayer(P1).Position.Z, Is.EqualTo(0f).Within(0.01f));

            Step(world, 600, new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(0f, 1f) }); // 50 m north in 10 s
            Assert.That(world.GetPlayer(P1).Position.Z, Is.EqualTo(50f).Within(0.01f), "past both fights to the far wall: nothing is locked");
            Assert.That(Living(world).Count(), Is.EqualTo(3), "the enemies are still there");
        }

        // --- Checkpoints ---------------------------------------------------------------------------

        [Test]
        public void ACheckpointIsActivatedWhenALivingPlayerReachesIt()
        {
            var world = World();
            Assert.That(world.Gate.LatestCheckpoint, Is.EqualTo(-1), "only the entrance so far");

            var near = Walk(world, P1, Checkpoint1 + new Vec3(4f, 0f, 0f));
            Assert.That(near.OfType<CheckpointActivated>(), Is.Empty, "not close enough");

            var events = Walk(world, P1, Checkpoint1 + new Vec3(1f, 0f, 0f));
            events.AddRange(Step(world, 10));

            Assert.That(events.OfType<CheckpointActivated>().Select(c => (c.Checkpoint, c.Player)), Is.EqualTo(new[] { (0, P1) }), "once");
            Assert.That(world.Gate.IsCheckpointActivated(0), Is.True);
            Assert.That(world.Gate.LatestCheckpoint, Is.EqualTo(0));
            Assert.That(world.Gate.LatestCheckpointPosition, Is.EqualTo(Checkpoint1));
        }

        [Test]
        public void TheLatestCheckpointIsTheFurthestOneReached()
        {
            var world = World();
            Walk(world, P1, Checkpoint2);
            Walk(world, P1, Checkpoint1);

            Assert.That(world.Gate.LatestCheckpoint, Is.EqualTo(1), "walking back does not move it");
        }

        // --- Solo ----------------------------------------------------------------------------------

        [Test]
        public void SoloAFallenPlayerRevivesAtTheLatestCheckpointAndUsesOneOfThree()
        {
            var world = World();
            Walk(world, P1, Checkpoint1);
            Walk(world, P1, new Vec3(0f, 0f, 28f));
            Assert.That(world.Gate.RevivesLeft(P1), Is.EqualTo(3));
            Defeat(world, P1);
            var enemiesBefore = Living(world).Count();

            Step(world, 30);
            Assert.That(world.GetPlayer(P1).IsDefeated, Is.True, "nothing happens until they choose to revive");
            var events = Press(world, P1, Respawn);

            var player = world.GetPlayer(P1);
            Assert.That(player.IsDefeated, Is.False);
            Assert.That(player.Health, Is.EqualTo(player.MaxHealth));
            Assert.That((player.Position - Checkpoint1).Length, Is.LessThan(1.5f));
            Assert.That(world.Gate.RevivesLeft(P1), Is.EqualTo(2));
            var revived = events.OfType<PlayerRevived>().Single();
            Assert.That((revived.Player, revived.Checkpoint, revived.RevivesLeft), Is.EqualTo((P1, 0, 2)));
            Assert.That(Living(world).Count(), Is.EqualTo(enemiesBefore), "the fight is as they left it");
        }

        [Test]
        public void SoloBeforeAnyCheckpointARevivePutsYouBackAtTheEntrance()
        {
            var world = World();
            Walk(world, P1, new Vec3(0f, 0f, 12f));
            Defeat(world, P1);

            var revived = Press(world, P1, Respawn).OfType<PlayerRevived>().Single();

            Assert.That(revived.Checkpoint, Is.EqualTo(-1));
            Assert.That((world.GetPlayer(P1).Position - world.Gate.Layout.Entrance).Length, Is.LessThan(1.5f));
            Assert.That(world.Gate.RoomState(1), Is.EqualTo(RoomState.Entered), "the gate does not reset for a revive");
        }

        [Test]
        public void SoloAfterThreeRevivesTheGateStartsAgainFromTheEntranceAndTheCharacterKeepsEverything()
        {
            var world = World();
            world.GrantXp(P1, 60);
            Walk(world, P1, Checkpoint1);
            for (var i = 0; i < 3; i++)
            {
                Defeat(world, P1);
                Press(world, P1, Respawn);
            }
            Assert.That(world.Gate.RevivesLeft(P1), Is.Zero);
            Defeat(world, P1);

            var events = Press(world, P1, Respawn);

            var gate = world.Gate;
            Assert.That(events.OfType<GateRestarted>().Count(), Is.EqualTo(1));
            Assert.That((world.GetPlayer(P1).Position - gate.Layout.Entrance).Length, Is.LessThan(1.5f));
            Assert.That(world.GetPlayer(P1).IsDefeated, Is.False);
            Assert.That(gate.Attempt, Is.EqualTo(2));
            Assert.That(gate.RevivesLeft(P1), Is.EqualTo(3), "three again for the new attempt");
            Assert.That(gate.LatestCheckpoint, Is.EqualTo(-1), "checkpoints are lost");
            Assert.That(gate.RoomState(1), Is.EqualTo(RoomState.NotEntered));
            Assert.That(world.EnemyIds, Is.Empty, "every room will refill");
            Assert.That(world.GetPlayer(P1).Level, Is.EqualTo(2), "a restart costs progress in the gate only, never the character");
        }

        [Test]
        public void AskingToRespawnWhileStandingDoesNothing()
        {
            var world = World();
            var before = world.GetPlayer(P1).Position;

            var events = Press(world, P1, Respawn);

            Assert.That(events.OfType<PlayerRevived>(), Is.Empty);
            Assert.That(world.Gate.RevivesLeft(P1), Is.EqualTo(3));
            Assert.That(world.GetPlayer(P1).Position, Is.EqualTo(before));
        }

        // --- Co-op ---------------------------------------------------------------------------------

        [Test]
        public void CoopAFallenPlayerComesBackWhenALivingTeammateReachesACheckpoint()
        {
            var world = World(players: 2);
            Walk(world, P2, new Vec3(0f, 0f, 12f));
            Defeat(world, P2);

            Assert.That(Press(world, P2, Respawn).OfType<PlayerRevived>(), Is.Empty, "no respawning by yourself in co-op");
            Step(world, 120);
            Assert.That(world.GetPlayer(P2).IsDefeated, Is.True, "they wait");

            var events = Walk(world, P1, Checkpoint1);

            var revived = events.OfType<PlayerRevived>().Single();
            Assert.That((revived.Player, revived.Checkpoint), Is.EqualTo((P2, 0)));
            Assert.That(world.GetPlayer(P2).IsDefeated, Is.False);
            Assert.That(world.GetPlayer(P2).Health, Is.EqualTo(world.GetPlayer(P2).MaxHealth));
            Assert.That((world.GetPlayer(P2).Position - Checkpoint1).Length, Is.LessThan(1.5f), "at that checkpoint");
            Assert.That(world.Gate.RevivesLeft(P2), Is.EqualTo(3), "the solo limit does not apply");
        }

        [Test]
        public void CoopATeammateMustReachTheCheckpointNotJustBeStandingOnIt()
        {
            var world = World(players: 2);
            Walk(world, P1, Checkpoint1); // P1 is already there
            Walk(world, P2, new Vec3(0f, 0f, 28f));
            Defeat(world, P2);

            Step(world, 60);
            Assert.That(world.GetPlayer(P2).IsDefeated, Is.True, "standing on it when they fell does not count");

            Walk(world, P1, Checkpoint1 + new Vec3(0f, 0f, 5f)); // step away...
            Walk(world, P1, Checkpoint1);                         // ...and reach it again

            Assert.That(world.GetPlayer(P2).IsDefeated, Is.False);
        }

        [Test]
        public void CoopWalkingBackToAnEarlierCheckpointAlsoBringsThemBack()
        {
            var world = World(players: 2);
            Walk(world, P1, Checkpoint1);
            Walk(world, P1, Checkpoint2);
            Walk(world, P1, new Vec3(0f, 0f, 48f));
            Defeat(world, P2);

            Walk(world, P1, Checkpoint1); // the earlier one

            Assert.That(world.GetPlayer(P2).IsDefeated, Is.False);
            Assert.That((world.GetPlayer(P2).Position - Checkpoint1).Length, Is.LessThan(1.5f));
        }

        [Test]
        public void CoopAWipeReturnsEveryoneToTheLatestCheckpointAndRefillsUnfinishedRooms()
        {
            var world = World(players: 2);
            world.GrantXp(P1, 60);
            // Clear the last room's single dummy, reach both checkpoints, then get caught in the unfinished middle room.
            Walk(world, P1, new Vec3(0f, 0f, 45f));
            Press(world, P1, new PlayerCommand(0f, 0f, lightAttack: true));
            Step(world, 40);
            Assert.That(world.Gate.RoomState(2), Is.EqualTo(RoomState.Cleared));
            Walk(world, P1, Checkpoint1);
            Walk(world, P1, Checkpoint2);
            Walk(world, P1, new Vec3(-3f, 0f, 25f));
            Press(world, P1, new PlayerCommand(0f, 0f, lightAttack: true)); // one of the middle room's two dummies
            Step(world, 40);
            Walk(world, P2, new Vec3(0f, 0f, 12f));
            Assert.That(Living(world).Count(), Is.EqualTo(1));

            var before = world.GetPlayer(P1);
            Defeat(world, P1);
            Defeat(world, P2);
            var events = Step(world, 60); // the wipe delay is half a second

            Assert.That(events.OfType<GateWiped>().Count(), Is.EqualTo(1));
            foreach (var id in new[] { P1, P2 })
            {
                var player = world.GetPlayer(id);
                Assert.That(player.IsDefeated, Is.False);
                Assert.That(player.Health, Is.EqualTo(player.MaxHealth));
                Assert.That((player.Position - Checkpoint2).Length, Is.LessThan(1.5f), "the latest checkpoint");
            }
            Assert.That(world.Gate.RoomState(1), Is.EqualTo(RoomState.NotEntered), "the unfinished room starts over");
            Assert.That(Living(world), Is.Empty, "its remaining enemy is gone until the party returns");
            Assert.That(world.Gate.RoomState(2), Is.EqualTo(RoomState.Cleared), "cleared rooms stay cleared");
            Assert.That(world.Gate.IsCheckpointActivated(0) && world.Gate.IsCheckpointActivated(1), Is.True, "checkpoints are kept");
            Assert.That(world.Gate.Attempt, Is.EqualTo(1), "not a restart");
            Assert.That((world.GetPlayer(P1).Level, world.GetPlayer(P1).Xp), Is.EqualTo((before.Level, before.Xp)), "a wipe never costs the character");
            Assert.That(before.Level, Is.GreaterThan(1));

            Walk(world, P1, new Vec3(0f, 0f, 12f));
            Assert.That(Living(world).Count(), Is.EqualTo(2), "and it refills in full when they come back");
        }

        [Test]
        public void ASoloRunAFriendJoinsSwitchesToTheCoopRuleAndUsedRevivesStayUsed()
        {
            var world = World();
            Walk(world, P1, Checkpoint1);
            Defeat(world, P1);
            Press(world, P1, Respawn);
            Assert.That(world.Gate.RevivesLeft(P1), Is.EqualTo(2));

            world.AddPlayer(P2, world.Gate.JoinPosition(P2)); // a friend joins at the latest checkpoint
            Assert.That((world.GetPlayer(P2).Position - Checkpoint1).Length, Is.LessThan(1.5f));
            Walk(world, P2, new Vec3(0f, 0f, 28f));
            Defeat(world, P1);

            Assert.That(Press(world, P1, Respawn).OfType<PlayerRevived>(), Is.Empty, "co-op now: wait for the friend");
            Walk(world, P2, Checkpoint1);

            Assert.That(world.GetPlayer(P1).IsDefeated, Is.False);
            Assert.That(world.Gate.RevivesLeft(P1), Is.EqualTo(2), "not given back, not used up");
            Assert.That(world.Gate.RevivesLeft(P2), Is.EqualTo(3), "a late joiner starts with none used");
        }

        [Test]
        public void APlayerWhoIsAwayDoesNotCountAsATeammate()
        {
            // A disconnected friend's character stays in the run, but cannot fetch anyone or be waited for.
            var world = World(players: 2);
            Walk(world, P1, Checkpoint1);
            world.SetAway(P2, true);
            Walk(world, P1, new Vec3(0f, 0f, 28f));
            Defeat(world, P1);

            var events = Press(world, P1, Respawn);

            Assert.That(events.OfType<PlayerRevived>().Single().RevivesLeft, Is.EqualTo(2), "the solo rule applies while they are away");
            Assert.That(world.GetPlayer(P2).IsAway, Is.True);
        }

        [Test]
        public void EnemiesLeaveAwayPlayersAlone()
        {
            var world = World(players: 2);
            Walk(world, P1, new Vec3(0f, 0f, 8f)); // well out of its reach
            world.SetAway(P2, true);
            world.SpawnEnemy(Killer, world.GetPlayer(P2).Position);

            Step(world, 60);

            Assert.That(world.GetPlayer(P2).IsDefeated, Is.False);
            Assert.That(world.GetPlayer(P1).IsDefeated, Is.False, "P1 is out of its reach");
        }

        [Test]
        public void RevivesAreKeptByPlayerSoLeavingAndComingBackDoesNotRefillThem()
        {
            var world = World();
            Walk(world, P1, Checkpoint1);
            Defeat(world, P1);
            Press(world, P1, Respawn);

            world.SetAway(P1, true);
            Step(world, 30);
            world.SetAway(P1, false);

            Assert.That(world.Gate.RevivesLeft(P1), Is.EqualTo(2));
        }

        // --- Edges found in review -------------------------------------------------------------------

        [Test]
        public void AFriendJoiningAtTheCheckpointDoesNotCountAsReachingIt()
        {
            // Otherwise joining (or reconnecting) would be a free revive for everyone who is down.
            var world = World(players: 2);
            Walk(world, P1, Checkpoint1);
            Walk(world, P1, new Vec3(0f, 0f, 28f));
            Defeat(world, P2);
            var p3 = new PlayerId(3);

            world.AddPlayer(p3, world.Gate.JoinPosition(p3));
            Step(world, 30);
            Assert.That(world.GetPlayer(P2).IsDefeated, Is.True, "placed there, not arrived");

            Walk(world, p3, Checkpoint1 + new Vec3(0f, 0f, 6f));
            Walk(world, p3, Checkpoint1);
            Assert.That(world.GetPlayer(P2).IsDefeated, Is.False, "walking onto it does");
        }

        [Test]
        public void ComingBackFromAwayOntoACheckpointDoesNotCountAsReachingIt()
        {
            var world = World(players: 3);
            var p3 = new PlayerId(3);
            Walk(world, P1, Checkpoint1);
            Walk(world, P1, new Vec3(0f, 0f, 28f));
            world.SetAway(p3, true);
            Defeat(world, P2);

            world.PlacePlayer(p3, world.Gate.JoinPosition(p3)); // what the host does for a rejoiner
            world.SetAway(p3, false);
            Step(world, 30);

            Assert.That(world.GetPlayer(P2).IsDefeated, Is.True);
        }

        [Test]
        public void BeingRevivedOntoACheckpointIsNotReachingIt()
        {
            // P1 is put on the checkpoint by the revive. That must not count as P1 arriving a tick
            // later, or it would bring back P3, who returns (still down) just after the revive.
            var world = World(players: 3);
            var p3 = new PlayerId(3);
            Walk(world, P2, new Vec3(0f, 0f, 12f));
            Walk(world, p3, new Vec3(2f, 0f, 14f));
            Defeat(world, P1);
            Defeat(world, p3);
            world.SetAway(p3, true); // dropped while down: not revived with the others

            Walk(world, P2, Checkpoint1);
            Assert.That(world.GetPlayer(P1).IsDefeated, Is.False, "P1 is back, on the checkpoint");
            Assert.That(world.GetPlayer(p3).IsDefeated, Is.True);
            world.SetAway(p3, false); // reconnects, still down where they fell
            Step(world, 30);

            Assert.That(world.GetPlayer(p3).IsDefeated, Is.True, "nobody has walked onto a checkpoint since");

            Walk(world, P2, Checkpoint1 + new Vec3(0f, 0f, 6f));
            Walk(world, P2, Checkpoint1);
            Assert.That(world.GetPlayer(p3).IsDefeated, Is.False, "until a teammate does");
        }

        [Test]
        public void AnAttackAlreadyWindingUpDoesNotLandOnAPlayerWhoGoesAway()
        {
            var world = World(players: 2);
            Walk(world, P1, new Vec3(0f, 0f, 8f));
            var slow = new EnemyDefinition
            {
                Name = "Slow Killer", MaxHealth = 1, MoveSpeed = 0f, AttackDamage = 100000, AttackRange = 1f, AttackWindupSeconds = 0.5f,
            };
            world.SpawnEnemy(slow, world.GetPlayer(P2).Position);
            var started = Step(world, 5);
            Assert.That(started.OfType<EnemyAttackStarted>().Any(a => a.Target == P2), Is.True, "test setup: the hit is on its way");

            world.SetAway(P2, true);
            Step(world, 60);

            Assert.That(world.GetPlayer(P2).IsDefeated, Is.False);
        }

        [Test]
        public void ASoloRestartLeavesAnAwayPlayerWhereTheyStood()
        {
            var world = World(players: 2);
            Walk(world, P2, new Vec3(2f, 0f, 25f));
            world.SetAway(P2, true);
            var stoodAt = world.GetPlayer(P2).Position;
            for (var i = 0; i < 3; i++)
            {
                Defeat(world, P1);
                Press(world, P1, Respawn);
            }
            Defeat(world, P1);

            var events = Press(world, P1, Respawn);

            Assert.That(events.OfType<GateRestarted>().Count(), Is.EqualTo(1));
            Assert.That(world.GetPlayer(P2).Position, Is.EqualTo(stoodAt));
        }

        [Test]
        public void AWipeThatHasBegunStillHappensIfATeammateDropsOut()
        {
            // Otherwise the one left would have to spend a solo revive, or restart the gate, for a party wipe.
            var world = World(players: 2);
            Walk(world, P1, Checkpoint1);
            Walk(world, P1, new Vec3(0f, 0f, 28f));
            Defeat(world, P1);
            Defeat(world, P2);
            Step(world, 5); // the wipe countdown is running

            world.SetAway(P2, true);
            var events = Step(world, 60);

            Assert.That(events.OfType<GateWiped>().Count(), Is.EqualTo(1));
            Assert.That(world.GetPlayer(P1).IsDefeated, Is.False);
            Assert.That(world.Gate.RevivesLeft(P1), Is.EqualTo(3), "no solo revive was spent");
            Assert.That(world.GetPlayer(P2).IsDefeated, Is.True, "the away player is left as they were");
        }

        [Test]
        public void AWipeBeforeAnyCheckpointReturnsThePartyToTheEntrance()
        {
            var world = World(players: 2);
            Walk(world, P1, new Vec3(0f, 0f, 12f));
            Walk(world, P2, new Vec3(1f, 0f, 14f));
            Defeat(world, P1);
            Defeat(world, P2);

            var wiped = Step(world, 60).OfType<GateWiped>().Single();

            Assert.That(wiped.Checkpoint, Is.EqualTo(-1));
            foreach (var id in new[] { P1, P2 })
                Assert.That((world.GetPlayer(id).Position - world.Gate.Layout.Entrance).Length, Is.LessThan(1.5f));
        }

        [Test]
        public void TheDefaultWipeDelayIsThreeSeconds()
        {
            Assert.That(new GateDefinition().WipeDelaySeconds, Is.EqualTo(3f));
        }

        [Test]
        public void AClearedRoomsBodiesAreTakenAwayAfterAFewSeconds()
        {
            // Defeated enemies would otherwise ride along in every snapshot for the rest of the gate.
            var world = World();
            Walk(world, P1, new Vec3(0f, 0f, 45f));
            Press(world, P1, new PlayerCommand(0f, 0f, lightAttack: true));
            Step(world, 40);
            Assert.That(world.Gate.RoomState(2), Is.EqualTo(RoomState.Cleared));
            Assert.That(world.EnemyIds.Count, Is.EqualTo(1), "still there to be seen falling");

            Step(world, 200);

            Assert.That(world.EnemyIds, Is.Empty);
            Assert.That(world.Gate.RoomState(2), Is.EqualTo(RoomState.Cleared), "the room stays cleared");
        }

        // --- Snapshots -----------------------------------------------------------------------------

        [Test]
        public void AGateInProgressSurvivesASnapshotAndCarriesOnTheSame()
        {
            var world = World(players: 2);
            Walk(world, P1, Checkpoint1);
            Defeat(world, P1);
            Walk(world, P2, new Vec3(0f, 0f, 28f));
            world.SetAway(P2, true);
            Press(world, P1, Respawn);
            world.SetAway(P2, false);
            Step(world, 3);

            var copy = SimulationWorld.FromSnapshot(new SimulationConfig
            {
                TickRate = 60, PlayerMoveSpeed = 5f,
                Progression = new ProgressionDefinition
                {
                    LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 }, FreePointsPerLevel = 3, StartingStatValue = 10,
                },
                Gates = new[] { TestGate() },
            }, world.CaptureSnapshot());

            Assert.That(copy.HasGate, Is.True);
            Assert.That(copy.Gate.RevivesLeft(P1), Is.EqualTo(2));
            Assert.That(copy.Gate.LatestCheckpoint, Is.EqualTo(0));
            Assert.That(copy.Gate.RoomState(1), Is.EqualTo(RoomState.Entered));

            // Both carry on identically: P2 walks on past the far checkpoint while P1 fights.
            foreach (var w in new[] { world, copy })
            {
                Step(w, 200, new Dictionary<PlayerId, PlayerCommand> { [P2] = new PlayerCommand(0f, 1f), [P1] = new PlayerCommand(0.3f, 1f, lightAttack: true) });
            }
            Assert.That(copy.CaptureSnapshot(), Is.EqualTo(world.CaptureSnapshot()));
        }

        [Test]
        public void WithoutAGateNothingChanges()
        {
            var world = new SimulationWorld(new SimulationConfig { Gates = new[] { TestGate() } });
            world.AddPlayer(P1, new Vec3(100f, 0f, 100f));

            Step(world, 60, new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(1f, 0f, respawn: true) });

            Assert.That(world.HasGate, Is.False);
            Assert.That(world.Gate, Is.Null);
            Assert.That(world.GetPlayer(P1).Position.X, Is.GreaterThan(104f), "no walls anywhere");
        }
    }
}
