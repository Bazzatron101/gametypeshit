using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>Spawn placement for the test arena.</summary>
    public static class ArenaLayout
    {
        /// <summary><paramref name="count"/> points evenly around a circle on the floor, the first straight ahead (+Z).</summary>
        public static Vector3[] Ring(Vector3 centre, int count, float radius)
        {
            var points = new Vector3[Mathf.Max(0, count)];
            for (var i = 0; i < points.Length; i++)
            {
                var angle = i * Mathf.PI * 2f / points.Length;
                points[i] = new Vector3(centre.x + Mathf.Sin(angle) * radius, 0f, centre.z + Mathf.Cos(angle) * radius);
            }
            return points;
        }
    }
}
