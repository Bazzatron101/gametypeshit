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
    /// Checks the defensive wiring in the real greybox scene: the Goblin's attacks land on the
    /// player, and a dodge command reaches the simulation and dashes the capsule. Window timing
    /// is covered headlessly in SimulationDefenseTest.
    /// </summary>
    public class GreyboxDefenseTest
    {
        private static readonly PlayerId LocalPlayer = new PlayerId(1);

        private SimulationRunner _runner;
        private PlayerView _player;

        [UnitySetUp]
        public IEnumerator LoadGreybox()
        {
            TestSaves.UseFreshFolder();
            yield return SceneManager.LoadSceneAsync("Greybox", LoadSceneMode.Single);
            _runner = Object.FindAnyObjectByType<SimulationRunner>();
            _player = Object.FindAnyObjectByType<PlayerView>();
        }

        [UnityTest]
        public IEnumerator GoblinAttacksDamageAPlayerWhoStandsStill()
        {
            _runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(PlayerCommand.None));

            var deadline = Time.time + 8f;
            while (_runner.World.GetPlayer(LocalPlayer).Health == 100 && Time.time < deadline) yield return null;

            Assert.That(_runner.World.GetPlayer(LocalPlayer).Health, Is.LessThan(100));
        }

        [UnityTest]
        public IEnumerator DodgeCommandDashesTheCapsule()
        {
            _runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(new PlayerCommand(1f, 0f, dodge: true)));
            var start = _player.transform.position;

            yield return new WaitForSeconds(0.3f); // longer than the 0.25 s dash

            // One dash (3 m) plus a little walking once it ends; held dodge is limited by the cooldown.
            Assert.That(_player.transform.position.x - start.x, Is.GreaterThan(2.9f));
            Assert.That(_player.transform.position.x - start.x, Is.LessThan(4f));
        }
    }
}
