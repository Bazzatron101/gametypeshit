using HunterSystem.Game;
using NUnit.Framework;
using UnityEngine;

namespace HunterSystem.Tests
{
    public class CameraMathTest
    {
        private static void AssertClose(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-3f), $"expected {expected}, got {actual}");

        [Test]
        public void StickForwardMovesTheWayTheCameraLooks()
        {
            var world = CameraMath.CameraRelativeMove(new Vector2(0f, 1f), cameraYawDegrees: 90f);

            AssertClose(new Vector3(world.x, 0f, world.y), new Vector3(1f, 0f, 0f));
        }

        [Test]
        public void StickRightIsTheCamerasRight()
        {
            var world = CameraMath.CameraRelativeMove(new Vector2(1f, 0f), cameraYawDegrees: 90f);

            AssertClose(new Vector3(world.x, 0f, world.y), new Vector3(0f, 0f, -1f));
        }

        [Test]
        public void CameraFacingNorthLeavesInputUnchanged()
        {
            var world = CameraMath.CameraRelativeMove(new Vector2(0.3f, -0.8f), cameraYawDegrees: 0f);

            AssertClose(new Vector3(world.x, 0f, world.y), new Vector3(0.3f, 0f, -0.8f));
        }

        [Test]
        public void OrbitSitsBehindAndAboveTheTarget()
        {
            var pos = CameraMath.OrbitPosition(new Vector3(2f, 0f, 3f), yawDegrees: 0f, pitchDegrees: 0f, distance: 5f);

            AssertClose(pos, new Vector3(2f, 0f, -2f));

            pos = CameraMath.OrbitPosition(Vector3.zero, yawDegrees: 0f, pitchDegrees: 90f, distance: 5f);
            AssertClose(pos, new Vector3(0f, 5f, 0f));
        }
    }
}
