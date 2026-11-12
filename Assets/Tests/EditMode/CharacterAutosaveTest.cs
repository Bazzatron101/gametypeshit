using System;
using System.Collections.Generic;
using System.IO;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The game saves the local player's character by itself: soon after anything worth keeping
    /// happens to it, not more than once a second, and whenever asked outright (leaving, quitting).
    /// </summary>
    public class CharacterAutosaveTest
    {
        private static readonly PlayerId Me = new PlayerId(1);
        private static readonly PlayerId Other = new PlayerId(2);
        private string _root;
        private CharacterSaves _saves;
        private SimulationWorld _world;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "hunter-autosave-" + Guid.NewGuid().ToString("N"));
            _saves = new CharacterSaves(_root);
            _world = new SimulationWorld(new SimulationConfig
            {
                Progression = new ProgressionDefinition { LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 } },
            });
            _world.AddPlayer(Me, Vec3.Zero);
            _world.AddPlayer(Other, Vec3.Zero);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private int SavedLevel() => File.Exists(_saves.FilePath) ? new CharacterSaves(_root).LoadOrNew().Level : 0;

        private void Earn(PlayerId who, int xp)
        {
            _world.GrantXp(who, xp);
            _world.Step(new Dictionary<PlayerId, PlayerCommand>());
        }

        [Test]
        public void ProgressIsSavedSoonAfterItHappens()
        {
            var autosave = new CharacterAutosave(_saves);
            Earn(Me, 60);

            autosave.Observe(_world.Events, Me);
            var saved = autosave.SaveIfDue(_world, Me, now: 10f);

            Assert.That(saved, Is.True);
            Assert.That(SavedLevel(), Is.EqualTo(2));
        }

        [Test]
        public void NothingIsWrittenWhenNothingChanged()
        {
            var autosave = new CharacterAutosave(_saves);
            _world.Step(new Dictionary<PlayerId, PlayerCommand> { [Me] = new PlayerCommand(1f, 0f, lightAttack: true) });

            autosave.Observe(_world.Events, Me);

            Assert.That(autosave.SaveIfDue(_world, Me, now: 10f), Is.False, "moving and swinging is not progress");
            Assert.That(File.Exists(_saves.FilePath), Is.False);
        }

        [Test]
        public void AnotherPlayersProgressIsNotMine()
        {
            var autosave = new CharacterAutosave(_saves);
            Earn(Other, 60);

            autosave.Observe(_world.Events, Me);

            Assert.That(autosave.SaveIfDue(_world, Me, now: 10f), Is.False);
        }

        [Test]
        public void SavesAreSpacedOutButNothingIsLost()
        {
            var autosave = new CharacterAutosave(_saves, minimumSecondsBetweenSaves: 1f);
            Earn(Me, 60);
            autosave.Observe(_world.Events, Me);
            autosave.SaveIfDue(_world, Me, now: 10f);

            Earn(Me, 100); // level 3, a moment later
            autosave.Observe(_world.Events, Me);
            Assert.That(autosave.SaveIfDue(_world, Me, now: 10.2f), Is.False, "too soon after the last save");
            Assert.That(SavedLevel(), Is.EqualTo(2));

            Assert.That(autosave.SaveIfDue(_world, Me, now: 11.1f), Is.True, "but it is not forgotten");
            Assert.That(SavedLevel(), Is.EqualTo(3));
        }

        [Test]
        public void LeavingSavesAtOnceWhateverTheTiming()
        {
            var autosave = new CharacterAutosave(_saves, minimumSecondsBetweenSaves: 60f);
            Earn(Me, 60);
            autosave.Observe(_world.Events, Me);
            autosave.SaveIfDue(_world, Me, now: 10f);
            Earn(Me, 100);

            autosave.SaveNow(_world, Me);

            Assert.That(SavedLevel(), Is.EqualTo(3));
        }

        [Test]
        public void WhenSwitchedOffNothingIsEverWritten()
        {
            // The test arena and bots: the character is used but never saved.
            var autosave = new CharacterAutosave(_saves) { Enabled = false };
            Earn(Me, 60);

            autosave.Observe(_world.Events, Me);
            autosave.SaveIfDue(_world, Me, now: 10f);
            autosave.SaveNow(_world, Me);

            Assert.That(File.Exists(_saves.FilePath), Is.False);
        }
    }
}
