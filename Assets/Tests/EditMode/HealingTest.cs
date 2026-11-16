using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Healing (ticket 56) and drained life (ticket 58). A healing bolt flies where it is aimed, past enemies, and
    /// heals the first other player it reaches; a healing area heals every player in it, the caster too. A skill
    /// that drains life heals its caster for part of what its direct hits remove. Healing never goes past a
    /// player's max and never reaches the fallen. Sense raises skills that scale with it.
    /// </summary>
    public class HealingTest
    {
        private static readonly PlayerId Cleric = new PlayerId(1);
        private static readonly PlayerId Ally = new PlayerId(2);

        private static SkillDefinition HealBolt => new SkillDefinition
        {
            Id = "mend", Name = "Mend", Effect = SkillEffectKind.Projectile, Damage = 0, Healing = 30, Range = 12f, Radius = 0.8f, ProjectileSpeed = 30f,
            CooldownSeconds = 1f, ScalingStat = Stat.Sense, Tags = new[] { SkillTags.Magic, SkillTags.Projectile },
        };

        private static SkillDefinition HealArea => new SkillDefinition
        {
            Id = "sanctuary", Name = "Sanctuary", Effect = SkillEffectKind.Area, Damage = 0, Healing = 25, Radius = 5f, CooldownSeconds = 1f,
            ScalingStat = Stat.Sense, Tags = new[] { SkillTags.Magic, SkillTags.Area },
        };

        private static SkillDefinition DrainBolt => new SkillDefinition
        {
            Id = "drain", Name = "Drain", Effect = SkillEffectKind.Area, Damage = 40, Radius = 4f, CooldownSeconds = 1f,
            ScalingStat = Stat.Intelligence, LifeStealPercent = 50, Tags = new[] { SkillTags.Magic, SkillTags.Area },
        };

        private static SimulationWorld World(params SkillDefinition[] skills)
        {
            var world = new SimulationWorld(new SimulationConfig { TickRate = 60, Skills = skills });
            world.AddPlayer(Cleric, Vec3.Zero);
            return world;
        }

        private static List<SimulationEvent> Step(SimulationWorld world, int ticks, PlayerCommand cleric = default)
        {
            var all = new List<SimulationEvent>();
            for (var i = 0; i < ticks; i++)
            {
                world.Step(new Dictionary<PlayerId, PlayerCommand> { [Cleric] = i == 0 ? cleric : PlayerCommand.None });
                all.AddRange(world.Events);
            }
            return all;
        }

        private static PlayerCommand Use(int slot, float aim = 0f) => new PlayerCommand(0f, 0f, useSkillSlot: slot, aimYawDegrees: aim);

        /// <summary>An enemy that never moves or strikes, with plenty of health, to stand in the way or be hit.</summary>
        private static EnemyDefinition Dummy => new EnemyDefinition { Name = "Dummy", MaxHealth = 100000, MoveSpeed = 0f, AttackRange = 0f };

        [Test]
        public void AHealingBoltFliesPastEnemiesToTheFirstAlly()
        {
            var world = World(HealBolt);
            world.AddPlayer(Ally, new Vec3(0f, 0f, 8f));
            world.SpawnEnemy(Dummy, new Vec3(0f, 0f, 4f)); // in the way
            world.DamagePlayerForTesting(Ally, 50);
            Step(world, 1);
            var before = world.GetPlayer(Ally).Health;

            var events = Step(world, 60, Use(0, aim: 0f));

            var healed = events.OfType<PlayerHealed>().Single();
            Assert.That((healed.Player, healed.Healer, healed.Amount), Is.EqualTo((Ally, Cleric, 30)));
            Assert.That(world.GetPlayer(Ally).Health, Is.EqualTo(before + 30));
            Assert.That(events.OfType<DamageDealt>(), Is.Empty, "a heal hurts nobody on the way");
        }

        [Test]
        public void AHealingBoltNeverHealsItsCaster()
        {
            var world = World(HealBolt);
            world.DamagePlayerForTesting(Cleric, 50);
            Step(world, 1);

            var events = Step(world, 60, Use(0));

            Assert.That(events.OfType<PlayerHealed>(), Is.Empty, "it is for others: it flies off and fades");
        }

        [Test]
        public void AHealingAreaHealsEveryoneInItTheCasterToo()
        {
            var world = World(HealArea);
            world.AddPlayer(Ally, new Vec3(3f, 0f, 0f));
            var far = new PlayerId(3);
            world.AddPlayer(far, new Vec3(20f, 0f, 0f));
            foreach (var id in new[] { Cleric, Ally, far }) world.DamagePlayerForTesting(id, 60);
            Step(world, 1);

            var events = Step(world, 2, Use(0));

            Assert.That(events.OfType<PlayerHealed>().Select(h => h.Player), Is.EquivalentTo(new[] { Cleric, Ally }), "not the one 20 m away");
            Assert.That(events.OfType<PlayerHealed>().All(h => h.Amount == 25), Is.True);
        }

        [Test]
        public void HealingNeverGoesPastMaxOrReachesTheFallen()
        {
            var world = World(HealArea);
            world.AddPlayer(Ally, new Vec3(2f, 0f, 0f));
            world.DamagePlayerForTesting(Cleric, 10); // 10 short of max
            world.DamagePlayerForTesting(Ally, world.GetPlayer(Ally).MaxHealth); // fallen
            Step(world, 1);
            Assert.That(world.GetPlayer(Ally).LifeState, Is.EqualTo(PlayerLifeState.Defeated));

            var events = Step(world, 2, Use(0));

            Assert.That(events.OfType<PlayerHealed>().Select(h => (h.Player, h.Amount)), Is.EqualTo(new[] { (Cleric, 10) }), "only up to max, and the fallen are not raised");
            Assert.That(world.GetPlayer(Cleric).Health, Is.EqualTo(world.GetPlayer(Cleric).MaxHealth));
        }

        [Test]
        public void SenseRaisesHealing()
        {
            var plain = World(HealArea);
            var wise = World(HealArea);
            wise.SetStatsForTesting(Cleric, Stat.Sense, 20); // 10 above the start: +50%
            foreach (var w in new[] { plain, wise }) { w.DamagePlayerForTesting(Cleric, 80); Step(w, 1); }

            var plainHeal = Step(plain, 2, Use(0)).OfType<PlayerHealed>().Single().Amount;
            var wiseHeal = Step(wise, 2, Use(0)).OfType<PlayerHealed>().Single().Amount;

            Assert.That((plainHeal, wiseHeal), Is.EqualTo((25, 38)), "25 at +50%, rounding halves up");
        }

        [Test]
        public void DrainLifeHealsTheCasterForPartOfTheDamage()
        {
            var world = World(DrainBolt);
            world.SpawnEnemy(Dummy, new Vec3(0f, 0f, 2f));
            world.SpawnEnemy(Dummy, new Vec3(0f, 0f, -2f));
            world.DamagePlayerForTesting(Cleric, 80);
            Step(world, 1);

            var events = Step(world, 2, Use(0));

            Assert.That(events.OfType<DamageDealt>().Select(d => d.Amount), Is.EqualTo(new[] { 40, 40 }));
            Assert.That(events.OfType<PlayerHealed>().Select(h => (h.Player, h.Amount)), Is.EqualTo(new[] { (Cleric, 20), (Cleric, 20) }), "half of each hit");
        }

        [Test]
        public void AHealingSkillIsABoltOrAnAreaAndDealsNoDamage()
        {
            var cone = HealBolt;
            cone.Effect = SkillEffectKind.Cone;
            Assert.Throws<ArgumentException>(() => World(cone));
            var both = HealArea;
            both.Damage = 10;
            Assert.Throws<ArgumentException>(() => World(both));
        }

        [Test]
        public void TheHealCrossesTheWire()
        {
            var config = new SimulationConfig { Skills = new[] { HealBolt } };
            var healed = new PlayerHealed(7, Ally, 30, 90, Cleric, HealBolt);
            var back = (PlayerHealed)SimulationEventCodec.Decode(SimulationEventCodec.Encode(new SimulationEvent[] { healed }), config).Single();
            Assert.That((back.Player, back.Amount, back.Health, back.Healer, back.Skill.Id), Is.EqualTo((Ally, 30, 90, Cleric, "mend")));
        }
    }
}
