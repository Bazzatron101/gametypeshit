using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    public class SimulationAimAndHeavyTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);

        // Heavy at 60 Hz: windup 21 ticks, cooldown 60 ticks.
        private const int HeavyWindup = 21;
        private const int HeavyCooldown = 60;

        /// <summary>
        /// A narrow 30 degree light attack, so a target 40 degrees off the facing is missed
        /// unless soft lock-on turns the swing toward it.
        /// </summary>
        private static SimulationConfig Config(bool softLock = true) => new SimulationConfig
        {
            TickRate = 60,
            PlayerMoveSpeed = 5f,
            LightAttack = new AttackDefinition { Damage = 10, Range = 2f, ArcDegrees = 30f, CooldownSeconds = 0.4f },
            HeavyAttack = new AttackDefinition { Damage = 25, Range = 2.5f, ArcDegrees = 90f, WindupSeconds = 0.35f, CooldownSeconds = 1f },
            SoftLockOn = new SoftLockOnDefinition { Enabled = softLock, Range = 4f, ConeDegrees = 90f },
        };

        /// <summary>Stands still and never attacks back, so tests see only the player's actions.</summary>
        private static EnemyDefinition Dummy(int health = 100) => new EnemyDefinition
        {
            Name = "Dummy", MaxHealth = health, MoveSpeed = 0f, StopDistance = 1f, AttackDamage = 0, AttackRange = 0f,
        };

        private static readonly Dictionary<PlayerId, PlayerCommand> NoInput = new Dictionary<PlayerId, PlayerCommand>();

        private static Dictionary<PlayerId, PlayerCommand> Cmd(PlayerCommand cmd) =>
            new Dictionary<PlayerId, PlayerCommand> { [P1] = cmd };

        private static readonly PlayerCommand Light = new PlayerCommand(0f, 0f, lightAttack: true);
        private static readonly PlayerCommand Heavy = new PlayerCommand(0f, 0f, heavyAttack: true);

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

        private static SimulationWorld World(SimulationConfig config = null)
        {
            var sim = new SimulationWorld(config ?? Config());
            sim.AddPlayer(P1, Vec3.Zero); // faces +Z
            return sim;
        }

        private static float AngleFromPlusZ(Vec3 facing) => MathF.Atan2(facing.X, facing.Z) * 180f / MathF.PI;

        // --- Soft lock-on -----------------------------------------------------------------

        [Test]
        public void AttackTurnsTowardAnEnemyInsideTheAssistCone()
        {
            var sim = World();
            var enemy = sim.SpawnEnemy(Dummy(), new Vec3(1f, 0f, 1.2f)); // 40 degrees right of facing

            RunTo(sim, 1, _ => Cmd(Light));

            Assert.That(sim.GetEnemy(enemy).Health, Is.EqualTo(90), "the narrow swing connected");
            Assert.That(AngleFromPlusZ(sim.GetPlayer(P1).Facing), Is.EqualTo(39.8f).Within(0.1f), "player turned to the target");
        }

        [Test]
        public void WithoutSoftLockTheSameSwingMisses()
        {
            var sim = World(Config(softLock: false));
            var enemy = sim.SpawnEnemy(Dummy(), new Vec3(1f, 0f, 1.2f));

            RunTo(sim, 1, _ => Cmd(Light));

            Assert.That(sim.GetEnemy(enemy).Health, Is.EqualTo(100));
            Assert.That(AngleFromPlusZ(sim.GetPlayer(P1).Facing), Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void EnemyOutsideTheAssistConeIsNotAssisted()
        {
            var sim = World();
            var enemy = sim.SpawnEnemy(Dummy(), new Vec3(1.5f, 0f, 0.5f)); // about 72 degrees off

            RunTo(sim, 1, _ => Cmd(Light));

            Assert.That(sim.GetEnemy(enemy).Health, Is.EqualTo(100));
            Assert.That(AngleFromPlusZ(sim.GetPlayer(P1).Facing), Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void EnemyBeyondAssistRangeIsNotAssisted()
        {
            var sim = World();
            sim.SpawnEnemy(Dummy(), new Vec3(3f, 0f, 3.6f)); // in the cone but about 4.7 m away

            RunTo(sim, 1, _ => Cmd(Light));

            Assert.That(AngleFromPlusZ(sim.GetPlayer(P1).Facing), Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void AssistPicksTheNearestValidEnemyNotTheBestAligned()
        {
            var sim = World();
            sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 1.9f));       // dead ahead, 1.9 m
            var near = sim.SpawnEnemy(Dummy(), new Vec3(1f, 0f, 1.2f)); // 40 degrees off, 1.56 m

            RunTo(sim, 1, _ => Cmd(Light));

            Assert.That(sim.GetEnemy(near).Health, Is.EqualTo(90));
            Assert.That(AngleFromPlusZ(sim.GetPlayer(P1).Facing), Is.EqualTo(39.8f).Within(0.1f));
        }

        [Test]
        public void AssistIgnoresEnemiesTheAttackCannotReach()
        {
            // A wide 120 degree swing would hit the reachable enemy 55 degrees right as things stand.
            // Turning toward the out-of-reach enemy 40 degrees left would swing away from it and whiff.
            var config = Config();
            config.LightAttack.ArcDegrees = 120f;
            var sim = World(config);
            sim.SpawnEnemy(Dummy(), new Vec3(-1.93f, 0f, 2.3f));                // 40 degrees left, 3 m: in lock-on range, beyond reach
            var reachable = sim.SpawnEnemy(Dummy(), new Vec3(1.23f, 0f, 0.86f)); // 55 degrees right, 1.5 m: outside the assist cone

            RunTo(sim, 1, _ => Cmd(Light));

            Assert.That(sim.GetEnemy(reachable).Health, Is.EqualTo(90), "the swing was not dragged toward the far enemy");
            Assert.That(AngleFromPlusZ(sim.GetPlayer(P1).Facing), Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void DefeatedEnemiesAreNotValidTargets()
        {
            var sim = World();
            var corpse = sim.SpawnEnemy(Dummy(health: 10), new Vec3(0f, 0f, 1.2f));
            var other = sim.SpawnEnemy(Dummy(), new Vec3(-1f, 0f, 1.6f)); // 32 degrees left, farther
            RunTo(sim, 1, _ => Cmd(Light));
            Assert.That(sim.GetEnemy(corpse).IsDefeated, Is.True);

            RunTo(sim, 30, t => t == 30 ? Cmd(Light) : NoInput);

            Assert.That(sim.GetEnemy(other).Health, Is.EqualTo(90), "assist skipped the corpse");
        }

        [Test]
        public void HoldingADirectionAwayFromTheEnemyOverridesTheAssist()
        {
            var sim = World();
            var enemy = sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 1.2f)); // straight ahead

            RunTo(sim, 1, _ => Cmd(new PlayerCommand(-1f, 0f, lightAttack: true))); // swing left instead

            Assert.That(sim.GetEnemy(enemy).Health, Is.EqualTo(100));
            Assert.That(sim.GetPlayer(P1).Facing.X, Is.EqualTo(-1f).Within(1e-3f));
        }

        // --- Heavy attack -----------------------------------------------------------------

        [Test]
        public void HeavyAttackLandsAfterItsWindupWithItsOwnDamage()
        {
            var sim = World();
            var enemy = sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2.2f)); // beyond light reach, inside heavy reach

            var events = RunTo(sim, HeavyWindup, t => t == 1 ? Cmd(Heavy) : NoInput);
            Assert.That(sim.GetEnemy(enemy).Health, Is.EqualTo(100), "nothing lands during the windup");
            var windup = events.OfType<AttackWindupStarted>().Single();
            Assert.That((windup.Player, windup.Kind, windup.Tick, windup.LandsAtTick), Is.EqualTo((P1, AttackKind.Heavy, 1, 1 + HeavyWindup)));
            Assert.That(sim.GetPlayer(P1).IsWindingUp, Is.True);

            events = RunTo(sim, 1 + HeavyWindup);

            Assert.That(sim.GetEnemy(enemy).Health, Is.EqualTo(75));
            Assert.That(events.OfType<AttackPerformed>().Single().Kind, Is.EqualTo(AttackKind.Heavy));
            Assert.That(events.OfType<DamageDealt>().Single().Amount, Is.EqualTo(25));
            Assert.That(sim.GetPlayer(P1).IsWindingUp, Is.False);
        }

        [Test]
        public void LightAttacksAreReportedAsLight()
        {
            var sim = World();

            var events = RunTo(sim, 1, _ => Cmd(Light));

            Assert.That(events.OfType<AttackPerformed>().Single().Kind, Is.EqualTo(AttackKind.Light));
            Assert.That(events.OfType<AttackWindupStarted>(), Is.Empty, "light attacks have no windup");
        }

        [Test]
        public void PlayerIsCommittedDuringTheHeavyWindup()
        {
            var sim = World();

            var events = RunTo(sim, HeavyWindup, t => t == 1
                ? Cmd(Heavy)
                : Cmd(new PlayerCommand(1f, 0f, lightAttack: true)));

            Assert.That(sim.GetPlayer(P1).Position, Is.EqualTo(Vec3.Zero), "no walking while winding up");
            Assert.That(events.OfType<AttackPerformed>(), Is.Empty, "no light attacks while winding up");
        }

        [Test]
        public void DodgeCancelsTheHeavyWindup()
        {
            var sim = World();
            var enemy = sim.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2.2f));

            var events = RunTo(sim, 40, t => t == 1 ? Cmd(Heavy) : t == 10 ? Cmd(new PlayerCommand(1f, 0f, dodge: true)) : NoInput);

            Assert.That(events.OfType<AttackPerformed>(), Is.Empty);
            Assert.That(sim.GetEnemy(enemy).Health, Is.EqualTo(100));
            Assert.That(events.OfType<DodgePerformed>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void HeavyAttackHasItsOwnCooldown()
        {
            var sim = World();

            var events = RunTo(sim, HeavyCooldown, _ => Cmd(Heavy));
            Assert.That(events.OfType<AttackWindupStarted>().Count(), Is.EqualTo(1));

            events = RunTo(sim, HeavyCooldown + 1, _ => Cmd(Heavy));
            Assert.That(events.OfType<AttackWindupStarted>().Count(), Is.EqualTo(1), "ready again a second after the first");
        }

        [Test]
        public void HeavyAttackUsesSoftLockOnWhenItStarts()
        {
            var config = Config();
            config.HeavyAttack.ArcDegrees = 30f; // narrow, so only the nudge can make it connect
            var sim = World(config);
            var enemy = sim.SpawnEnemy(Dummy(), new Vec3(1.2f, 0f, 1.5f)); // about 39 degrees right

            RunTo(sim, 1 + HeavyWindup, t => t == 1 ? Cmd(Heavy) : NoInput);

            Assert.That(sim.GetEnemy(enemy).Health, Is.EqualTo(75));
        }
    }
}
