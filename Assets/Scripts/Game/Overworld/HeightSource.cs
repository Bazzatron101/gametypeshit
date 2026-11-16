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

        /// <summary>
        /// A bridge deck along the line (ticket 45): nothing within <see cref="HeightStamp.Radius"/> is lower than
        /// <see cref="HeightStamp.Height"/>, ramping down over <see cref="HeightStamp.Blend"/>. Over the sea or a
        /// river the deck stands above the water, so a road can cross.
        /// </summary>
        Bridge,
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
        [Tooltip("Optional: rivers and lakes painted white on black, stretched over the whole area like the painted heights. Water even above sea level (a river high in the hills), except where a road crosses it. Must be readable.")]
        public Texture2D paintedWater;

        public List<HeightStamp> stamps = new List<HeightStamp>();

        /// <summary>The generated height at world (x, z), for an area from <paramref name="areaMin"/> spanning <paramref name="areaSize"/> (for the painted map).</summary>
        public float Sample(float x, float z, Vector2 areaMin, Vector2 areaSize)
        {
            Prepare();
            var h = SampleWithoutRoads(x, z, areaMin, areaSize);

            // Roads level the finished hills across their width: the height on the road's centre line, at the nearest point.
            for (var i = 0; i < stamps.Count; i++)
            {
                var stamp = stamps[i];
                if (stamp == null || stamp.kind != HeightStampKind.Flatten || stamp.points == null || stamp.points.Count == 0 || !_bounds[i].Contains(x, z)) continue;
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
            if (_heightPixels != null)
            {
                var u = Mathf.Clamp01((x - areaMin.x) / areaSize.x);
                var v = Mathf.Clamp01((z - areaMin.y) / areaSize.y);
                h += _heightPixels.Bilinear(u, v) * paintedHeightScale;
            }

            for (var i = 0; i < stamps.Count; i++)
            {
                var stamp = stamps[i];
                if (stamp == null || stamp.points == null || stamp.points.Count == 0 || !_bounds[i].Contains(x, z)) continue;
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
                    case HeightStampKind.Bridge:
                    {
                        var d = DistanceToLine(p, stamp.points, out _);
                        var weight = 1f - Smooth(stamp.radius, stamp.radius + Mathf.Max(stamp.blend, 0.01f), d);
                        if (h < stamp.height) h = Mathf.Lerp(h, stamp.height, weight);
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

        /// <summary>
        /// Whether (x, z) is a river or lake on the painted water map (ticket 39). A road crossing it is a
        /// bridge or ford, so it is not water there.
        /// </summary>
        public bool IsPaintedWater(float x, float z, Vector2 areaMin, Vector2 areaSize)
        {
            Prepare();
            if (_waterPixels == null) return false;
            var u = (x - areaMin.x) / areaSize.x;
            var v = (z - areaMin.y) / areaSize.y;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return false;
            return _waterPixels.Bilinear(u, v) > 0.5f && DistanceToRoad(x, z) > 0f;
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

        /// <summary>The distance from <paramref name="x"/>, <paramref name="z"/> to the nearest road's edge (a flatten or bridge stamp); infinity with none within reach.</summary>
        public float DistanceToRoad(float x, float z)
        {
            var best = float.PositiveInfinity;
            Prepare();
            for (var i = 0; i < stamps.Count; i++)
            {
                var stamp = stamps[i];
                if (stamp == null || (stamp.kind != HeightStampKind.Flatten && stamp.kind != HeightStampKind.Bridge) || stamp.points == null || stamp.points.Count < 2) continue;
                if (!_bounds[i].Contains(x, z)) continue; // beyond this road's reach
                best = Mathf.Min(best, DistanceToLine(new Vector2(x, z), stamp.points, out _) - stamp.radius);
            }
            return best;
        }

        // --- Prepared once: stamp bounds and the painted maps' pixels -----------------------------------

        [NonSerialized] private bool _prepared;
        [NonSerialized] private Rect2[] _bounds;
        [NonSerialized] private Pixels _heightPixels, _waterPixels;

        /// <summary>
        /// Forget what was prepared (stamp bounds, the painted maps' pixels). Call after changing stamps or
        /// maps in code; the tools call it before each generation and bake, so Inspector edits are seen.
        /// </summary>
        public void Refresh() => _prepared = false;

        private void Prepare()
        {
            if (_prepared && _bounds != null && _bounds.Length == stamps.Count) return;
            _bounds = new Rect2[stamps.Count];
            for (var i = 0; i < stamps.Count; i++) _bounds[i] = Rect2.Around(stamps[i]);
            _heightPixels = Pixels.From(paintedHeights);
            _waterPixels = Pixels.From(paintedWater);
            _prepared = true;
        }

        /// <summary>Where a stamp can change anything (its points grown by its reach); the sea reaches everywhere.</summary>
        private readonly struct Rect2
        {
            private readonly float _minX, _minZ, _maxX, _maxZ;
            private Rect2(float minX, float minZ, float maxX, float maxZ) { _minX = minX; _minZ = minZ; _maxX = maxX; _maxZ = maxZ; }
            public bool Contains(float x, float z) => x >= _minX && x <= _maxX && z >= _minZ && z <= _maxZ;

            public static Rect2 Around(HeightStamp stamp)
            {
                if (stamp == null || stamp.points == null || stamp.points.Count == 0) return new Rect2(1, 1, 0, 0);
                if (stamp.kind == HeightStampKind.Sea) return new Rect2(float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity);
                var reach = stamp.radius + (stamp.kind == HeightStampKind.Pass || stamp.kind == HeightStampKind.Flatten || stamp.kind == HeightStampKind.Bridge ? Mathf.Max(stamp.blend, 0.01f) : 0f) + 0.01f;
                float minX = float.PositiveInfinity, minZ = float.PositiveInfinity, maxX = float.NegativeInfinity, maxZ = float.NegativeInfinity;
                foreach (var p in stamp.points)
                {
                    minX = Mathf.Min(minX, p.x); minZ = Mathf.Min(minZ, p.y);
                    maxX = Mathf.Max(maxX, p.x); maxZ = Mathf.Max(maxZ, p.y);
                }
                return new Rect2(minX - reach, minZ - reach, maxX + reach, maxZ + reach);
            }
        }

        /// <summary>
        /// A painted map's red channel, read once (a one-channel map such as R16 reports green and blue as 1),
        /// sampled like <see cref="Texture2D.GetPixelBilinear(float, float)"/> with clamped edges. Safe off the main thread.
        /// </summary>
        private sealed class Pixels
        {
            private readonly float[] _red;
            private readonly int _width, _height;

            private Pixels(float[] red, int width, int height) { _red = red; _width = width; _height = height; }

            public static Pixels From(Texture2D texture)
            {
                if (texture == null || !texture.isReadable) return null;
                var colours = texture.GetPixels();
                var red = new float[colours.Length];
                for (var i = 0; i < red.Length; i++) red[i] = colours[i].r;
                return new Pixels(red, texture.width, texture.height);
            }

            public float Bilinear(float u, float v)
            {
                var fx = Mathf.Clamp(u * _width - 0.5f, 0f, _width - 1);
                var fy = Mathf.Clamp(v * _height - 0.5f, 0f, _height - 1);
                var x0 = (int)fx; var y0 = (int)fy;
                var x1 = Math.Min(x0 + 1, _width - 1); var y1 = Math.Min(y0 + 1, _height - 1);
                var tx = fx - x0; var ty = fy - y0;
                var a = Mathf.Lerp(_red[y0 * _width + x0], _red[y0 * _width + x1], tx);
                var b = Mathf.Lerp(_red[y1 * _width + x0], _red[y1 * _width + x1], tx);
                return Mathf.Lerp(a, b, ty);
            }
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
