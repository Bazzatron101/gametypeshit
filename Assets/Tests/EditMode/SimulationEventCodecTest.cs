using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The host sends its events to clients so they can show what happened on ticks they did not
    /// simulate themselves (another player's kill, an enemy's hit). Every event type must survive
    /// the trip intact.
    /// </summary>
    public class SimulationEventCodecTest
    {
        private static readonly SkillDefinition Slam = new SkillDefinition { Id = "slam", Name = "Slam", Effect = SkillEffectKind.Area };

        private static SimulationConfig Config() => new SimulationConfig { Skills = new[] { Slam } };

        private static readonly ModifierDefinition Ember = new ModifierDefinition
        {
            Id = "ember", Name = "Ember", AppliesToTag = "fire", ExplosionRadius = 2f, ExplosionDamagePercent = 50, DamagePercent = 20,
        };

        private static readonly PlayerId P = new PlayerId(2);
        private static readonly EnemyId E = new EnemyId(7);

        /// <summary>One of every event, with values that are not defaults.</summary>
        private static List<SimulationEvent> OneOfEach() => new List<SimulationEvent>
        {
            new AttackPerformed(11, P, AttackKind.Heavy),
            new AttackWindupStarted(12, P, AttackKind.Heavy, 30),
            new DamageDealt(13, P, E, 17, 83, Slam, isSecondary: true),
            new DamageDealt(13, P, E, 5, 78),
            new DodgePerformed(14, P),
            new EnemyAttackStarted(15, E, P, 45),
            new PlayerDamaged(16, E, P, 9, 91),
            new AttackDodged(17, E, P),
            new PlayerDefeated(18, P, E),
            new XpGained(19, P, 60, E),
            new XpGained(19, P, 40, null),
            new PlayerLeveledUp(20, P, 3, 3),
            new StatPointAllocated(21, P, Stat.Agility, 12),
            new SkillUsed(22, P, Slam, 1, 4.5f, 3.25f, 0.75f, isCounter: true),
            new SkillChargeStarted(23, P, Slam, 1),
            new SkillChargeUnlocked(24, P, Slam),
            new SkillSlotChanged(25, P, 2, Slam),
            new SkillSlotChanged(25, P, 3, null),
            new ModifierGranted(26, P, Ember),
            new ModifierRevoked(27, P, "ember"),
            new SkillUnlocked(28, P, Slam, 4),
            new SkillUnlocked(28, P, Slam, null),
            new Explosion(29, P, Slam, new Vec3(1f, 2f, 3f), 2.5f),
            new ProjectileEnded(30, 44, true, new Vec3(-1f, 0f, 8f)),
            new EnemyDefeated(31, E, P),
            new RoomEntered(32, 3),
            new RoomCleared(33, 3),
            new CheckpointActivated(34, 2, P),
            new PlayerRevived(35, P, 2, 1),
            new PlayerRevived(35, P, -1, PlayerRevived.NoLimit),
            new GateWiped(36, 2),
            new GateRestarted(37, 4),
            new GateCleared(38, "goblin-warren"),
            new BossAttackStarted(39, E, BossAttackKind.Charge, new Vec3(1f, 0f, 2f), new Vec3(0f, 0f, 1f), 60),
            new BossEnraged(40, E),
            new GateRewardGranted(41, P, Ember),
            new GateRewardGranted(41, P, null),
            new ReadyChanged(42, P, true),
            new ReturnedToHub(43, "goblin-warren", true),
        };

        [Test]
        public void EveryEventTypeIsCovered()
        {
            var all = typeof(SimulationEvent).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(SimulationEvent).IsAssignableFrom(t));
            Assert.That(OneOfEach().Select(e => e.GetType()).Distinct(), Is.EquivalentTo(all),
                "a new event type needs encoding (and a line in OneOfEach)");
        }

        [Test]
        public void EventsSurviveEncodingWithEveryValue()
        {
            var original = OneOfEach();

            var decoded = SimulationEventCodec.Decode(SimulationEventCodec.Encode(original), Config());

            Assert.That(decoded.Count, Is.EqualTo(original.Count));
            for (var i = 0; i < original.Count; i++)
            {
                Assert.That(decoded[i].GetType(), Is.EqualTo(original[i].GetType()));
                foreach (var property in original[i].GetType().GetProperties())
                {
                    Assert.That(Comparable(property.GetValue(decoded[i])), Is.EqualTo(Comparable(property.GetValue(original[i]))),
                        $"{original[i].GetType().Name}.{property.Name}");
                }
            }
        }

        [Test]
        public void SkillsResolveToTheReceivingSidesOwnDefinitions()
        {
            var config = Config();
            var decoded = SimulationEventCodec.Decode(SimulationEventCodec.Encode(new[] { new SkillChargeUnlocked(1, P, Slam) }), config);

            Assert.That(((SkillChargeUnlocked)decoded[0]).Skill, Is.SameAs(config.Skills[0]));
        }

        [Test]
        public void AnUnknownSkillOrGarbageIsRejectedAsMalformed()
        {
            var other = new SkillDefinition { Id = "nope" };
            var bytes = SimulationEventCodec.Encode(new[] { new SkillChargeUnlocked(1, P, other) });

            Assert.Throws<ArgumentException>(() => SimulationEventCodec.Decode(bytes, Config()));
            Assert.Throws<ArgumentException>(() => SimulationEventCodec.Decode(new byte[] { 1, 0, 0, 0, 99 }, Config()));
            Assert.Throws<ArgumentException>(() => SimulationEventCodec.Decode(bytes.Take(bytes.Length - 2).ToArray(), Config()));
        }

        /// <summary>Definitions compare by id (skills resolve to local copies; modifiers are rebuilt); a modifier also by its numbers.</summary>
        private static object Comparable(object value) => value switch
        {
            SkillDefinition s => "skill:" + s.Id,
            ModifierDefinition m => $"modifier:{m.Id}/{m.Name}/{m.AppliesToTag}/{m.DamagePercent}/{m.ExplosionRadius}/{m.ExplosionDamagePercent}",
            _ => value,
        };
    }
}
