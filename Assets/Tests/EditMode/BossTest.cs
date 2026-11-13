using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The first boss (ticket 18): a Warlord with three telegraphed attacks, each with an obvious
    /// answer. A sweep in front (get behind it), a slam all around it (get out), and a charge in a
    /// line at someone far away (step aside). At half health it enrages once: faster attacks, and
    /// goblins join the fight. Defeating it completes the gate, and everyone present is rewarded
    /// with a modifier from the gate's pool that they do not have yet.
    /// </summary>
    public class BossTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);
        private static readonly PlayerId P2 = new PlayerId(2);

        private static readonly EnemyDefinition Add = new EnemyDefinition
        {
            Name = "Goblin", MaxHealth = 10, MoveSpeed = 0f, AttackDamage = 0, AttackRange = 0f, XpReward = 0,
        };

        private static EnemyDefinition Warlord(int health = 1000) => new EnemyDefinition
        {
            Name = "Warlord", MaxHealth = health, MoveSpeed = 2f, StopDistance = 1.5f, XpReward = 200,
            Boss = new BossDefinition
            {
                Attacks =
                {
                    new BossAttackDefinition { Kind = BossAttackKind.Sweep, Damage = 20, WindupSeconds = 0.5f, CooldownSeconds = 1f, Range = 4f, ArcDegrees = 120f },
                    new BossAttackDefinition { Kind = BossAttackKind.Slam, Damage = 30, WindupSeconds = 0.5f, CooldownSeconds = 1f, Radius = 3f },
                    new BossAttackDefinition { Kind = BossAttackKind.Charge, Damage = 25, WindupSeconds = 0.5f, CooldownSeconds = 1f, Range = 10f, Radius = 1f, Speed = 10f },
                },
                EnrageAtHealthPercent = 50,
                EnragedTimePercent = 50,
                EnrageAdds = { new RoomSpawn(Add, new Vec3(-2f, 0f, 0f)), new RoomSpawn(Add, new Vec3(2f, 0f, 0f)) },
            },
        };

        private static SimulationConfig Config(params GateDefinition[] gates) => new SimulationConfig
        {
            TickRate = 60,
            PlayerMoveSpeed = 5f,
            PlayerMaxHealth = 1000,
            Progression = new ProgressionDefinition
            {
                LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 }, FreePointsPerLevel = 3, StartingStatValue = 10,
            },
            Gates = gates,
        };

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

        /// <summary>Runs until the boss starts an attack; returns it.</summary>
        private static BossAttackStarted NextAttack(SimulationWorld world, int maxTicks = 300)
        {
            for (var i = 0; i < maxTicks; i++)
            {
                var started = Step(world).OfType<BossAttackStarted>().FirstOrDefault();
                if (started != null) return started;
            }
            Assert.Fail("the boss never attacked");
            return null;
        }

        /// <summary>Steps through a charge that has been telegraphed, until it has run and stopped. Returns what happened.</summary>
        private static List<SimulationEvent> RunCharge(SimulationWorld world, EnemyId boss, System.Action<int> ticksCharging = null)
        {
            var events = new List<SimulationEvent>();
            var charging = 0;
            for (var i = 0; i < 300; i++)
            {
                events.AddRange(Step(world));
                if (world.GetEnemy(boss).IsCharging) charging++;
                else if (charging > 0)
                {
                    ticksCharging?.Invoke(charging + 1);
                    return events;
                }
            }
            Assert.Fail("the charge never finished");
            return events;
        }

        /// <summary>A boss facing +Z (towards the far end) at the origin, and players where given.</summary>
        private static SimulationWorld Arena(out EnemyId boss, params Vec3[] players)
        {
            var world = new SimulationWorld(Config());
            for (var i = 0; i < players.Length; i++) world.AddPlayer(new PlayerId(i + 1), players[i]);
            boss = world.SpawnEnemy(Warlord(), Vec3.Zero);
            return world;
        }

        // --- Choosing and telegraphing -----------------------------------------------------------

        [Test]
        public void SomeoneCloseInFrontGetsTheSweep()
        {
            var world = Arena(out var boss, new Vec3(0f, 0f, 2f));

            var attack = NextAttack(world);

            Assert.That(attack.Kind, Is.EqualTo(BossAttackKind.Sweep));
            Assert.That(attack.Enemy, Is.EqualTo(boss));
            Assert.That(attack.Direction.Z, Is.GreaterThan(0.99f), "aimed at the player");
            Assert.That(attack.LandsAtTick - attack.Tick, Is.EqualTo(30), "half a second to read it");
            var state = world.GetEnemy(boss);
            Assert.That(state.CurrentBossAttack.Kind, Is.EqualTo(BossAttackKind.Sweep), "the telegraph is in the world's state too, for drawing");
            Assert.That(state.AttackLandsAtTick, Is.EqualTo(attack.LandsAtTick));
        }

        [Test]
        public void SomeoneFarAwayGetsTheCharge()
        {
            var world = Arena(out _, new Vec3(0f, 0f, 8f));

            Assert.That(NextAttack(world).Kind, Is.EqualTo(BossAttackKind.Charge));
        }

        [Test]
        public void GettingBehindItAfterASweepBringsTheSlam()
        {
            var world = Arena(out _, new Vec3(0f, 0f, 2f));
            Assert.That(NextAttack(world).Kind, Is.EqualTo(BossAttackKind.Sweep));
            world.PlacePlayer(P1, new Vec3(0f, 0f, -2f)); // round the back while it swings

            Assert.That(NextAttack(world).Kind, Is.EqualTo(BossAttackKind.Slam), "it cannot turn round fast: it slams instead");
        }

        [Test]
        public void TwoPlayersCrowdingItBringTheSlam()
        {
            var world = Arena(out _, new Vec3(-1.5f, 0f, 1.5f), new Vec3(1.5f, 0f, 1.5f));

            Assert.That(NextAttack(world).Kind, Is.EqualTo(BossAttackKind.Slam));
        }

        [Test]
        public void AnAttackIsAimedWhenItStartsSoMovingAwayInTimeAvoidsIt()
        {
            var world = Arena(out _, new Vec3(0f, 0f, 2f));
            var attack = NextAttack(world);
            Assert.That(attack.Kind, Is.EqualTo(BossAttackKind.Sweep));

            world.PlacePlayer(P1, new Vec3(-2.5f, 0f, -1.5f)); // out of the marked area, behind it
            var events = Step(world, 40);

            Assert.That(events.OfType<PlayerDamaged>(), Is.Empty, "the sweep did not follow the player");
        }

        // --- Hitting -----------------------------------------------------------------------------

        [Test]
        public void TheSweepHitsInFrontOnly()
        {
            var world = Arena(out _, new Vec3(0f, 0f, 2f), new Vec3(0f, 0f, -3.5f)); // behind: in reach, outside the arc
            Assert.That(NextAttack(world).Kind, Is.EqualTo(BossAttackKind.Sweep));

            var hits = Step(world, 40).OfType<PlayerDamaged>().ToList();

            Assert.That(hits.Select(h => h.Target), Is.EqualTo(new[] { P1 }), "the one in front, not the one behind");
            Assert.That(hits[0].Amount, Is.EqualTo(20));
        }

        [Test]
        public void TheSlamHitsEveryoneAroundIt()
        {
            var world = Arena(out _, new Vec3(-1.5f, 0f, 1.5f), new Vec3(1.5f, 0f, 1.5f), new Vec3(0f, 0f, 6f));
            Assert.That(NextAttack(world).Kind, Is.EqualTo(BossAttackKind.Slam));

            var hits = Step(world, 40).OfType<PlayerDamaged>().ToList();

            Assert.That(hits.Select(h => h.Target), Is.EquivalentTo(new[] { P1, P2 }), "both close ones; not the one 6 m away");
            Assert.That(hits.Select(h => h.Amount), Has.All.EqualTo(30));
        }

        [Test]
        public void TheChargeRunsAlongItsLineAndHitsEachPlayerInItOnce()
        {
            // It charges the nearest player out of sweeping reach (P2); P1 is further along the same line; P3 is off to the side.
            var world = Arena(out var boss, new Vec3(0f, 0f, 8f), new Vec3(0.3f, 0f, 6f), new Vec3(5f, 0f, 6f));
            Assert.That(NextAttack(world).Kind, Is.EqualTo(BossAttackKind.Charge));

            var hits = RunCharge(world, boss).OfType<PlayerDamaged>().ToList();

            Assert.That(hits.Select(h => h.Target), Is.EquivalentTo(new[] { P1, P2 }), "the target and the one in the way; not the one to the side");
            Assert.That(world.GetEnemy(boss).Position.Length, Is.EqualTo(10f).Within(0.05f), "it ran its full 10 m");
        }

        [Test]
        public void AChargeStopsAtAWall()
        {
            var gate = new GateDefinition { Id = "box", Name = "Box", Rooms = { new RoomDefinition { Width = 10f, Length = 12f } } };
            var world = new SimulationWorld(Config(gate));
            world.AddPlayer(P1, Vec3.Zero);
            world.StartGate("box");
            world.PlacePlayer(P1, new Vec3(0f, 0f, 9f));
            var boss = world.SpawnEnemy(Warlord(), new Vec3(0f, 0f, 3f)); // a 10 m charge would end at 13: past the wall at 12
            Assert.That(NextAttack(world).Kind, Is.EqualTo(BossAttackKind.Charge));
            world.PlacePlayer(P1, new Vec3(4f, 0f, 9f)); // stepped aside

            var ticks = 0;
            RunCharge(world, boss, t => ticks = t);

            Assert.That(world.GetEnemy(boss).Position.Z, Is.EqualTo(12f).Within(0.01f), "at the far wall, not through it");
            Assert.That(ticks, Is.LessThanOrEqualTo(56), "and the charge ended there (9 m at 10 m/s), not by running on into it");
        }

        [Test]
        public void ADodgeThroughABossAttackAvoidsIt()
        {
            var world = Arena(out _, new Vec3(0f, 0f, 2f));
            var attack = NextAttack(world);
            Step(world, attack.LandsAtTick - world.Tick - 2);

            var events = Step(world, 1, new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(1f, 0f, dodge: true) });
            events.AddRange(Step(world, 10));

            Assert.That(events.OfType<PlayerDamaged>(), Is.Empty);
            Assert.That(events.OfType<AttackDodged>().Count(), Is.EqualTo(1));
        }

        // --- Enrage ------------------------------------------------------------------------------

        [Test]
        public void AtHalfHealthItEnragesOnceCallsGoblinsAndAttacksFaster()
        {
            var world = Arena(out var boss, new Vec3(0f, 0f, 2f));
            var calm = NextAttack(world);
            Step(world, 100);

            HitBoss(world, boss, 510); // past half of 1000
            var events = Step(world, 3);

            Assert.That(events.OfType<BossEnraged>().Count(), Is.EqualTo(1));
            Assert.That(world.GetEnemy(boss).IsEnraged, Is.True);
            Assert.That(world.EnemyIds.Count(id => world.GetEnemy(id).Definition.Name == "Goblin"), Is.EqualTo(2), "two goblins join");

            var angry = NextAttack(world);
            Assert.That(angry.LandsAtTick - angry.Tick, Is.LessThan(calm.LandsAtTick - calm.Tick), "faster telegraphs");

            HitBoss(world, boss, 100);
            Assert.That(Step(world, 30).OfType<BossEnraged>(), Is.Empty, "only once");
        }

        /// <summary>Damage to the boss credited to P1, applied on the next tick (as a trap or scripted event would).</summary>
        private static void HitBoss(SimulationWorld world, EnemyId boss, int damage) => world.DamageEnemy(boss, P1, damage);

        private static readonly EnemyDefinition Killer = new EnemyDefinition
        {
            Name = "Killer", MaxHealth = 1, MoveSpeed = 0f, AttackDamage = 100000, AttackRange = 1f, AttackWindupSeconds = 0.05f, AttackCooldownSeconds = 0.05f,
        };

        /// <summary>Defeats a player where they stand with a one-hit enemy, then removes it.</summary>
        private static void Defeat(SimulationWorld world, PlayerId who)
        {
            var killer = world.SpawnEnemy(Killer, world.GetPlayer(who).Position);
            for (var i = 0; i < 60 && !world.GetPlayer(who).IsDefeated; i++) Step(world);
            world.DespawnEnemy(killer);
            Assert.That(world.GetPlayer(who).IsDefeated, Is.True, "test setup: the player should be down");
        }

        // --- In a gate ---------------------------------------------------------------------------

        private static readonly ModifierDefinition Ember = new ModifierDefinition { Id = "ember", Name = "Ember Core", AppliesToTag = "fire", DamagePercent = 10 };
        private static readonly ModifierDefinition Frost = new ModifierDefinition { Id = "frost", Name = "Frost Edge", AppliesToTag = "melee", DamagePercent = 10 };
        private static readonly ModifierDefinition Storm = new ModifierDefinition { Id = "storm", Name = "Storm Call", AppliesToTag = "area", DamagePercent = 10 };

        /// <summary>An entrance, a room with a goblin, and the boss chamber at the end.</summary>
        private static GateDefinition WarlordGate() => new GateDefinition
        {
            Id = "warlord-gate", Name = "Warlord Gate", WipeDelaySeconds = 0.5f,
            Rooms =
            {
                new RoomDefinition { Width = 10f, Length = 10f },
                new RoomDefinition { Width = 10f, Length = 10f, HasCheckpoint = true, Spawns = { new RoomSpawn(Add, Vec3.Zero) } },
                new RoomDefinition { Width = 16f, Length = 16f, Spawns = { new RoomSpawn(Warlord(health: 100), new Vec3(0f, 0f, 4f)) } },
            },
            RewardPool = { Ember, Frost, Storm },
        };

        private static SimulationWorld InWarlordGate(int players)
        {
            var world = new SimulationWorld(Config(WarlordGate()));
            for (var i = 1; i <= players; i++) world.AddPlayer(new PlayerId(i), Vec3.Zero);
            world.StartGate("warlord-gate");
            return world;
        }

        private static EnemyId EnterBossChamber(SimulationWorld world)
        {
            world.PlacePlayer(P1, new Vec3(0f, 0f, 22f));
            Step(world);
            return world.EnemyIds.Single(id => world.GetEnemy(id).Definition.Boss != null);
        }

        [Test]
        public void DefeatingTheBossCompletesTheGateEvenWithRoomsLeftBehind()
        {
            var world = InWarlordGate(players: 1);
            var boss = EnterBossChamber(world); // ran past the goblin room: the way is always open
            Assert.That(world.Gate.HasBoss, Is.True);

            HitBoss(world, boss, 100);
            var events = Step(world);

            Assert.That(world.Gate.IsCleared, Is.True);
            Assert.That(events.OfType<GateCleared>().Count(), Is.EqualTo(1));
            Assert.That(world.Gate.RoomState(1), Is.EqualTo(RoomState.NotEntered), "the skipped room does not matter");
        }

        [Test]
        public void EveryonePresentIsRewardedWithAModifierTheyDoNotHave()
        {
            var world = InWarlordGate(players: 2);
            world.GrantModifier(P2, Ember); // P2 already has one from the pool
            Step(world);
            var boss = EnterBossChamber(world);

            HitBoss(world, boss, 100);
            var events = Step(world);

            var rewards = events.OfType<GateRewardGranted>().ToList();
            Assert.That(rewards.Select(r => r.Player), Is.EquivalentTo(new[] { P1, P2 }));
            Assert.That(rewards.Select(r => r.Modifier.Id), Has.All.Matches<string>(id => id == "ember" || id == "frost" || id == "storm"),
                "from the pool (two players may draw the same one)");
            Assert.That(rewards.Single(r => r.Player == P2).Modifier.Id, Is.Not.EqualTo("ember"), "never one they already have");
            foreach (var reward in rewards)
            {
                Assert.That(world.GetPlayer(reward.Player).ActiveModifiers.Select(m => m.Id), Has.Member(reward.Modifier.Id), "kept on the character");
                Assert.That(events.OfType<ModifierGranted>().Any(g => g.Player == reward.Player && g.Modifier.Id == reward.Modifier.Id), Is.True,
                    "announced like any modifier, so the character is saved");
            }
        }

        [Test]
        public void TheRewardIsGivenOnce()
        {
            var world = InWarlordGate(players: 1);
            HitBoss(world, EnterBossChamber(world), 100);
            Step(world);

            Assert.That(Step(world, 120).OfType<GateRewardGranted>(), Is.Empty);
            Assert.That(world.GetPlayer(P1).ActiveModifiers.Count, Is.EqualTo(1));
        }

        [Test]
        public void AwayPlayersAreNotPresentForTheReward()
        {
            var world = InWarlordGate(players: 2);
            world.SetAway(P2, true);
            HitBoss(world, EnterBossChamber(world), 100);

            var rewards = Step(world).OfType<GateRewardGranted>().ToList();

            Assert.That(rewards.Select(r => r.Player), Is.EqualTo(new[] { P1 }));
        }

        [Test]
        public void RepeatedClearsWorkThroughThePoolBeforeAnythingRepeats()
        {
            var world = new SimulationWorld(Config(WarlordGate()));
            world.AddPlayer(P1, Vec3.Zero);
            var got = new List<string>();
            for (var run = 0; run < 4; run++)
            {
                world.StartGate("warlord-gate");
                HitBoss(world, EnterBossChamber(world), 100);
                var reward = Step(world).OfType<GateRewardGranted>().Single();
                got.Add(reward.Modifier?.Id);
            }

            Assert.That(got.Take(3), Is.EquivalentTo(new[] { "ember", "frost", "storm" }), "the whole pool, no repeats");
            Assert.That(got[3], Is.Null, "the pool is owned: nothing more (duplicates arrive with loot)");
        }

        [Test]
        public void TheDrawIsTheSameOnEveryMachine()
        {
            // Guests predict the clear tick themselves; the host's draw must match theirs.
            var a = InWarlordGate(players: 2);
            var b = SimulationWorld.FromSnapshot(Config(WarlordGate()), a.CaptureSnapshot());
            foreach (var world in new[] { a, b }) HitBoss(world, EnterBossChamber(world), 100);

            var fromA = Step(a).OfType<GateRewardGranted>().Select(r => (r.Player, r.Modifier.Id)).ToList();
            var fromB = Step(b).OfType<GateRewardGranted>().Select(r => (r.Player, r.Modifier.Id)).ToList();

            Assert.That(fromB, Is.EqualTo(fromA));
        }

        [Test]
        public void AWipeInTheBossChamberBringsTheBossBackFresh()
        {
            var world = InWarlordGate(players: 2);
            world.PlacePlayer(P1, new Vec3(0f, 0f, 15f)); // the checkpoint room
            Step(world);
            var boss = EnterBossChamber(world);
            HitBoss(world, boss, 60); // enraged, adds out
            Step(world, 3);
            Assert.That(world.GetEnemy(boss).IsEnraged, Is.True);

            world.PlacePlayer(P2, new Vec3(-6f, 0f, 30f)); // well away from each other and the boss
            world.PlacePlayer(P1, new Vec3(6f, 0f, 30f));
            Defeat(world, P1);
            Defeat(world, P2);
            Step(world, 60);

            // (The party is back at the checkpoint in the goblin room, which has refilled as it should.)
            Assert.That(world.EnemyIds.Where(id => !world.GetEnemy(id).IsDefeated && world.GetEnemy(id).Position.Z >= 20f), Is.Empty,
                "the boss chamber is empty until the party returns: its goblins went with the boss");
            Assert.That(world.EnemyIds.Count(id => world.GetEnemy(id).Definition.Boss != null), Is.Zero, "boss and its goblins are gone");
            var again = EnterBossChamber(world);
            Assert.That(world.GetEnemy(again).Health, Is.EqualTo(100));
            Assert.That(world.GetEnemy(again).IsEnraged, Is.False);
        }

        // --- Edges found in review ------------------------------------------------------------------

        [Test]
        public void AChargeIsNotStartedAtSomeoneBehindIt()
        {
            // It turns slowly; a charge would snap it round. It turns towards them first.
            var world = Arena(out var boss, new Vec3(0f, 0f, 8f));
            Assert.That(NextAttack(world).Kind, Is.EqualTo(BossAttackKind.Charge));
            RunCharge(world, boss);
            world.PlacePlayer(P1, world.GetEnemy(boss).Position + new Vec3(0f, 0f, -8f)); // now far behind it

            var next = NextAttack(world, maxTicks: 600);

            var state = world.GetEnemy(boss);
            var toPlayer = world.GetPlayer(P1).Position - state.AttackOrigin;
            Assert.That(next.Direction.X * toPlayer.X + next.Direction.Z * toPlayer.Z, Is.GreaterThan(0f), "aimed at them, once it had turned to face them");
            Assert.That(state.Facing.X * toPlayer.X + state.Facing.Z * toPlayer.Z, Is.GreaterThan(0f), "they are in front of it, not behind");
        }

        [Test]
        public void AChargeTowardsAWallIsMarkedOnlyAsFarAsTheWall()
        {
            var gate = new GateDefinition { Id = "box", Name = "Box", Rooms = { new RoomDefinition { Width = 10f, Length = 12f } } };
            var world = new SimulationWorld(Config(gate));
            world.AddPlayer(P1, Vec3.Zero);
            world.StartGate("box");
            world.PlacePlayer(P1, new Vec3(0f, 0f, 9f));
            var boss = world.SpawnEnemy(Warlord(), new Vec3(0f, 0f, 3f));
            Assert.That(NextAttack(world).Kind, Is.EqualTo(BossAttackKind.Charge));

            var state = world.GetEnemy(boss);
            Assert.That(state.AttackReach, Is.EqualTo(9f).Within(0.3f), "from 3 m to the wall at 12 m, not the full 10 m");
            Assert.That(state.CurrentBossAttack.Covers(state.AttackOrigin, state.AttackDirection, new Vec3(0f, 0f, 11.5f), state.AttackReach), Is.True);
        }

        [Test]
        public void TheChargesLaneStaysMarkedWhileItRuns()
        {
            var world = Arena(out var boss, new Vec3(0f, 0f, 8f));
            var attack = NextAttack(world);
            Step(world, attack.LandsAtTick - world.Tick + 5);

            var state = world.GetEnemy(boss);
            Assert.That(state.IsCharging, Is.True);
            Assert.That(state.CurrentBossAttack, Is.Not.Null, "so it can still be drawn");
            Assert.That(state.AttackLandsAtTick, Is.Not.Zero);
        }

        [Test]
        public void ABossKilledInTheTickItEnragesCallsNoOne()
        {
            var world = Arena(out var boss, new Vec3(0f, 0f, 2f));
            HitBoss(world, boss, 510); // crosses half...
            HitBoss(world, boss, 600); // ...and dies, in the same tick

            var events = Step(world, 3);

            Assert.That(world.GetEnemy(boss).IsDefeated, Is.True);
            Assert.That(events.OfType<BossEnraged>(), Is.Empty);
            Assert.That(world.EnemyIds.Count(id => world.GetEnemy(id).Definition.Name == "Goblin"), Is.Zero);
        }

        [Test]
        public void ADefeatedBossStopsItsAttack()
        {
            var world = Arena(out var boss, new Vec3(0f, 0f, 2f));
            var attack = NextAttack(world);
            HitBoss(world, boss, 100000);

            var events = Step(world, attack.LandsAtTick - world.Tick + 10);

            Assert.That(events.OfType<PlayerDamaged>(), Is.Empty, "its sweep never lands");
            Assert.That(world.GetEnemy(boss).CurrentBossAttack, Is.Null, "and nothing is left marked");
        }

        [Test]
        public void WhenTheBossFallsItsGoblinsGoWithItAndItsRoomIsCleared()
        {
            var world = InWarlordGate(players: 1);
            var boss = EnterBossChamber(world);
            HitBoss(world, boss, 60); // enraged: goblins join
            Step(world, 3);
            Assert.That(world.EnemyIds.Count(id => world.GetEnemy(id).Definition.Name == "Goblin" && !world.GetEnemy(id).IsDefeated), Is.EqualTo(2));

            HitBoss(world, boss, 100);
            Step(world);

            Assert.That(world.Gate.IsCleared, Is.True);
            Assert.That(world.EnemyIds.Where(id => world.GetEnemy(id).Position.Z >= 20f && !world.GetEnemy(id).IsDefeated), Is.Empty,
                "nothing in the chamber fights on");
            Assert.That(world.Gate.RoomState(2), Is.EqualTo(RoomState.Cleared));
        }

        [Test]
        public void AWipeAfterTheClearDoesNotBringTheBossBack()
        {
            var world = InWarlordGate(players: 2);
            world.PlacePlayer(P2, new Vec3(0f, 0f, 15f)); // the goblin room, left unfinished
            Step(world);
            HitBoss(world, EnterBossChamber(world), 100);
            Step(world);
            Assert.That(world.Gate.IsCleared, Is.True);

            world.PlacePlayer(P1, new Vec3(0f, 0f, 12f));
            Defeat(world, P1);
            Defeat(world, P2);
            Step(world, 60);

            Assert.That(world.Gate.RoomState(2), Is.EqualTo(RoomState.Cleared));
            Assert.That(world.EnemyIds.Count(id => world.GetEnemy(id).IsBoss && !world.GetEnemy(id).IsDefeated), Is.Zero);
            Assert.That(world.Gate.IsCleared, Is.True);
        }

        [Test]
        public void AfterTheClearASoloDeathCostsNothingAndNeverRewardsTwice()
        {
            var world = InWarlordGate(players: 1);
            HitBoss(world, EnterBossChamber(world), 100);
            Step(world);
            var events = new List<SimulationEvent>();
            for (var i = 0; i < 5; i++) // more than three: after the clear there is no limit and no restart
            {
                Defeat(world, P1);
                events.AddRange(Step(world, 1, new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(0f, 0f, respawn: true) }));
            }

            Assert.That(world.GetPlayer(P1).IsDefeated, Is.False);
            Assert.That(events.OfType<GateRestarted>(), Is.Empty);
            Assert.That(events.OfType<GateRewardGranted>(), Is.Empty);
            Assert.That(world.Gate.Attempt, Is.EqualTo(1));
            Assert.That(world.Gate.RevivesLeft(P1), Is.EqualTo(3), "none were used");
        }

        [Test]
        public void AChargeWithNoSpeedEndsAtOnce()
        {
            var boss = Warlord();
            boss.Boss.Attacks.Single(a => a.Kind == BossAttackKind.Charge).Speed = 0f;
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, new Vec3(0f, 0f, 8f));
            var id = world.SpawnEnemy(boss, Vec3.Zero);
            var attack = NextAttack(world);
            Assert.That(attack.Kind, Is.EqualTo(BossAttackKind.Charge));

            Step(world, attack.LandsAtTick - world.Tick + 2);

            Assert.That(world.GetEnemy(id).IsCharging, Is.False, "it ended where it stood");
            Assert.That(world.GetEnemy(id).CurrentBossAttack, Is.Null);
        }

        [Test]
        public void AddsCalledAfterARestoreShareTheHostsDefinition()
        {
            // A guest restores a snapshot, then the boss enrages on both: their next snapshots must match.
            var host = Arena(out var boss, new Vec3(0f, 0f, 2f));
            Step(host, 5);
            var guest = SimulationWorld.FromSnapshot(Config(), host.CaptureSnapshot());
            foreach (var world in new[] { host, guest })
            {
                HitBoss(world, boss, 510);
                Step(world, 5);
            }

            Assert.That(guest.CaptureSnapshot(), Is.EqualTo(host.CaptureSnapshot()));
        }

        // --- Snapshots ---------------------------------------------------------------------------

        [Test]
        public void ABossMidChargeSurvivesASnapshotAndCarriesOnTheSame()
        {
            var world = Arena(out var boss, new Vec3(0f, 0f, 8f), new Vec3(0.5f, 0f, 4f));
            var attack = NextAttack(world);
            Assert.That(attack.Kind, Is.EqualTo(BossAttackKind.Charge));
            Step(world, attack.LandsAtTick - world.Tick + 3); // charging now
            HitBoss(world, boss, 600); // enraged mid-charge
            Step(world);
            Assert.That(world.GetEnemy(boss).IsCharging, Is.True);

            var config = Config();
            var copy = SimulationWorld.FromSnapshot(config, world.CaptureSnapshot());
            var original = Step(world, 200).Select(e => e.GetType().Name + e.Tick).ToList();
            var restored = Step(copy, 200).Select(e => e.GetType().Name + e.Tick).ToList();

            Assert.That(restored, Is.EqualTo(original));
            Assert.That(copy.CaptureSnapshot(), Is.EqualTo(world.CaptureSnapshot()));
        }
    }
}
