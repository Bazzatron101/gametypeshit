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
    /// Checks the demo synergy in the real greybox scene: toggling Split Shot through the debug
    /// toggles makes one Fire Bolt press put three bolts on screen. Modifier rules are covered in
    /// SimulationModifiersTest.
    /// </summary>
    public class GreyboxModifiersTest
    {
        private static readonly PlayerId LocalPlayer = new PlayerId(1);

        [UnityTest]
        public IEnumerator SplitShotTurnsOneFireBoltIntoThree()
        {
            yield return SceneManager.LoadSceneAsync("Greybox", LoadSceneMode.Single);
            var runner = Object.FindAnyObjectByType<SimulationRunner>();
            var toggles = Object.FindAnyObjectByType<DebugModifierToggles>();
            var effects = Object.FindAnyObjectByType<SkillEffectsView>();
            Assert.That(toggles, Is.Not.Null, "Greybox scene needs the modifier toggles");
            yield return null; // let Start run

            var splitShot = Enumerable.Range(0, toggles.Count).Single(i => toggles[i].Id == "split-shot");
            Assert.That(toggles.Toggle(splitShot), Is.True, "Split Shot will be on");
            var until = Time.time + 1f;
            while (!toggles.IsActive(splitShot) && Time.time < until) yield return null; // applies on the next tick
            Assert.That(runner.World.GetPlayer(LocalPlayer).ActiveModifiers.Select(m => m.Id), Has.Some.EqualTo("split-shot"));

            runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(new PlayerCommand(0f, 0f, useSkillSlot: 2)));
            var deadline = Time.time + 1f;
            while (effects.ActiveProjectileVisuals < 3 && Time.time < deadline) yield return null;

            Assert.That(effects.ActiveProjectileVisuals, Is.EqualTo(3));
        }
    }
}
