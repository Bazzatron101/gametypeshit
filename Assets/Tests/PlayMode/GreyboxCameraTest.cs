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
    /// Checks the camera rig and heavy attack wiring in the real greybox scene. Orbit and
    /// camera-relative maths are covered in CameraMathTest; aim and heavy rules in SimulationAimAndHeavyTest.
    /// </summary>
    public class GreyboxCameraTest
    {
        private static readonly PlayerId LocalPlayer = new PlayerId(1);

        private SimulationRunner _runner;
        private PlayerView _player;
        private ThirdPersonCamera _camera;

        [UnitySetUp]
        public IEnumerator LoadGreybox()
        {
            TestSaves.UseFreshFolder();
            yield return SceneManager.LoadSceneAsync("Greybox", LoadSceneMode.Single);
            _runner = Object.FindAnyObjectByType<SimulationRunner>();
            _player = Object.FindAnyObjectByType<PlayerView>();
            _camera = Object.FindAnyObjectByType<ThirdPersonCamera>();
            Assert.That(_camera, Is.Not.Null, "Greybox scene needs a ThirdPersonCamera");
            Assert.That(_camera.Target, Is.EqualTo(_player.transform), "camera follows the player");
        }

        [UnityTest]
        public IEnumerator CameraFollowsThePlayerAtItsOrbitDistance()
        {
            _runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(new PlayerCommand(1f, 0f)));
            var startCamera = _camera.transform.position;

            yield return new WaitForSeconds(0.5f);

            var focus = _player.transform.position + Vector3.up * _camera.TargetHeight;
            Assert.That(Vector3.Distance(_camera.transform.position, focus), Is.EqualTo(_camera.Distance).Within(0.05f));
            Assert.That(_camera.transform.position.x - startCamera.x, Is.GreaterThan(1f), "moved with the player");
            var toPlayer = (focus - _camera.transform.position).normalized;
            Assert.That(Vector3.Dot(_camera.transform.forward, toPlayer), Is.GreaterThan(0.999f), "looking at the player");
        }

        [UnityTest]
        public IEnumerator HeavyAttackCommandSwingsAfterItsWindup()
        {
            var swung = false;
            _runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(new PlayerCommand(0f, 0f, heavyAttack: true)));

            var deadline = Time.time + 2f;
            while (!swung && Time.time < deadline)
            {
                yield return null;
                foreach (var e in _runner.FrameEvents)
                    if (e is AttackPerformed a && a.Kind == AttackKind.Heavy) swung = true;
            }

            Assert.That(swung, Is.True);
        }
    }
}
