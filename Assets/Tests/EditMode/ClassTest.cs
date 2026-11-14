using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Classes. A character picks one when it is made: Warrior, Rogue, Archer or Wizard. A class
    /// decides which skills the character learns (its own, plus any shared ones), gives stat
    /// points in its main stats every level on top of the free ones, and may raise the jump (the
    /// Rogue clears an enemy). The choice is part of the character and is saved with it.
    /// </summary>
    public class ClassTest
    {
        private static readonly PlayerId P = new PlayerId(1);

        private static ClassDefinition Warrior => new ClassDefinition { Id = "warrior", Name = "Warrior", StatsPerLevel = Stats(str: 1, vit: 1) };
        private static ClassDefinition Wizard => new ClassDefinition { Id = "wizard", Name = "Wizard", StatsPerLevel = Stats(intel: 1, sense: 1) };
        private static ClassDefinition Rogue => new ClassDefinition { Id = "rogue", Name = "Rogue", StatsPerLevel = Stats(agi: 1, luck: 1), JumpHeightPercent = 450 };

        private static int[] Stats(int str = 0, int agi = 0, int vit = 0, int intel = 0, int sense = 0, int luck = 0)
        {
            var stats = new int[PlayerStats.Count];
            stats[(int)Stat.Strength] = str;
            stats[(int)Stat.Agility] = agi;
            stats[(int)Stat.Vitality] = vit;
            stats[(int)Stat.Intelligence] = intel;
            stats[(int)Stat.Sense] = sense;
            stats[(int)Stat.Luck] = luck;
            return stats;
        }

        private static SkillDefinition Skill(string id, string classId, int unlock = 1) => new SkillDefinition
        {
            Id = id, Name = id, Effect = SkillEffectKind.Area, Damage = 5, Radius = 2f, UnlockLevel = unlock, ClassId = classId,
        };

        private static SimulationConfig Config() => new SimulationConfig
        {
            TickRate = 60,
            Progression = new ProgressionDefinition
            {
                LevelCap = 20, XpToNextLevel = Enumerable.Repeat(100, 19).ToArray(), FreePointsPerLevel = 3, StartingStatValue = 10,
            },
            Classes = new[] { Warrior, Wizard, Rogue },
            Skills = new[]
            {
                Skill("rending-slash", "warrior"), Skill("ground-slam", "warrior"), Skill("whirlwind", "warrior", unlock: 3),
                Skill("fireball", "wizard"), Skill("flame-burst", "wizard", unlock: 3),
                Skill("twin-fangs", "rogue"),
                Skill("first-aid", null), // shared: every class
            },
        };

        private static CharacterRecord NewCharacter(string classId) => new CharacterRecord { IsNew = true, ClassId = classId };

        private static SimulationWorld WorldWith(CharacterRecord character)
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P, Vec3.Zero, character);
            return world;
        }

        private static IEnumerable<string> Known(SimulationWorld world) => world.GetPlayer(P).KnownSkills.Select(s => s.Id);

        private static void LevelTo(SimulationWorld world, int level)
        {
            world.RaiseToLevel(P, level);
            world.Step(new Dictionary<PlayerId, PlayerCommand>());
        }

        [Test]
        public void ACharacterStartsWithItsClassesSkillsAndTheSharedOnes()
        {
            Assert.That(Known(WorldWith(NewCharacter("warrior"))), Is.EquivalentTo(new[] { "rending-slash", "ground-slam", "first-aid" }));
            Assert.That(Known(WorldWith(NewCharacter("wizard"))), Is.EquivalentTo(new[] { "fireball", "first-aid" }), "a wizard starts with just its fireball");
            Assert.That(WorldWith(NewCharacter("wizard")).GetPlayer(P).Class.Id, Is.EqualTo("wizard"));
        }

        [Test]
        public void LevellingUpUnlocksOnlyTheClassesOwnSkills()
        {
            var world = WorldWith(NewCharacter("warrior"));

            LevelTo(world, 3);

            Assert.That(Known(world), Has.Member("whirlwind"));
            Assert.That(Known(world), Has.No.Member("flame-burst"));
        }

        [Test]
        public void EachLevelGivesTheClassesStatsOnTopOfTheFreePoints()
        {
            var world = WorldWith(NewCharacter("warrior"));

            LevelTo(world, 4); // three levels

            var player = world.GetPlayer(P);
            Assert.That(player.Stats[Stat.Strength], Is.EqualTo(13));
            Assert.That(player.Stats[Stat.Vitality], Is.EqualTo(13));
            Assert.That(player.Stats[Stat.Intelligence], Is.EqualTo(10), "not the wizard's stats");
            Assert.That(player.UnspentStatPoints, Is.EqualTo(9), "and the free points are still yours to place");
            Assert.That(player.MaxHealth, Is.GreaterThan(WorldWith(NewCharacter("wizard")).GetPlayer(P).MaxHealth), "vitality counts at once");
        }

        [Test]
        public void TheRogueJumpsHighEnoughToClearAnEnemy()
        {
            var rogue = WorldWith(NewCharacter("rogue"));
            var warrior = WorldWith(NewCharacter("warrior"));

            Assert.That(rogue.GetPlayer(P).JumpHeightPercent, Is.EqualTo(450));
            Assert.That(warrior.GetPlayer(P).JumpHeightPercent, Is.EqualTo(100));
        }

        [Test]
        public void TheClassTravelsWithTheCharacter()
        {
            var world = WorldWith(NewCharacter("rogue"));
            LevelTo(world, 2);

            var record = world.CaptureCharacter(P);
            var bytes = CharacterCodec.Encode(record);
            var next = new SimulationWorld(Config());
            next.AddPlayer(P, Vec3.Zero, CharacterCodec.Decode(bytes));

            Assert.That(record.ClassId, Is.EqualTo("rogue"));
            Assert.That(next.GetPlayer(P).Class.Id, Is.EqualTo("rogue"));
            Assert.That(next.GetPlayer(P).JumpHeightPercent, Is.EqualTo(450));
        }

        [Test]
        public void AnOlderSaveWithoutAClassStillLoadsClassless()
        {
            // A format-1 character (before classes): level, xp, points, stats, skills, bar, modifiers.
            var old = new CharacterRecord { Level = 3, Xp = 5, KnownSkills = { "first-aid" } };
            var bytes = CharacterCodec.EncodeVersion1ForTests(old);

            var loaded = CharacterCodec.Decode(bytes);

            Assert.That(loaded.ClassId, Is.Null);
            Assert.That(loaded.Level, Is.EqualTo(3));
        }

        [Test]
        public void AClasslessCharacterLearnsOnlySharedSkills()
        {
            var world = WorldWith(CharacterRecord.New());

            Assert.That(Known(world), Is.EquivalentTo(new[] { "first-aid" }));
            Assert.That(world.GetPlayer(P).Class, Is.Null);
        }

        [Test]
        public void ChoosingAClassForAnExistingCharacterGivesTheStatsItsLevelsHaveEarned()
        {
            // An existing save made before classes: level 5, its own points placed.
            var existing = new CharacterRecord { Level = 5, Stats = new[] { 12, 10, 10, 10, 10, 10 }, UnspentStatPoints = 2 };

            var chosen = existing.WithClass(Wizard, Config().Progression);
            var world = WorldWith(chosen);

            var player = world.GetPlayer(P);
            Assert.That(chosen.ClassId, Is.EqualTo("wizard"));
            Assert.That(player.Stats[Stat.Intelligence], Is.EqualTo(14), "four levels of wizard intelligence");
            Assert.That(player.Stats[Stat.Strength], Is.EqualTo(12), "its own choices stay");
            Assert.That(player.UnspentStatPoints, Is.EqualTo(2));
            Assert.That(Known(world), Is.SupersetOf(new[] { "fireball", "flame-burst" }), "and the skills its level has reached");
            Assert.Throws<InvalidOperationException>(() => chosen.WithClass(Warrior, Config().Progression), "the choice is permanent");
        }

        [Test]
        public void ACharacterWhoseClassIsGoneGainsNoStatsForPastLevelsFromItsNewClass()
        {
            // Its stats already hold its old class's bonuses, which cannot be told apart from its own.
            var gone = new CharacterRecord { Level = 5, Stats = new[] { 14, 10, 14, 10, 10, 10 } };

            var chosen = gone.WithClass(Wizard, Config().Progression, addEarnedStats: false);

            Assert.That(chosen.Stats, Is.EqualTo(new[] { 14, 10, 14, 10, 10, 10 }));
            Assert.That(gone.ClassId, Is.Null, "the original record is left alone");
        }

        [Test]
        public void ACharacterNamingAClassTheGameDoesNotHaveIsClassless()
        {
            var world = WorldWith(NewCharacter("necromancer"));

            Assert.That(world.GetPlayer(P).Class, Is.Null);
            Assert.That(Known(world), Is.EquivalentTo(new[] { "first-aid" }));
        }

        [Test]
        public void TheClassSurvivesASnapshot()
        {
            var world = WorldWith(NewCharacter("rogue"));
            var copy = SimulationWorld.FromSnapshot(Config(), world.CaptureSnapshot());

            Assert.That(copy.GetPlayer(P).Class.Id, Is.EqualTo("rogue"));
            LevelTo(world, 3);
            LevelTo(copy, 3);
            Assert.That(copy.CaptureSnapshot(), Is.EqualTo(world.CaptureSnapshot()));
        }

        [Test]
        public void ContentChecksEachClassesStartersFitTheBar()
        {
            var config = Config();
            config.Skills = config.Skills.Concat(Enumerable.Range(0, 5).Select(i => Skill("extra-" + i, "warrior"))).ToArray();

            var e = Assert.Throws<ArgumentException>(() => new SimulationWorld(config));
            Assert.That(e.Message, Does.Contain("warrior"));
        }

        [Test]
        public void ContentChecksASkillsClassExists()
        {
            var config = Config();
            config.Skills = config.Skills.Append(Skill("orphan", "bard")).ToArray();

            var e = Assert.Throws<ArgumentException>(() => new SimulationWorld(config));
            Assert.That(e.Message, Does.Contain("bard"));
        }
    }
}
