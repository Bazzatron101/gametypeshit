using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// A snapshot is the whole simulation state at a tick boundary. The host sends it to a joining
    /// player (late join) and to clients correcting their prediction, so a restored world must carry
    /// on exactly as the original would.
    /// </summary>
    public class SimulationSnapshotTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);
        private static readonly PlayerId P2 = new PlayerId(2);

        private static SimulationConfig Config() => new SimulationConfig
        {
            TickRate = 60,
            ChargeTapGraceSeconds = 0f,
            Progression = new ProgressionDefinition
            {
                LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 }, FreePointsPerLevel = 3, StartingStatValue = 10,
            },
            Skills = new[]
            {
                new SkillDefinition
                {
                    Id = "slam", Name = "Slam", Effect = SkillEffectKind.Area, Damage = 20, Radius = 3f, CooldownSeconds = 2f,
                    Tags = new[] { "area" }, ChargeSeconds = 1f, ChargedSizePercent = 100,
                },
                new SkillDefinition
                {
                    Id = "bolt", Name = "Bolt", Effect = SkillEffectKind.Projectile, Damage = 25, Range = 30f, Radius = 0.5f,
                    ProjectileSpeed = 6f, CooldownSeconds = 1f, Tags = new[] { "projectile", "fire" },
                },
            },
        };

        private static readonly ModifierDefinition Ember = new ModifierDefinition
        {
            Id = "ember", Name = "Ember", AppliesToTag = "fire", ExplosionRadius = 2f, ExplosionDamagePercent = 50, DamagePercent = 20,
        };

        private static EnemyDefinition Goblin => new EnemyDefinition
        {
            Name = "Goblin", MaxHealth = 300, MoveSpeed = 2f, StopDistance = 1.2f, AttackDamage = 5, AttackRange = 1.6f,
            AttackWindupSeconds = 0.5f, AttackCooldownSeconds = 1f, XpReward = 60,
        };

        /// <summary>Two players doing a bit of everything, so the snapshot has real state in every part.</summary>
        private static Dictionary<PlayerId, PlayerCommand> Script(int tick)
        {
            var p1 = tick switch
            {
                3 => new PlayerCommand(0f, 1f, useSkillSlot: 1),              // a bolt into the goblins
                <= 30 => new PlayerCommand(0.6f, 0.8f, lightAttack: tick % 7 == 0),
                31 => new PlayerCommand(0f, 0f, useSkillSlot: 0, heldSkillSlots: 1), // start charging the slam
                <= 70 => new PlayerCommand(0.3f, 0f, heldSkillSlots: 1),       // keep holding
                _ => new PlayerCommand(-1f, 0.2f, dodge: tick == 90, useSkillSlot: tick == 95 ? 1 : (int?)null),
            };
            var p2 = new PlayerCommand(tick % 40 < 20 ? 1f : -1f, 0f, heavyAttack: tick % 50 == 10, allocateStat: tick == 45 ? Stat.Strength : (Stat?)null,
                useSkillSlot: tick == 40 ? 1 : (int?)null); // a slow bolt still flying when the snapshot is taken
            return new Dictionary<PlayerId, PlayerCommand> { [P1] = p1, [P2] = p2 };
        }

        private static SimulationWorld NewWorld()
        {
            var sim = new SimulationWorld(Config());
            sim.AddPlayer(P1, Vec3.Zero);
            sim.AddPlayer(P2, new Vec3(3f, 0f, 0f));
            sim.SpawnEnemy(Goblin, new Vec3(0f, 0f, 6f));
            sim.SpawnEnemy(Goblin, new Vec3(2f, 0f, 9f));
            sim.SpawnEnemy(Goblin, new Vec3(-3f, 0f, 2f));
            sim.GrantModifier(P1, Ember);
            sim.GrantXp(P2, 60);
            return sim;
        }

        private static List<string> RunTo(SimulationWorld sim, int lastTick)
        {
            var events = new List<string>();
            while (sim.Tick < lastTick)
            {
                sim.Step(Script(sim.Tick + 1));
                events.AddRange(sim.Events.Select(Describe));
            }
            return events;
        }

        /// <summary>Events as comparable text: type, tick, and their public values.</summary>
        private static string Describe(SimulationEvent e) =>
            e.GetType().Name + "@" + e.Tick + ":" + string.Join(",", e.GetType().GetProperties()
                .Where(p => p.Name != "Tick")
                .Select(p => p.GetValue(e) is SkillDefinition s ? s.Id : p.GetValue(e) is ModifierDefinition m ? m.Id : Convert.ToString(p.GetValue(e))));

        [Test]
        public void TheSnapshotTakesInEveryKindOfMidActionState()
        {
            var sim = NewWorld();
            RunTo(sim, 50);

            Assert.That(sim.Projectiles, Is.Not.Empty, "a bolt in flight");
            Assert.That(sim.GetPlayer(P1).ChargingSlot, Is.EqualTo(0), "a charge being held");
            Assert.That(sim.GetPlayer(P1).ActiveModifiers, Is.Not.Empty);
            Assert.That(sim.GetPlayer(P2).Level, Is.EqualTo(2));
            Assert.That(sim.CaptureSnapshot().Length, Is.GreaterThan(0));
        }

        [Test]
        public void CapturingARestoredWorldGivesTheSameBytes()
        {
            var sim = NewWorld();
            RunTo(sim, 50);
            var snapshot = sim.CaptureSnapshot();

            var restored = SimulationWorld.FromSnapshot(Config(), snapshot);

            Assert.That(restored.CaptureSnapshot(), Is.EqualTo(snapshot));
            Assert.That(restored.Tick, Is.EqualTo(50));
            Assert.That(restored.PlayerIds, Is.EqualTo(sim.PlayerIds));
            Assert.That(restored.EnemyIds, Is.EqualTo(sim.EnemyIds));
        }

        [Test]
        public void ARestoredWorldCarriesOnExactlyLikeTheOriginal()
        {
            var original = NewWorld();
            RunTo(original, 50);
            var restored = SimulationWorld.FromSnapshot(Config(), original.CaptureSnapshot());

            var originalEvents = RunTo(original, 160);
            var restoredEvents = RunTo(restored, 160);

            Assert.That(restoredEvents, Is.EqualTo(originalEvents), "the same things happen, in the same order");
            Assert.That(originalEvents, Has.Some.StartsWith("SkillUsed@"), "the stretch after the snapshot includes skills firing");
            Assert.That(restored.CaptureSnapshot(), Is.EqualTo(original.CaptureSnapshot()));
        }

        [Test]
        public void RestoringOverAnExistingWorldReplacesItsState()
        {
            var original = NewWorld();
            RunTo(original, 50);
            var snapshot = original.CaptureSnapshot();
            var other = NewWorld();
            RunTo(other, 120);

            other.RestoreSnapshot(snapshot);

            Assert.That(other.CaptureSnapshot(), Is.EqualTo(snapshot));
            Assert.That(other.Tick, Is.EqualTo(50));
        }

        [Test]
        public void ASnapshotNamingASkillThisWorldDoesNotHaveIsRejected()
        {
            var sim = NewWorld();
            RunTo(sim, 50);
            var snapshot = sim.CaptureSnapshot();
            var config = Config();
            config.Skills = config.Skills.Where(s => s.Id != "slam").ToArray();

            Assert.Throws<ArgumentException>(() => SimulationWorld.FromSnapshot(config, snapshot));
        }

        [Test]
        public void QueuedOperationsAreNotPartOfTheSnapshot()
        {
            var sim = NewWorld();
            RunTo(sim, 10);
            sim.GrantXp(P1, 1000); // queued for the next tick on this (host) world

            var restored = SimulationWorld.FromSnapshot(Config(), sim.CaptureSnapshot());
            restored.Step(Script(11));

            Assert.That(restored.GetPlayer(P1).Level, Is.EqualTo(1), "the host applies it and the next snapshot carries the result");
        }

        [Test]
        public void GarbageIsRejectedRatherThanHalfRestored()
        {
            var sim = NewWorld();
            var before = sim.CaptureSnapshot();

            Assert.Throws<ArgumentException>(() => sim.RestoreSnapshot(new byte[] { 1, 2, 3, 4, 5 }));
            Assert.That(sim.CaptureSnapshot(), Is.EqualTo(before), "left untouched");
        }
    }
}
