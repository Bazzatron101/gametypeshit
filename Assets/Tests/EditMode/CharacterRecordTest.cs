using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// A character is what a player keeps between sessions: level, XP, stats, skills and their bar,
    /// and modifiers. It is taken out of one world and brought into another (the player's next game,
    /// or a host's session as a guest), and saved as bytes in between (ticket 15).
    /// </summary>
    public class CharacterRecordTest
    {
        private static readonly PlayerId P = new PlayerId(1);

        private static readonly SkillDefinition Slam = new SkillDefinition
        {
            Id = "slam", Name = "Slam", Effect = SkillEffectKind.Area, Damage = 20, Radius = 3f, CooldownSeconds = 2f, Tags = new[] { "area" },
        };

        private static readonly SkillDefinition Bolt = new SkillDefinition
        {
            Id = "bolt", Name = "Bolt", Effect = SkillEffectKind.Projectile, Damage = 25, Range = 30f, Radius = 0.5f, ProjectileSpeed = 20f,
            CooldownSeconds = 1f, Tags = new[] { "projectile", "fire" }, UnlockLevel = 2,
        };

        private static readonly ModifierDefinition Ember = new ModifierDefinition
        {
            Id = "ember", Name = "Ember", AppliesToTag = "fire", ExplosionRadius = 2f, ExplosionDamagePercent = 50, DamagePercent = 20,
        };

        private static SimulationConfig Config(params SkillDefinition[] skills) => new SimulationConfig
        {
            TickRate = 60,
            Progression = new ProgressionDefinition
            {
                LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 }, FreePointsPerLevel = 3, StartingStatValue = 10,
            },
            Skills = skills.Length > 0 ? skills : new[] { Slam, Bolt },
        };

        private static void Step(SimulationWorld world, PlayerCommand command, int times = 1)
        {
            for (var i = 0; i < times; i++) world.Step(new Dictionary<PlayerId, PlayerCommand> { [P] = command });
        }

        /// <summary>A character with a bit of everything: two levels, spent and unspent points, a rearranged bar, a modifier.</summary>
        private static SimulationWorld PlayedWorld()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P, Vec3.Zero);
            world.GrantXp(P, 50 + 100 + 30);       // level 3 with 30 over
            world.GrantModifier(P, Ember);
            Step(world, PlayerCommand.None);
            Step(world, new PlayerCommand(0f, 0f, allocateStat: Stat.Vitality));
            Step(world, new PlayerCommand(0f, 0f, allocateStat: Stat.Strength));
            Step(world, new PlayerCommand(0f, 0f, assignSkill: new SkillAssignment(4, "bolt"))); // bolt moves to slot 4
            return world;
        }

        private static void AssertSameCharacter(PlayerState actual, PlayerState expected)
        {
            Assert.That(actual.Level, Is.EqualTo(expected.Level), "level");
            Assert.That(actual.Xp, Is.EqualTo(expected.Xp), "xp");
            Assert.That(actual.UnspentStatPoints, Is.EqualTo(expected.UnspentStatPoints), "unspent points");
            Assert.That(Enumerable.Range(0, PlayerStats.Count).Select(i => actual.Stats[(Stat)i]),
                Is.EqualTo(Enumerable.Range(0, PlayerStats.Count).Select(i => expected.Stats[(Stat)i])), "stats");
            Assert.That(actual.KnownSkills.Select(s => s.Id), Is.EqualTo(expected.KnownSkills.Select(s => s.Id)), "known skills");
            Assert.That(actual.SkillSlots.Select(s => s.Skill?.Id), Is.EqualTo(expected.SkillSlots.Select(s => s.Skill?.Id)), "the bar");
            Assert.That(actual.ActiveModifiers.Select(m => m.Id), Is.EqualTo(expected.ActiveModifiers.Select(m => m.Id)), "modifiers");
            Assert.That(actual.MaxHealth, Is.EqualTo(expected.MaxHealth), "max health follows vitality");
        }

        [Test]
        public void ACharacterBroughtIntoANewWorldIsTheSameCharacter()
        {
            var played = PlayedWorld();
            var before = played.GetPlayer(P);

            var record = played.CaptureCharacter(P);
            var next = new SimulationWorld(Config());
            next.AddPlayer(new PlayerId(7), new Vec3(5f, 0f, 5f), record);

            var after = next.GetPlayer(new PlayerId(7));
            AssertSameCharacter(after, before);
            Assert.That(after.Health, Is.EqualTo(after.MaxHealth), "arrives at full health");
            Assert.That(after.Position, Is.EqualTo(new Vec3(5f, 0f, 5f)));
        }

        [Test]
        public void ACharacterSurvivesSavingAsBytes()
        {
            var played = PlayedWorld();
            var record = played.CaptureCharacter(P);

            var bytes = CharacterCodec.Encode(record);
            var next = new SimulationWorld(Config());
            next.AddPlayer(P, Vec3.Zero, CharacterCodec.Decode(bytes));

            AssertSameCharacter(next.GetPlayer(P), played.GetPlayer(P));
            Assert.That(next.GetPlayer(P).ActiveModifiers.Single().ExplosionDamagePercent, Is.EqualTo(50), "a modifier keeps its numbers");
            Assert.That(CharacterCodec.Encode(CharacterCodec.Decode(bytes)), Is.EqualTo(bytes), "and encodes the same again");
        }

        [Test]
        public void UnreadableOrNewerSavesAreRejectedWithAReason()
        {
            var bytes = CharacterCodec.Encode(PlayedWorld().CaptureCharacter(P));

            Assert.Throws<ArgumentException>(() => CharacterCodec.Decode(new byte[] { 1, 2, 3 }));
            Assert.Throws<ArgumentException>(() => CharacterCodec.Decode(bytes.Take(bytes.Length - 3).ToArray()));

            var newer = (byte[])bytes.Clone();
            newer[4] = 99; // the version, right after the four-byte magic
            var e = Assert.Throws<ArgumentException>(() => CharacterCodec.Decode(newer));
            Assert.That(e.Message, Does.Contain("newer"));
        }

        [Test]
        public void SkillsTheGameNoLongerHasAreDroppedAndNewOnesForItsLevelAreLearned()
        {
            var record = PlayedWorld().CaptureCharacter(P); // knows slam and bolt, level 3

            var shield = new SkillDefinition { Id = "shield", Name = "Shield", Effect = SkillEffectKind.Area, Radius = 2f, UnlockLevel = 3 };
            var next = new SimulationWorld(Config(Slam, shield)); // bolt removed from the game, shield added at level 3
            next.AddPlayer(P, Vec3.Zero, record);

            var player = next.GetPlayer(P);
            Assert.That(player.KnownSkills.Select(s => s.Id), Is.EquivalentTo(new[] { "slam", "shield" }));
            Assert.That(player.SkillSlots.Select(s => s.Skill?.Id), Has.No.Member("bolt"));
            Assert.That(player.SkillSlots.Select(s => s.Skill?.Id), Has.Member("shield"), "a new skill goes on the bar");
        }

        [Test]
        public void ImpossibleValuesAreBroughtInsideTheRules()
        {
            // A guest's character comes from their own machine; a damaged or edited save must not break the session.
            var record = new CharacterRecord
            {
                Level = 99, Xp = -5, UnspentStatPoints = -3, Stats = new[] { 1, 2 },
                KnownSkills = { "slam", "slam" }, SkillBar = { [0] = "slam", [1] = "slam", [2] = "unknown" },
            };

            var world = new SimulationWorld(Config());
            world.AddPlayer(P, Vec3.Zero, record);

            var player = world.GetPlayer(P);
            Assert.That(player.Level, Is.EqualTo(10), "at most the cap");
            Assert.That(player.Xp, Is.Zero);
            Assert.That(player.UnspentStatPoints, Is.Zero);
            Assert.That(Enumerable.Range(0, PlayerStats.Count).Select(i => player.Stats[(Stat)i]), Has.All.GreaterThanOrEqualTo(10),
                "missing or low stats start at the starting value");
            Assert.That(player.KnownSkills.Count(s => s.Id == "slam"), Is.EqualTo(1));
            Assert.That(player.SkillSlots.Count(s => s.Skill?.Id == "slam"), Is.EqualTo(1), "a skill sits in one slot only");
        }

        [Test]
        public void ANewCharacterIsAFreshLevelOne()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P, Vec3.Zero, CharacterRecord.New());
            var fresh = new SimulationWorld(Config());
            fresh.AddPlayer(P, Vec3.Zero);

            AssertSameCharacter(world.GetPlayer(P), fresh.GetPlayer(P));
        }
    }
}
