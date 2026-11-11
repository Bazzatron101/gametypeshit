using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>Pure maths for the third-person camera and camera-relative movement.</summary>
    public static class CameraMath
    {
        /// <summary>
        /// Rotates stick input (x right, y forward) by the camera's yaw, so "forward" is where the
        /// camera looks. Returns the world floor direction as (x, z).
        /// </summary>
        public static Vector2 CameraRelativeMove(Vector2 stick, float cameraYawDegrees)
        {
            var yaw = cameraYawDegrees * Mathf.Deg2Rad;
            var sin = Mathf.Sin(yaw);
            var cos = Mathf.Cos(yaw);
            // Camera forward on the floor is (sin, cos); camera right is (cos, -sin).
            return new Vector2(stick.x * cos + stick.y * sin, -stick.x * sin + stick.y * cos);
        }

        /// <summary>Where a camera orbiting <paramref name="target"/> sits: behind it by yaw, raised by pitch.</summary>
        public static Vector3 OrbitPosition(Vector3 target, float yawDegrees, float pitchDegrees, float distance) =>
            target + Quaternion.Euler(pitchDegrees, yawDegrees, 0f) * new Vector3(0f, 0f, -distance);
    }
}
