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
    /// Checks the wiring in the real greybox scene: commands fed to the runner move the
    /// capsule on screen. Movement rules themselves are covered headlessly in EditMode.
    /// </summary>
    public class GreyboxCapsuleTest
    {
        private static readonly PlayerId LocalPlayer = new PlayerId(1);

        private SimulationRunner _runner;
        private PlayerView _view;

        [UnitySetUp]
        public IEnumerator LoadGreybox()
        {
            TestSaves.UseFreshFolder();
            yield return SceneManager.LoadSceneAsync("Greybox", LoadSceneMode.Single);
            _runner = Object.FindAnyObjectByType<SimulationRunner>();
            _view = Object.FindAnyObjectByType<PlayerView>();
            Assert.That(_runner, Is.Not.Null, "Greybox scene needs a SimulationRunner");
            Assert.That(_view, Is.Not.Null, "Greybox scene needs a PlayerView capsule");
        }

        [UnityTest]
        public IEnumerator CapsuleMovesWhenCommandsArrive()
        {
            _runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(new PlayerCommand(1f, 0f)));
            var start = _view.transform.position;
            var startTick = _runner.World.Tick;

            yield return new WaitForSeconds(0.5f);

            var end = _view.transform.position;
            Assert.That(_runner.World.Tick, Is.GreaterThan(startTick), "simulation ticked");
            Assert.That(end.x - start.x, Is.GreaterThan(1f), "capsule moved along +X");
            Assert.That(end.z, Is.EqualTo(start.z).Within(1e-3f), "and only along X");
        }

        [UnityTest]
        public IEnumerator CapsuleStaysPutWithoutInput()
        {
            _runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(PlayerCommand.None));
            var start = _view.transform.position;

            yield return new WaitForSeconds(0.25f);

            Assert.That(Vector3.Distance(_view.transform.position, start), Is.LessThan(1e-3f));
        }
    }
}
