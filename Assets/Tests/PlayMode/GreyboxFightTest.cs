using System.Collections;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Checks the first fight in the real greybox scene: the Goblin closes in, the player's
    /// light attacks land, and the defeated Goblin disappears. Damage rules are covered headlessly.
    /// </summary>
    public class GreyboxFightTest
    {
        private static readonly PlayerId LocalPlayer = new PlayerId(1);

        [UnityTest]
        public IEnumerator GoblinChasesInAndIsDefeatedByLightAttacks()
        {
            yield return SceneManager.LoadSceneAsync("Greybox", LoadSceneMode.Single);
            var runner = Object.FindAnyObjectByType<SimulationRunner>();
            var goblin = Object.FindAnyObjectByType<EnemyView>();
            Assert.That(goblin, Is.Not.Null, "Greybox scene needs a Goblin EnemyView");
            var start = goblin.transform.position;

            // Holding attack: the cooldown, not the input, sets the pace.
            runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(new PlayerCommand(0f, 0f, lightAttack: true)));

            var deadline = Time.time + 10f;
            while (!goblin.ShowsDefeated && Time.time < deadline) yield return null;

            Assert.That(goblin.ShowsDefeated, Is.True, "Goblin body hidden after defeat");
            Assert.That(runner.World.GetEnemy(goblin.Id).IsDefeated, Is.True);
            Assert.That(Vector3.Distance(goblin.transform.position, start), Is.GreaterThan(5f), "it chased in from 8 m");
        }
    }
}
