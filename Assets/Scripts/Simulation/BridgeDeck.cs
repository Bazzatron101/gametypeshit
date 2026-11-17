using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// A bridge over water (developer playtest, 2026-11-17): a walkable deck along a line of points, each at
    /// its own height, <see cref="HalfWidth"/> each side. The ground under it is left as it is, so a river
    /// keeps flowing under the deck; on the deck, the ground's height is the deck's (where the deck is higher)
    /// and it can be walked. The ends ramp down to the banks.
    /// </summary>
    public sealed class BridgeDeck
    {
        private readonly Vec3[] _points;
        private readonly float _minX, _maxX, _minZ, _maxZ;

        public BridgeDeck(IReadOnlyList<Vec3> points, float halfWidth)
        {
            if (points == null || points.Count < 2) throw new ArgumentException("A bridge needs at least two points.", nameof(points));
            if (halfWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(halfWidth));
            _points = new Vec3[points.Count];
            for (var i = 0; i < points.Count; i++) _points[i] = points[i];
            HalfWidth = halfWidth;
            _minX = _minZ = float.PositiveInfinity;
            _maxX = _maxZ = float.NegativeInfinity;
            foreach (var p in _points)
            {
                _minX = Math.Min(_minX, p.X - halfWidth); _maxX = Math.Max(_maxX, p.X + halfWidth);
                _minZ = Math.Min(_minZ, p.Z - halfWidth); _maxZ = Math.Max(_maxZ, p.Z + halfWidth);
            }
        }

        public IReadOnlyList<Vec3> Points => _points;
        public float HalfWidth { get; }

        /// <summary>The deck's height above (x, z), or null when (x, z) is not on the deck.</summary>
        public float? HeightAt(float x, float z)
        {
            if (x < _minX || x > _maxX || z < _minZ || z > _maxZ) return null;
            float? best = null;
            var bestDistance = HalfWidth;
            for (var i = 0; i + 1 < _points.Length; i++)
            {
                var a = _points[i];
                var b = _points[i + 1];
                var abX = b.X - a.X;
                var abZ = b.Z - a.Z;
                var lengthSq = abX * abX + abZ * abZ;
                var t = lengthSq > 0f ? Math.Max(0f, Math.Min(1f, ((x - a.X) * abX + (z - a.Z) * abZ) / lengthSq)) : 0f;
                var dx = x - (a.X + abX * t);
                var dz = z - (a.Z + abZ * t);
                var distance = (float)Math.Sqrt(dx * dx + dz * dz);
                if (distance > bestDistance) continue;
                bestDistance = distance;
                best = a.Y + (b.Y - a.Y) * t;
            }
            return best;
        }
    }
}
