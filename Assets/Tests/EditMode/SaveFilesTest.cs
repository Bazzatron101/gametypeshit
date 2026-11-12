using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Saves on disk (ticket 15): the player's character in its own file, with the previous save
    /// kept as a backup and an optional copy in Steam Cloud; and the host's world progress in a
    /// separate file that guests' characters never touch.
    /// </summary>
    public class SaveFilesTest
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "hunter-saves-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private sealed class FakeCloud : ICloudStorage
        {
            public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();
            public bool Available = true;
            public bool Broken;

            public bool IsAvailable => Available;

            public void Write(string name, byte[] data)
            {
                if (Broken) throw new IOException("The cloud is having a day.");
                Files[name] = data;
            }

            public byte[] Read(string name) => Broken ? throw new IOException("The cloud is having a day.") : Files.TryGetValue(name, out var data) ? data : null;
        }

        private static CharacterRecord Hunter(int level) => new CharacterRecord
        {
            Level = level, Xp = 7, UnspentStatPoints = 1, Stats = new[] { 10, 11, 12, 13, 14, 15 }, KnownSkills = { "slam" }, SkillBar = { [0] = "slam" },
        };

        [Test]
        public void ASavedCharacterLoadsBack()
        {
            new CharacterSaves(_root).Save(Hunter(level: 4));

            var saves = new CharacterSaves(_root);
            var loaded = saves.LoadOrNew();

            Assert.That(loaded.Level, Is.EqualTo(4));
            Assert.That(loaded.Stats, Is.EqualTo(new[] { 10, 11, 12, 13, 14, 15 }));
            Assert.That(loaded.IsNew, Is.False);
            Assert.That(saves.LastProblem, Is.Null);
        }

        [Test]
        public void WithNoSaveYouStartANewCharacter()
        {
            var saves = new CharacterSaves(_root);

            Assert.That(saves.LoadOrNew().IsNew, Is.True);
            Assert.That(saves.LastProblem, Is.Null, "no save is not a problem");
        }

        [Test]
        public void ADamagedSaveFallsBackToThePreviousOneAndSaysSo()
        {
            var saves = new CharacterSaves(_root);
            saves.Save(Hunter(level: 4));
            saves.Save(Hunter(level: 5)); // level 4 is now the backup
            File.WriteAllBytes(saves.FilePath, new byte[] { 1, 2, 3 });

            var loaded = new CharacterSaves(_root);
            var character = loaded.LoadOrNew();

            Assert.That(character.Level, Is.EqualTo(4), "the previous save");
            Assert.That(loaded.LastProblem, Does.Contain("backup"));
        }

        [Test]
        public void ASaveThatCannotBeReadIsKeptAsideNotOverwritten()
        {
            // For example a save from a newer version of the game: starting fresh must not destroy it.
            var saves = new CharacterSaves(_root);
            Directory.CreateDirectory(Path.GetDirectoryName(saves.FilePath));
            var mystery = new byte[] { 9, 9, 9, 9, 9 };
            File.WriteAllBytes(saves.FilePath, mystery);

            var character = saves.LoadOrNew();
            saves.Save(Hunter(level: 2));
            saves.Save(Hunter(level: 3));

            Assert.That(character.IsNew, Is.True);
            Assert.That(saves.LastProblemOnLoad, Does.Contain("could not be read"));
            var kept = Directory.GetFiles(Path.GetDirectoryName(saves.FilePath), "*.unreadable*");
            Assert.That(kept.Length, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(kept[0]), Is.EqualTo(mystery), "still there after later saves");
        }

        [Test]
        public void SavingLeavesNoTemporaryFilesBehind()
        {
            var saves = new CharacterSaves(_root);
            saves.Save(Hunter(level: 1));
            saves.Save(Hunter(level: 2));

            var names = Directory.GetFiles(Path.GetDirectoryName(saves.FilePath)).Select(Path.GetFileName).OrderBy(n => n).ToArray();
            Assert.That(names, Is.EqualTo(new[] { Path.GetFileName(saves.FilePath), Path.GetFileName(saves.FilePath) + ".bak" }));
        }

        [Test]
        public void SteamCloudBackupIsOffUntilSwitchedOnAndTheChoiceIsRemembered()
        {
            var cloud = new FakeCloud();
            var saves = new CharacterSaves(_root, cloud);
            saves.Save(Hunter(level: 2));
            Assert.That(saves.CloudBackup, Is.False);
            Assert.That(cloud.Files, Is.Empty, "nothing leaves the PC while it is off");

            Assert.That(saves.SetCloudBackup(true), Is.True);
            Assert.That(cloud.Files.Count, Is.EqualTo(1), "switching it on backs up the current save at once");
            saves.Save(Hunter(level: 3));

            Assert.That(CharacterCodec.Decode(cloud.Files.Values.Single()).Level, Is.EqualTo(3));
            Assert.That(new CharacterSaves(_root, cloud).CloudBackup, Is.True, "remembered next time");
        }

        [Test]
        public void ACloudFailureNeverLosesTheLocalSave()
        {
            var cloud = new FakeCloud();
            var saves = new CharacterSaves(_root, cloud);
            saves.SetCloudBackup(true);
            cloud.Broken = true;

            saves.Save(Hunter(level: 6));

            Assert.That(new CharacterSaves(_root).LoadOrNew().Level, Is.EqualTo(6));
            Assert.That(saves.LastProblem, Does.Contain("Steam Cloud"));
        }

        [Test]
        public void RestoringFromSteamCloudReplacesTheLocalSaveAndKeepsItAsTheBackup()
        {
            var cloud = new FakeCloud();
            var saves = new CharacterSaves(_root, cloud);
            saves.SetCloudBackup(true);
            saves.Save(Hunter(level: 8));      // in the cloud
            saves.SetCloudBackup(false);
            saves.Save(Hunter(level: 2));      // local only: say, a mistake, or a fresh PC

            Assert.That(saves.CloudHasBackup, Is.True);
            var restored = saves.RestoreFromCloud();

            Assert.That(restored.Level, Is.EqualTo(8));
            Assert.That(new CharacterSaves(_root).LoadOrNew().Level, Is.EqualTo(8));
            Assert.That(CharacterCodec.Decode(File.ReadAllBytes(saves.FilePath + ".bak")).Level, Is.EqualTo(2), "what it replaced can still be recovered");

            // The .bak only lasts until the next save; the restore also keeps a copy that later saves leave alone.
            saves.Save(Hunter(level: 9));
            saves.Save(Hunter(level: 10));
            var kept = Directory.GetFiles(Path.GetDirectoryName(saves.FilePath), "*.before-restore-*");
            Assert.That(kept.Length, Is.EqualTo(1));
            Assert.That(CharacterCodec.Decode(File.ReadAllBytes(kept[0])).Level, Is.EqualTo(2));
        }

        [Test]
        public void SwitchingBackupOnNeverReplacesADifferentHunterAlreadyInTheCloud()
        {
            // A new PC: a fresh local character, and the real one waiting in Steam Cloud from the old PC.
            var cloud = new FakeCloud();
            cloud.Files[CharacterSaves.CloudFileName] = CharacterCodec.Encode(Hunter(level: 30));
            var saves = new CharacterSaves(_root, cloud);
            saves.Save(Hunter(level: 1));

            Assert.That(saves.SetCloudBackup(true), Is.False);

            Assert.That(saves.CloudBackup, Is.False, "still off");
            Assert.That(saves.LastProblem, Does.Contain("level 30"));
            Assert.That(saves.PeekCloud().Level, Is.EqualTo(30), "the cloud's hunter is untouched");
            saves.Save(Hunter(level: 2));
            Assert.That(saves.PeekCloud().Level, Is.EqualTo(30), "and later saves do not touch it either");
        }

        [Test]
        public void ThePlayerCanChooseToReplaceTheCloudsHunterWithThisPcs()
        {
            var cloud = new FakeCloud();
            cloud.Files[CharacterSaves.CloudFileName] = CharacterCodec.Encode(Hunter(level: 30));
            var saves = new CharacterSaves(_root, cloud);
            saves.Save(Hunter(level: 4));
            saves.SetCloudBackup(true);

            Assert.That(saves.ReplaceCloudBackup(), Is.True);

            Assert.That(saves.CloudBackup, Is.True);
            Assert.That(saves.PeekCloud().Level, Is.EqualTo(4));
        }

        [Test]
        public void AfterRestoringBackupCanBeSwitchedOnWithoutAnyConflict()
        {
            var cloud = new FakeCloud();
            cloud.Files[CharacterSaves.CloudFileName] = CharacterCodec.Encode(Hunter(level: 30));
            var saves = new CharacterSaves(_root, cloud);
            saves.Save(Hunter(level: 1));

            saves.RestoreFromCloud();

            Assert.That(saves.SetCloudBackup(true), Is.True, "both sides now hold the same hunter");
        }

        [Test]
        public void ASaveThatCannotBeOpenedIsLeftAloneAndNothingIsSavedOverIt()
        {
            // Locked by another program or a second copy of the game: almost certainly a good save.
            var saves = new CharacterSaves(_root);
            saves.Save(Hunter(level: 7));
            saves.Save(Hunter(level: 8));
            var good = File.ReadAllBytes(saves.FilePath);
            var goodBackup = File.ReadAllBytes(saves.FilePath + ".bak");

            var session = new CharacterSaves(_root);
            CharacterRecord loaded;
            using (new FileStream(saves.FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                loaded = session.LoadOrNew();
            }
            session.Save(Hunter(level: 1));
            session.Save(Hunter(level: 1));

            Assert.That(loaded.IsNew, Is.True);
            Assert.That(session.SavingBlocked, Is.True);
            Assert.That(session.LastProblemOnLoad, Does.Contain("could not be opened"));
            Assert.That(session.LastProblem, Is.Not.Null, "each skipped save says why");
            Assert.That(File.ReadAllBytes(saves.FilePath), Is.EqualTo(good));
            Assert.That(File.ReadAllBytes(saves.FilePath + ".bak"), Is.EqualTo(goodBackup));
            Assert.That(new CharacterSaves(_root).LoadOrNew().Level, Is.EqualTo(8), "next time it loads normally");
        }

        [Test]
        public void TwoUnreadableSavesInTheSameSecondAreBothKept()
        {
            var saves = new CharacterSaves(_root);
            Directory.CreateDirectory(Path.GetDirectoryName(saves.FilePath));
            File.WriteAllBytes(saves.FilePath, new byte[] { 1 });
            saves.LoadOrNew();
            File.WriteAllBytes(saves.FilePath, new byte[] { 2 });
            saves.LoadOrNew();

            Assert.That(Directory.GetFiles(Path.GetDirectoryName(saves.FilePath), "*.unreadable-*").Length, Is.EqualTo(2));
        }

        [Test]
        public void RestoringWithNothingUsableInTheCloudChangesNothingAndSaysWhy()
        {
            var cloud = new FakeCloud();
            var saves = new CharacterSaves(_root, cloud);
            saves.Save(Hunter(level: 2));

            Assert.That(saves.CloudHasBackup, Is.False);
            Assert.That(saves.RestoreFromCloud(), Is.Null);
            Assert.That(saves.LastProblem, Does.Contain("no backup"));

            cloud.Files[CharacterSaves.CloudFileName] = new byte[] { 1, 2, 3 };
            Assert.That(saves.RestoreFromCloud(), Is.Null);
            Assert.That(saves.LastProblem, Does.Contain("could not be read"));
            Assert.That(new CharacterSaves(_root).LoadOrNew().Level, Is.EqualTo(2), "the local save is untouched");
        }

        [Test]
        public void WithoutSteamTheCloudOptionsDoNothing()
        {
            var cloud = new FakeCloud();
            var saves = new CharacterSaves(_root, cloud);
            saves.SetCloudBackup(true);
            cloud.Available = false; // Steam is not running this time

            saves.Save(Hunter(level: 2));

            Assert.That(saves.CloudAvailable, Is.False);
            Assert.That(saves.CloudHasBackup, Is.False);
            Assert.That(saves.LastProblem, Is.Null, "an unavailable cloud is not an error on every save");
            Assert.That(new CharacterSaves(_root, new FakeCloud { Available = false }).SetCloudBackup(false), Is.True);
            Assert.That(new CharacterSaves(_root).LoadOrNew().Level, Is.EqualTo(2));
        }

        [Test]
        public void TheHostsWorldIsSavedApartFromCharacters()
        {
            var characters = new CharacterSaves(_root);
            var worlds = new WorldSaves(_root);
            characters.Save(Hunter(level: 3));
            var characterBytes = File.ReadAllBytes(characters.FilePath);

            var world = worlds.LoadOrNew();
            world.ClearedGates.Add("e-rank-goblin-den");
            worlds.Save(world);

            Assert.That(Path.GetDirectoryName(worlds.FilePath), Is.Not.EqualTo(Path.GetDirectoryName(characters.FilePath)), "separate folders");
            Assert.That(File.ReadAllBytes(characters.FilePath), Is.EqualTo(characterBytes), "saving the world does not touch the character");
            Assert.That(new WorldSaves(_root).LoadOrNew().ClearedGates, Is.EqualTo(new[] { "e-rank-goblin-den" }));

            characters.Save(Hunter(level: 4));
            Assert.That(new WorldSaves(_root).LoadOrNew().ClearedGates, Is.EqualTo(new[] { "e-rank-goblin-den" }), "and the other way round");
        }
    }
}
