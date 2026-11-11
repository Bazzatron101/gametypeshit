using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    public class SimulationDefenseTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);
        private static readonly PlayerId P2 = new PlayerId(2);

        // At 60 Hz: windup 30 ticks, enemy cooldown 90, dodge lasts 15, invulnerable 18, dodge cooldown 48.
        private const int Windup = 30;
        private const int Invulnerable = 18;
        private const int DodgeCooldown = 48;
        private const int EnemyCooldown = 90;

        private static SimulationConfig Config(float dodgeDistance = 0f) => new SimulationConfig
        {
            TickRate = 60,
            PlayerMoveSpeed = 5f,
            PlayerMaxHealth = 100,
            // Distance 0 by default so tests measure invulnerability, not the dash carrying the player out of reach.
            Dodge = new DodgeDefinition { Distance = dodgeDistance, DurationSeconds = 0.25f, InvulnerabilitySeconds = 0.3f, CooldownSeconds = 0.8f },
        };

        private static EnemyDefinition Goblin(int damage = 10, int maxHealth = 30) => new EnemyDefinition
        {
            Name = "Goblin", MaxHealth = maxHealth, MoveSpeed = 3f, StopDistance = 1.2f,
            AttackDamage = damage, AttackRange = 1.6f, AttackWindupSeconds = 0.5f, AttackCooldownSeconds = 1.5f,
        };

        private static readonly Dictionary<PlayerId, PlayerCommand> NoInput = new Dictionary<PlayerId, PlayerCommand>();

        private static Dictionary<PlayerId, PlayerCommand> Cmd(PlayerId id, PlayerCommand cmd) =>
            new Dictionary<PlayerId, PlayerCommand> { [id] = cmd };

        private static readonly PlayerCommand DodgeNow = new PlayerCommand(0f, 0f, dodge: true);

        /// <summary>Steps until <see cref="SimulationWorld.Tick"/> reaches <paramref name="lastTick"/>, returning all events.</summary>
        private static List<SimulationEvent> RunTo(SimulationWorld sim, int lastTick, Func<int, Dictionary<PlayerId, PlayerCommand>> commandsForTick = null)
        {
            var events = new List<SimulationEvent>();
            while (sim.Tick < lastTick)
            {
                sim.Step(commandsForTick?.Invoke(sim.Tick + 1) ?? NoInput);
                events.AddRange(sim.Events);
            }
            return events;
        }

        /// <summary>A player with a Goblin already in reach. The Goblin starts its attack on tick 1, so it lands on tick 31.</summary>
        private static SimulationWorld Duel(out EnemyId goblin, SimulationConfig config = null, EnemyDefinition definition = null)
        {
            var sim = new SimulationWorld(config ?? Config());
            sim.AddPlayer(P1, Vec3.Zero);
            goblin = sim.SpawnEnemy(definition ?? Goblin(), new Vec3(0f, 0f, 1.2f));
            return sim;
        }

        private const int FirstHitTick = 1 + Windup;

        [Test]
        public void EnemyWindsUpThenItsAttackReducesPlayerHealthAndEmitsADamageEvent()
        {
            var sim = Duel(out var goblin);

            var events = RunTo(sim, FirstHitTick - 1);
            Assert.That(sim.GetPlayer(P1).Health, Is.EqualTo(100), "nothing lands during the windup");
            var started = events.OfType<EnemyAttackStarted>().Single();
            Assert.That((started.Enemy, started.Target, started.Tick, started.LandsAtTick), Is.EqualTo((goblin, P1, 1, FirstHitTick)));

            events = RunTo(sim, FirstHitTick);

            Assert.That(sim.GetPlayer(P1).Health, Is.EqualTo(90));
            var hit = events.OfType<PlayerDamaged>().Single();
            Assert.That((hit.Attacker, hit.Target, hit.Amount, hit.RemainingHealth, hit.Tick), Is.EqualTo((goblin, P1, 10, 90, FirstHitTick)));
        }

        [Test]
        public void EnemyAttacksAgainAfterItsCooldown()
        {
            var sim = Duel(out _);

            var events = RunTo(sim, FirstHitTick + EnemyCooldown + Windup);

            Assert.That(events.OfType<PlayerDamaged>().Select(e => e.Tick), Is.EqualTo(new[] { FirstHitTick, FirstHitTick + EnemyCooldown + Windup }));
        }

        [Test]
        public void AttackMissesIfThePlayerLeavesReachDuringTheWindup()
        {
            var sim = Duel(out _);

            var events = RunTo(sim, FirstHitTick, _ => Cmd(P1, new PlayerCommand(0f, -1f))); // walk away at 5 m/s

            Assert.That(sim.GetPlayer(P1).Health, Is.EqualTo(100));
            Assert.That(events.OfType<PlayerDamaged>(), Is.Empty);
        }

        [TestCase(FirstHitTick - Invulnerable + 1, TestName = "Dodge whose window ends exactly on the hit negates it")]
        [TestCase(FirstHitTick - 5, TestName = "Dodge shortly before the hit negates it")]
        [TestCase(FirstHitTick, TestName = "Dodge on the hit tick itself negates it")]
        public void DodgeInsideTheWindowNegatesDamage(int dodgeTick)
        {
            var sim = Duel(out var goblin);

            var events = RunTo(sim, FirstHitTick, t => t == dodgeTick ? Cmd(P1, DodgeNow) : NoInput);

            Assert.That(sim.GetPlayer(P1).Health, Is.EqualTo(100));
            Assert.That(events.OfType<PlayerDamaged>(), Is.Empty);
            var dodged = events.OfType<AttackDodged>().Single();
            Assert.That((dodged.Attacker, dodged.Player, dodged.Tick), Is.EqualTo((goblin, P1, FirstHitTick)));
        }

        [TestCase(FirstHitTick - Invulnerable, TestName = "Dodge whose window ends one tick before the hit does not negate it")]
        [TestCase(5, TestName = "Dodge far too early does not negate the hit")]
        [TestCase(FirstHitTick + 1, TestName = "Dodge after the hit is too late")]
        public void DodgeOutsideTheWindowDoesNotNegateDamage(int dodgeTick)
        {
            var sim = Duel(out _);

            var events = RunTo(sim, FirstHitTick + 1, t => t == dodgeTick ? Cmd(P1, DodgeNow) : NoInput);

            Assert.That(sim.GetPlayer(P1).Health, Is.EqualTo(90));
            Assert.That(events.OfType<PlayerDamaged>().Count(), Is.EqualTo(1));
            Assert.That(events.OfType<AttackDodged>(), Is.Empty);
        }

        [Test]
        public void PlayerIsInvulnerableExactlyDuringTheWindow()
        {
            var sim = new SimulationWorld(Config());
            sim.AddPlayer(P1, Vec3.Zero);

            sim.Step(Cmd(P1, DodgeNow)); // tick 1
            var invulnerableTicks = new List<int>();
            if (sim.GetPlayer(P1).IsInvulnerable) invulnerableTicks.Add(sim.Tick);
            while (sim.Tick < 40)
            {
                sim.Step(NoInput);
                if (sim.GetPlayer(P1).IsInvulnerable) invulnerableTicks.Add(sim.Tick);
            }

            Assert.That(invulnerableTicks, Is.EqualTo(Enumerable.Range(1, Invulnerable)));
        }

        [Test]
        public void DodgeDashesInTheMoveDirection()
        {
            var sim = new SimulationWorld(Config(dodgeDistance: 3f));
            sim.AddPlayer(P1, Vec3.Zero);

            var events = RunTo(sim, 20, t => t == 1 ? Cmd(P1, new PlayerCommand(1f, 0f, dodge: true)) : NoInput);

            Assert.That(sim.GetPlayer(P1).Position.X, Is.EqualTo(3f).Within(1e-3f));
            Assert.That(sim.GetPlayer(P1).Position.Z, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(events.OfType<DodgePerformed>().Single().Player, Is.EqualTo(P1));
        }

        [Test]
        public void DodgeWithoutMoveInputDashesTheWayThePlayerFaces()
        {
            var sim = new SimulationWorld(Config(dodgeDistance: 3f));
            sim.AddPlayer(P1, Vec3.Zero); // faces +Z

            RunTo(sim, 20, t => t == 1 ? Cmd(P1, DodgeNow) : NoInput);

            Assert.That(sim.GetPlayer(P1).Position.Z, Is.EqualTo(3f).Within(1e-3f));
        }

        [Test]
        public void PlayerCannotSteerOrAttackMidDodge()
        {
            var sim = new SimulationWorld(Config(dodgeDistance: 3f));
            sim.AddPlayer(P1, Vec3.Zero);

            var events = RunTo(sim, 20, t => t == 1
                ? Cmd(P1, new PlayerCommand(1f, 0f, dodge: true))
                : Cmd(P1, new PlayerCommand(0f, 1f, lightAttack: t == 5)));

            Assert.That(events.OfType<AttackPerformed>(), Is.Empty, "attack pressed mid-dodge");
            const int lastDashTick = 15; // the dash runs ticks 1..15; steering resumes on 16..20
            var steered = (20 - lastDashTick) * 5f / 60f;
            Assert.That(sim.GetPlayer(P1).Position.X, Is.EqualTo(3f).Within(1e-3f));
            Assert.That(sim.GetPlayer(P1).Position.Z, Is.EqualTo(steered).Within(1e-3f));
        }

        [Test]
        public void DodgeDuringItsCooldownDoesNothing()
        {
            var sim = new SimulationWorld(Config());
            sim.AddPlayer(P1, Vec3.Zero);

            var events = RunTo(sim, DodgeCooldown, _ => Cmd(P1, DodgeNow));
            Assert.That(events.OfType<DodgePerformed>().Count(), Is.EqualTo(1));

            events = RunTo(sim, DodgeCooldown + 1, _ => Cmd(P1, DodgeNow));
            Assert.That(events.OfType<DodgePerformed>().Count(), Is.EqualTo(1), "ready again once the cooldown has passed");
        }

        [Test]
        public void PlayerIsDefeatedWhenHealthReachesZero()
        {
            var sim = Duel(out var goblin, definition: Goblin(damage: 60));

            var events = RunTo(sim, FirstHitTick + EnemyCooldown + Windup);

            var state = sim.GetPlayer(P1);
            Assert.That(state.LifeState, Is.EqualTo(PlayerLifeState.Defeated));
            Assert.That(state.Health, Is.EqualTo(0), "never below zero");
            Assert.That(events.OfType<PlayerDamaged>().Select(e => e.Amount), Is.EqualTo(new[] { 60, 40 }));
            var defeated = events.OfType<PlayerDefeated>().Single();
            Assert.That((defeated.Player, defeated.DefeatedBy, defeated.Tick), Is.EqualTo((P1, goblin, FirstHitTick + EnemyCooldown + Windup)));
        }

        [Test]
        public void DefeatedPlayerIgnoresCommands()
        {
            var sim = Duel(out _, definition: Goblin(damage: 100));
            RunTo(sim, FirstHitTick);
            var startPosition = sim.GetPlayer(P1).Position;

            var events = RunTo(sim, FirstHitTick + 60, _ => Cmd(P1, new PlayerCommand(1f, 0f, lightAttack: true, dodge: true)));

            Assert.That(sim.GetPlayer(P1).Position, Is.EqualTo(startPosition));
            Assert.That(events.OfType<AttackPerformed>(), Is.Empty);
            Assert.That(events.OfType<DodgePerformed>(), Is.Empty);
        }

        [Test]
        public void EnemiesIgnoreDefeatedPlayersAndGoAfterTheRest()
        {
            var sim = new SimulationWorld(Config());
            sim.AddPlayer(P1, Vec3.Zero);
            sim.AddPlayer(P2, new Vec3(10f, 0f, 0f));
            var goblin = sim.SpawnEnemy(Goblin(damage: 100), new Vec3(0f, 0f, 1.2f));
            RunTo(sim, FirstHitTick);
            Assert.That(sim.GetPlayer(P1).LifeState, Is.EqualTo(PlayerLifeState.Defeated));

            var events = RunTo(sim, FirstHitTick + 60);

            Assert.That(events.OfType<EnemyAttackStarted>(), Is.Empty, "no attacks on the defeated player");
            Assert.That(sim.GetEnemy(goblin).Position.X, Is.GreaterThan(2f), "heading for P2");
        }

        [Test]
        public void EnemyWithNoLivePlayersStandsStill()
        {
            var sim = Duel(out var goblin, definition: Goblin(damage: 100));
            RunTo(sim, FirstHitTick);
            var startPosition = sim.GetEnemy(goblin).Position;

            var events = RunTo(sim, FirstHitTick + 200);

            Assert.That(sim.GetEnemy(goblin).Position, Is.EqualTo(startPosition));
            Assert.That(events, Is.Empty);
        }

        [Test]
        public void DefeatedEnemyDoesNotFinishItsAttack()
        {
            var sim = Duel(out var goblin, definition: Goblin(maxHealth: 10));

            var events = RunTo(sim, FirstHitTick, t => t == 2 ? Cmd(P1, new PlayerCommand(0f, 0f, lightAttack: true)) : NoInput);

            Assert.That(sim.GetEnemy(goblin).IsDefeated, Is.True);
            Assert.That(sim.GetPlayer(P1).Health, Is.EqualTo(100));
            Assert.That(events.OfType<PlayerDamaged>(), Is.Empty);
        }

        [Test]
        public void ZeroDamageHitEmitsNoDamageEvent()
        {
            var sim = Duel(out _, definition: Goblin(damage: 0));

            var events = RunTo(sim, FirstHitTick);

            Assert.That(sim.GetPlayer(P1).Health, Is.EqualTo(100));
            Assert.That(events.OfType<PlayerDamaged>(), Is.Empty);
        }

        [Test]
        public void DefeatedPlayerIsNeitherDodgingNorInvulnerable()
        {
            // Invulnerability shorter than the dash, so the killing hit can land mid-dash.
            var config = Config(dodgeDistance: 0f);
            config.Dodge.InvulnerabilitySeconds = 0f;
            config.Dodge.DurationSeconds = 1f;
            var sim = Duel(out _, config, Goblin(damage: 100));

            RunTo(sim, FirstHitTick, t => t == FirstHitTick - 5 ? Cmd(P1, DodgeNow) : NoInput);

            var state = sim.GetPlayer(P1);
            Assert.That(state.IsDefeated, Is.True);
            Assert.That(state.IsDodging, Is.False);
            Assert.That(state.IsInvulnerable, Is.False);
        }
    }
}
