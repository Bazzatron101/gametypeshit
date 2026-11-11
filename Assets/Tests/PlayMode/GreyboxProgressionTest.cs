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
    /// Checks progression wiring in the real greybox scene: clearing the Goblins levels the
    /// player up, the System announces it, and stat points spent through the runner land in the
    /// simulation. The rules themselves are covered headlessly in SimulationProgressionTest.
    /// </summary>
    public class GreyboxProgressionTest
    {
        private static readonly PlayerId LocalPlayer = new PlayerId(1);

        [UnityTest]
        public IEnumerator ClearingTheGoblinsLevelsUpAndTheSystemAnnouncesIt()
        {
            yield return SceneManager.LoadSceneAsync("Greybox", LoadSceneMode.Single);
            var runner = Object.FindAnyObjectByType<SimulationRunner>();
            var hud = Object.FindAnyObjectByType<SystemHud>();
            Assert.That(hud, Is.Not.Null, "Greybox scene needs the System HUD");
            var goblins = Object.FindObjectsByType<EnemyView>(FindObjectsSortMode.None);
            Assert.That(goblins.Length, Is.EqualTo(3));

            // Hold attack and let the Goblins come; soft lock-on aims each swing.
            runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(new PlayerCommand(0f, 0f, lightAttack: true)));
            var deadline = Time.time + 20f;
            while (runner.World.GetPlayer(LocalPlayer).Level < 2 && Time.time < deadline) yield return null;

            var state = runner.World.GetPlayer(LocalPlayer);
            Assert.That(state.Level, Is.EqualTo(2), $"defeated {goblins.Count(g => g.ShowsDefeated)} of 3 Goblins");
            Assert.That(state.UnspentStatPoints, Is.EqualTo(3));
            yield return null; // let the HUD read this frame's events
            var announcement = hud.CurrentAnnouncement;
            Assert.That(announcement.HasValue, Is.True, "the System announced the level-up");
            Assert.That(announcement.Value.Title, Is.EqualTo("LEVEL UP"));
            StringAssert.Contains("level 2", announcement.Value.Body);

            runner.RequestStatAllocation(LocalPlayer, Stat.Vitality);
            runner.RequestStatAllocation(LocalPlayer, Stat.Vitality);
            yield return new WaitForSeconds(0.2f);

            state = runner.World.GetPlayer(LocalPlayer);
            Assert.That(state.Stats[Stat.Vitality], Is.EqualTo(12));
            Assert.That(state.UnspentStatPoints, Is.EqualTo(1));
            Assert.That(state.MaxHealth, Is.EqualTo(120));
        }
    }
}
