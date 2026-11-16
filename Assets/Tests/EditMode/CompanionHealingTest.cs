using System.Collections.Generic;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEditor;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Bot teammates heal (ticket 59): a heal bolt goes to the most hurt ally in range, and only along a
    /// path whose first ally needs it; a heal area is cast when the bot or an ally inside it is hurt.
    /// Nobody at full health is healed.
    /// </summary>
    public class CompanionHealingTest
    {
        private static readonly PlayerId Bot = new PlayerId(1);
        private static readonly PlayerId Ally = new PlayerId(2);
        private static readonly PlayerId Other = new PlayerId(3);

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

        private static CompanionBrain Cleric(string classId = "cleric") => new CompanionBrain(
            new CompanionRecord { Name = "Bot", Character = new CharacterRecord { IsNew = true, ClassId = classId },
                Experience = new CompanionExperience { GatesCleared = 30, BossesDefeated = 30 } },
            seed: 1);

        private static SimulationWorld World(params SkillDefinition[] skills)
        {
            var world = new SimulationWorld(new SimulationConfig { TickRate = 60, Skills = skills });
            world.AddPlayer(Bot, Vec3.Zero);
            return world;
        }

        /// <summary>Steps with the bot deciding and everyone else standing still; returns every event.</summary>
        private static List<SimulationEvent> Play(SimulationWorld world, CompanionBrain brain, int ticks)
        {
            var all = new List<SimulationEvent>();
            for (var i = 0; i < ticks; i++)
            {
                var commands = new Dictionary<PlayerId, PlayerCommand> { [Bot] = brain.Next(world, Bot, world.Events) };
                world.Step(commands);
                all.AddRange(world.Events);
            }
            return all;
        }

        private static List<PlayerHealed> Heals(IEnumerable<SimulationEvent> events) => events.OfType<PlayerHealed>().Where(h => h.Healer == Bot).ToList();

        [Test]
        public void ABotHealsAHurtAllyWithItsHealBolt()
        {
            var world = World(HealBolt);
            world.AddPlayer(Ally, new Vec3(4f, 0f, 5f));
            world.DamagePlayerForTesting(Ally, 50);

            var heals = Heals(Play(world, Cleric(), 60));

            Assert.That(heals, Is.Not.Empty, "it heals the hurt ally");
            Assert.That(heals.All(h => h.Player == Ally), Is.True);
            Assert.That(heals[0].Amount, Is.EqualTo(30), "a full heal's worth");
        }

        [Test]
        public void ABotDoesNotHealAnyoneAtFullHealth()
        {
            var world = World(HealBolt, HealArea);
            world.AddPlayer(Ally, new Vec3(0f, 0f, 3f));

            var events = Play(world, Cleric(), 180);

            Assert.That(events.OfType<SkillUsed>(), Is.Empty, "no heal is wasted");
        }

        [Test]
        public void TheHealBoltGoesToTheMostHurtAllyInRange()
        {
            var world = World(HealBolt);
            world.AddPlayer(Ally, new Vec3(-5f, 0f, 0f));
            world.AddPlayer(Other, new Vec3(0f, 0f, 8f));
            var outOfRange = new PlayerId(4);
            world.AddPlayer(outOfRange, new Vec3(30f, 0f, 0f));
            world.DamagePlayerForTesting(Ally, 40);
            world.DamagePlayerForTesting(Other, 70);
            world.DamagePlayerForTesting(outOfRange, 90);

            var heals = Heals(Play(world, Cleric(), 20));

            Assert.That(heals.Select(h => h.Player).FirstOrDefault(), Is.EqualTo(Other), "the worst hurt it can reach, first");
        }

        [Test]
        public void TheHealBoltIsNotFiredThroughAnAllyWhoIsFine()
        {
            var world = World(HealBolt);
            world.AddPlayer(Other, new Vec3(0f, 0f, 3f)); // fine, and in the way
            world.AddPlayer(Ally, new Vec3(0f, 0f, 8f)); // hurt, behind them
            world.DamagePlayerForTesting(Ally, 50);

            var events = Play(world, Cleric(), 120);

            Assert.That(events.OfType<SkillUsed>(), Is.Empty, "the bolt would heal the one in front, who needs nothing");
        }

        [Test]
        public void ABotCastsItsHealAreaWhenItOrAnAllyInsideIsHurt()
        {
            var alone = World(HealArea);
            alone.DamagePlayerForTesting(Bot, 50);
            Assert.That(Heals(Play(alone, Cleric(), 60)).Select(h => h.Player).Distinct(), Is.EqualTo(new[] { Bot }), "it heals itself");

            var withAlly = World(HealArea);
            withAlly.AddPlayer(Ally, new Vec3(3f, 0f, 0f));
            withAlly.DamagePlayerForTesting(Ally, 50);
            Assert.That(Heals(Play(withAlly, Cleric(), 60)).Select(h => h.Player).Distinct(), Is.EqualTo(new[] { Ally }), "it heals the ally beside it");

            var farAlly = World(HealArea);
            farAlly.AddPlayer(Ally, new Vec3(12f, 0f, 0f));
            farAlly.DamagePlayerForTesting(Ally, 50);
            Assert.That(Play(farAlly, Cleric(), 60).OfType<SkillUsed>(), Is.Empty, "an ally out of the area is out of its reach");
        }

        [Test]
        public void ABotClericFromTheRealContentHealsAHurtAllyInTheFirstGate()
        {
            var content = AssetDatabase.FindAssets("t:GameContentAsset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameContentAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Single();
            var config = new SimulationConfig();
            content.ApplyTo(config);
            var world = new SimulationWorld(config);
            world.AddPlayer(Ally, Vec3.Zero, new CharacterRecord { IsNew = true, ClassId = "warrior" });
            world.AddPlayer(Bot, Vec3.Zero, new CharacterRecord { IsNew = true, ClassId = "cleric" });
            world.StartGate(content.Gates[0].Id);
            Assert.That(world.GetPlayer(Bot).SkillSlots.Any(s => s.Skill != null && s.Skill.Heals), Is.True, "a new Cleric starts with a heal");

            var brain = Cleric();
            brain.Leader = Ally;
            world.DamagePlayerForTesting(Ally, 50);
            var events = new List<SimulationEvent>();
            for (var tick = 0; tick < 180; tick++)
            {
                world.Step(new Dictionary<PlayerId, PlayerCommand> { [Ally] = PlayerCommand.None, [Bot] = brain.Next(world, Bot, world.Events) });
                events.AddRange(world.Events);
            }

            var heals = Heals(events);
            Assert.That(heals.Any(h => h.Player == Ally), Is.True, "the bot Cleric heals the hurt warrior");
        }
    }
}
