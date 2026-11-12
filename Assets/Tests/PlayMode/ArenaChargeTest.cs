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
    /// Charged skills and the new unlocks with the game's real data, in the test arena. The rules
    /// are covered headlessly in SimulationChargeTest.
    /// </summary>
    public class ArenaChargeTest
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
            _runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(PlayerCommand.None));
            yield return null;
        }

        private IEnumerator Until(System.Func<bool> condition, float seconds = 2f)
        {
            var deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
        }

        private PlayerState Player => _runner.World.GetPlayer(LocalPlayer);

        [UnityTest]
        public IEnumerator HoldingGroundSlamChargesItAndLettingGoFiresABiggerSlam()
        {
            _panel.LevelTo(6); // Ground Slam's charge unlocks at 6
            yield return Until(() => Player.Level == 6);
            Assert.That(Player.SkillSlots[1].ChargeUnlocked, Is.True, "Ground Slam (key 2) can charge");

            _runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(new PlayerCommand(0f, 0f, useSkillSlot: 1, heldSkillSlots: 1 << 1)));
            yield return Until(() => Player.ChargingSlot == 1);
            yield return new WaitForSeconds(0.8f); // past the 0.15 s tap grace and well into the charge

            SkillUsed slam = null;
            _runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(PlayerCommand.None)); // let go
            var deadline = Time.time + 1f;
            while (slam == null && Time.time < deadline)
            {
                yield return null;
                slam = _runner.FrameEvents.OfType<SkillUsed>().FirstOrDefault(e => e.Skill.Id == "ground-slam");
            }

            Assert.That(slam, Is.Not.Null, "the slam fired on release");
            Assert.That(slam.ChargeFraction, Is.GreaterThan(0.4f));
            Assert.That(slam.Radius, Is.GreaterThan(3.5f * 1.4f), "bigger than the uncharged 3.5 m");
        }

        [UnityTest]
        public IEnumerator TheNewSkillsUnlockAtTheirLevels()
        {
            _panel.LevelTo(20);
            yield return Until(() => Player.Level == 20);

            Assert.That(Player.KnownSkills.Select(s => s.Id),
                Is.SupersetOf(new[] { "hunters-javelin", "whirlwind", "inferno-wave", "monarchs-decree" }));
            var unlockLevels = _runner.Content.Skills.ToDictionary(s => s.Id, s => s.ToDefinition().UnlockLevel);
            Assert.That((unlockLevels["hunters-javelin"], unlockLevels["whirlwind"], unlockLevels["inferno-wave"], unlockLevels["monarchs-decree"]),
                Is.EqualTo((5, 10, 15, 20)));
        }
    }
}
