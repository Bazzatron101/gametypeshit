using System.Collections;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Drives the test arena through its panel's actions: spawn chosen enemies, set the level, set
    /// the loadout, and try a skill with a modifier, all in one scene. The underlying world
    /// operations are covered headlessly in SimulationArenaControlsTest.
    /// </summary>
    public class ArenaTest
    {
        private static readonly PlayerId LocalPlayer = new PlayerId(1);

        private SimulationRunner _runner;
        private ArenaPanel _panel;

        [UnitySetUp]
        public IEnumerator LoadArena()
        {
            yield return SceneManager.LoadSceneAsync("Arena", LoadSceneMode.Single);
            _runner = Object.FindAnyObjectByType<SimulationRunner>();
            _panel = Object.FindAnyObjectByType<ArenaPanel>();
            Assert.That(_panel, Is.Not.Null, "Arena scene needs the arena panel");
            Assert.That(_runner.Content, Is.Not.Null, "the arena uses the game's content list");
            _runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(PlayerCommand.None));
            yield return null; // let Start run
        }

        private IEnumerator Until(System.Func<bool> condition, float seconds = 2f)
        {
            var deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
        }

        private PlayerState Player => _runner.World.GetPlayer(LocalPlayer);

        [UnityTest]
        public IEnumerator SpawnsAChosenEnemyTypeAndClearsThem()
        {
            var hobgoblin = _runner.Content.Enemies.Single(e => e.name == "Hobgoblin");

            _panel.SpawnRing(hobgoblin, 3);
            yield return null;

            Assert.That(_runner.World.EnemyIds.Count, Is.EqualTo(3));
            Assert.That(_runner.World.EnemyIds.Select(id => _runner.World.GetEnemy(id).Definition.Name), Is.All.EqualTo("Hobgoblin"));
            Assert.That(Object.FindObjectsByType<EnemyView>(FindObjectsSortMode.None).Length, Is.EqualTo(3), "each has a visual");

            _panel.ClearEnemies();
            yield return null;

            Assert.That(_runner.World.EnemyIds, Is.Empty);
            Assert.That(Object.FindObjectsByType<EnemyView>(FindObjectsSortMode.None), Is.Empty);
        }

        [UnityTest]
        public IEnumerator SetsTheLevelThroughTheRealPathIncludingUnlocks()
        {
            _panel.LevelTo(5);
            yield return Until(() => Player.Level == 5);

            Assert.That(Player.Level, Is.EqualTo(5));
            Assert.That(Player.KnownSkills.Select(s => s.Id), Has.Some.EqualTo("flame-burst"), "the level 3 unlock happened");
        }

        [UnityTest]
        public IEnumerator SetsTheLoadoutAndTriesASkillWithAModifier()
        {
            _panel.TeachAllSkills();
            yield return Until(() => Player.KnownSkills.Count == _runner.Content.Skills.Count);

            _panel.AssignSlot(5, "fire-bolt");
            yield return Until(() => Player.SkillSlots[5].Skill?.Id == "fire-bolt");
            Assert.That(Player.SkillSlots[5].Skill?.Id, Is.EqualTo("fire-bolt"), "moved to key 6");

            var splitShot = _runner.Content.Modifiers.Single(m => m.Id == "split-shot");
            _panel.ToggleModifier(splitShot);
            yield return Until(() => _panel.IsModifierActive(splitShot));

            _runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(new PlayerCommand(0f, 0f, useSkillSlot: 5)));
            yield return Until(() => _runner.World.Projectiles.Count >= 3);

            Assert.That(_runner.World.Projectiles.Count, Is.EqualTo(3), "Fire Bolt from slot 6, split three ways");
        }
    }
}
