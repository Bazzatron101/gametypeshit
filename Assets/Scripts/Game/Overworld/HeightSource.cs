using System;
using System.Collections.Generic;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>What a <see cref="HeightStamp"/> does to the ground.</summary>
    public enum HeightStampKind
    {
        /// <summary>A rounded rise: <see cref="HeightStamp.Height"/> at the first point, falling to nothing at <see cref="HeightStamp.Radius"/>.</summary>
        Hill,

        /// <summary>A long rise along the points: <see cref="HeightStamp.Height"/> on the line, falling to nothing over <see cref="HeightStamp.Radius"/> either side. Steep when narrow.</summary>
        Ridge,

        /// <summary>A notch through high ground at the first point: nothing is higher than <see cref="HeightStamp.Height"/> within <see cref="HeightStamp.Radius"/>, easing back over <see cref="HeightStamp.Blend"/>.</summary>
        Pass,

        /// <summary>The sea, beyond the line from the first point to the second (on its right, walking along it): the ground drops <see cref="HeightStamp.Height"/> metres over <see cref="HeightStamp.Radius"/>, a shore.</summary>
        Sea,

        /// <summary>Levels the ground across the line through the points (a road), <see cref="HeightStamp.Radius"/> wide each side, easing back over <see cref="HeightStamp.Blend"/>.</summary>
        Flatten,

        /// <summary>Marks ground as climbable within <see cref="HeightStamp.Radius"/> of the line (ticket 36). Does not change the height.</summary>
        Climbable,
    }

    /// <summary>One shape laid on the generated ground. Points are world X/Z.</summary>
    [Serializable]
    public sealed class HeightStamp
    {
        public string label;
        public HeightStampKind kind;
        public List<Vector2> points = new List<Vector2>();
        [Min(0f)] public float radius = 50f;
        public float height = 20f;
        [Min(0f)] public float blend = 10f;
    }

    /// <summary>
    /// Where the overworld's heights come from, before any hand edits (ticket 39): a base height, gentle
    /// noise, an optional painted heightmap the developer can trace from the reference map, and stamps
    /// (hills, ridges, passes, the sea, roads) laid on top in order. Pure maths, so tools and tests agree.
    /// </summary>
    [Serializable]
    public sealed class HeightSource
    {
        public float baseHeight = 10f;
        [Min(0f)] public float noiseAmplitude = 2f;
        [Min(1f)] public float noiseScale = 120f;
        public int noiseSeed = 1;

        [Tooltip("Optional: a greyscale heightmap painted or traced from the reference map, stretched over the whole area. Must be readable.")]
        public Texture2D paintedHeights;
        [Tooltip("Metres the painted map's white adds over its black")] public float paintedHeightScale = 60f;

        public List<HeightStamp> stamps = new List<HeightStamp>();

        /// <summary>The generated height at world (x, z), for an area from <paramref name="areaMin"/> spanning <paramref name="areaSize"/> (for the painted map).</summary>
        public float Sample(float x, float z, Vector2 areaMin, Vector2 areaSize)
        {
            var h = SampleWithoutRoads(x, z, areaMin, areaSize);

            // Roads level the finished hills across their width: the height on the road's centre line, at the nearest point.
            foreach (var stamp in stamps)
            {
                if (stamp == null || stamp.kind != HeightStampKind.Flatten || stamp.points == null || stamp.points.Count == 0) continue;
                var d = DistanceToLine(new Vector2(x, z), stamp.points, out var nearest);
                var weight = 1f - Smooth(stamp.radius, stamp.radius + Mathf.Max(stamp.blend, 0.01f), d);
                if (weight <= 0f) continue;
                h = Mathf.Lerp(h, SampleWithoutRoads(nearest.x, nearest.y, areaMin, areaSize), weight);
            }
            return h;
        }

        private float SampleWithoutRoads(float x, float z, Vector2 areaMin, Vector2 areaSize)
        {
            var h = baseHeight;
            if (noiseAmplitude > 0f)
                h += (Mathf.PerlinNoise(x / noiseScale + noiseSeed * 17.31f, z / noiseScale + noiseSeed * 5.77f) - 0.5f) * 2f * noiseAmplitude;
            if (paintedHeights != null && paintedHeights.isReadable)
            {
                var u = Mathf.Clamp01((x - areaMin.x) / areaSize.x);
                var v = Mathf.Clamp01((z - areaMin.y) / areaSize.y);
                h += paintedHeights.GetPixelBilinear(u, v).grayscale * paintedHeightScale;
            }

            foreach (var stamp in stamps)
            {
                if (stamp == null || stamp.points == null || stamp.points.Count == 0) continue;
                var p = new Vector2(x, z);
                switch (stamp.kind)
                {
                    case HeightStampKind.Hill:
                        h += stamp.height * Bump(Vector2.Distance(p, stamp.points[0]), stamp.radius);
                        break;
                    case HeightStampKind.Ridge:
                        h += stamp.height * Bump(DistanceToLine(p, stamp.points, out _), stamp.radius);
                        break;
                    case HeightStampKind.Pass:
                    {
                        var d = Vector2.Distance(p, stamp.points[0]);
                        var weight = 1f - Smooth(stamp.radius, stamp.radius + Mathf.Max(stamp.blend, 0.01f), d);
                        if (h > stamp.height) h = Mathf.Lerp(h, stamp.height, weight);
                        break;
                    }
                    case HeightStampKind.Sea:
                    {
                        if (stamp.points.Count < 2) break;
                        var a = stamp.points[0];
                        var dir = (stamp.points[1] - a).normalized;
                        var right = new Vector2(dir.y, -dir.x);
                        var beyond = Vector2.Dot(p - a, right); // metres out to sea
                        h -= stamp.height * Smooth(0f, Mathf.Max(stamp.radius, 0.01f), beyond);
                        break;
                    }
                }
            }
            return h;
        }

        /// <summary>Whether (x, z) is inside a climbable stamp.</summary>
        public bool IsClimbable(float x, float z)
        {
            foreach (var stamp in stamps)
                if (stamp != null && stamp.kind == HeightStampKind.Climbable && stamp.points != null && stamp.points.Count > 0 &&
                    DistanceToLine(new Vector2(x, z), stamp.points, out _) <= stamp.radius)
                    return true;
            return false;
        }

        /// <summary>The distance from <paramref name="x"/>, <paramref name="z"/> to the nearest road (a flatten stamp's line); infinity with none.</summary>
        public float DistanceToRoad(float x, float z)
        {
            var best = float.PositiveInfinity;
            foreach (var stamp in stamps)
                if (stamp != null && stamp.kind == HeightStampKind.Flatten && stamp.points != null && stamp.points.Count > 1)
                    best = Mathf.Min(best, DistanceToLine(new Vector2(x, z), stamp.points, out _) - stamp.radius);
            return best;
        }

        /// <summary>1 at the centre, easing to 0 at <paramref name="radius"/>.</summary>
        private static float Bump(float distance, float radius) => radius <= 0f ? 0f : 1f - Smooth(0f, radius, distance);

        private static float Smooth(float from, float to, float value)
        {
            var t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        /// <summary>The distance from <paramref name="p"/> to the polyline through <paramref name="points"/> (a single point is a dot).</summary>
        public static float DistanceToLine(Vector2 p, IReadOnlyList<Vector2> points, out Vector2 nearest)
        {
            nearest = points[0];
            var best = Vector2.Distance(p, nearest);
            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                var ab = b - a;
                var t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                var q = a + ab * t;
                var d = Vector2.Distance(p, q);
                if (d >= best) continue;
                best = d;
                nearest = q;
            }
            return best;
        }
    }
}
