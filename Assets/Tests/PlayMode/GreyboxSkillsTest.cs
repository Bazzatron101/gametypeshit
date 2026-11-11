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
    /// Checks skill wiring in the real greybox scene: the skill assets load into the bar, a skill
    /// command fires, and a Fire Bolt appears in flight. Skill rules are covered in SimulationSkillsTest.
    /// </summary>
    public class GreyboxSkillsTest
    {
        private static readonly PlayerId LocalPlayer = new PlayerId(1);

        private SimulationRunner _runner;

        [UnitySetUp]
        public IEnumerator LoadGreybox()
        {
            yield return SceneManager.LoadSceneAsync("Greybox", LoadSceneMode.Single);
            _runner = Object.FindAnyObjectByType<SimulationRunner>();
        }

        [Test]
        public void TheStarterSkillAssetsFillTheBar()
        {
            var names = _runner.World.GetPlayer(LocalPlayer).SkillSlots.Select(s => s.Skill?.Name).ToArray();

            Assert.That(names, Is.EqualTo(new[] { "Rending Slash", "Ground Slam", "Fire Bolt", null, null, null }));
        }

        [UnityTest]
        public IEnumerator FireBoltFliesOnScreen()
        {
            var effects = Object.FindAnyObjectByType<SkillEffectsView>();
            Assert.That(effects, Is.Not.Null, "Greybox scene needs the skill visuals");
            _runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(new PlayerCommand(0f, 0f, useSkillSlot: 2)));

            var used = false;
            var deadline = Time.time + 1f;
            while (effects.ActiveProjectileVisuals == 0 && Time.time < deadline)
            {
                yield return null;
                used |= _runner.FrameEvents.OfType<SkillUsed>().Any(e => e.Skill.Id == "fire-bolt");
            }

            Assert.That(used, Is.True, "the skill fired");
            Assert.That(effects.ActiveProjectileVisuals, Is.GreaterThan(0), "and its bolt is visible");
        }
    }
}
