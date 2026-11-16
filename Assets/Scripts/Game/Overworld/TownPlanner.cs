using System;
using System.Collections.Generic;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>What a place is, for its layout (ticket 49).</summary>
    public enum TownStyle
    {
        /// <summary>The guild town: the guild hall behind the board, an inn, a shop, a well, houses.</summary>
        GuildTown,

        /// <summary>A village: an inn, a well, a few houses.</summary>
        Village,

        /// <summary>A castle town: a keep in the middle, towers round it, a few houses.</summary>
        Castle,
    }

    /// <summary>
    /// Lays a town's buildings out round its square (ticket 49), at bake time. Buildings stand on rings round
    /// the centre with their doors to the square, keep off roads, the square and each other, and only go
    /// where the ground is buildable and flat enough. The same inputs always give the same town. The bake
    /// writes the result into the overworld, and blocks each footprint so nobody walks through a wall.
    /// </summary>
    public static class TownPlanner
    {
        public readonly struct Kind
        {
            public readonly string Id, Role;
            public readonly Vector2 Footprint;
            public Kind(string id, string role, Vector2 footprint) { Id = id; Role = role; Footprint = footprint; }
        }

        public readonly struct Placement
        {
            public readonly string Id;
            public readonly Vector2 Centre;
            /// <summary>Degrees about Y; 0 has the door facing +Z.</summary>
            public readonly float Yaw;
            public readonly Vector2 Footprint;
            public Placement(string id, Vector2 centre, float yaw, Vector2 footprint) { Id = id; Centre = centre; Yaw = yaw; Footprint = footprint; }
        }

        public sealed class Site
        {
            public Vector2 Centre;
            /// <summary>How far the town reaches: no building pokes out past it.</summary>
            public float Radius = 40f;
            public TownStyle Style;
            /// <summary>Open ground round the centre.</summary>
            public float SquareRadius = 12f;
            /// <summary>Further round spots to keep clear (the gate board), with their radius.</summary>
            public List<(Vector2 at, float radius)> KeepClear = new List<(Vector2, float)>();
            /// <summary>How far a point is outside the nearest road; null for no roads.</summary>
            public Func<float, float, float> DistanceToRoad;
            public float RoadClearance = 3f;
            public float Gap = 3f;
            /// <summary>The ground's height, to keep buildings to flat ground.</summary>
            public Func<float, float, float> HeightAt;
            /// <summary>Whether a building may stand on a point (dry, walkable land); null for anywhere.</summary>
            public Func<float, float, bool> Buildable;
            /// <summary>Most the ground may rise or fall under one building, in metres.</summary>
            public float MaxRise = 1.5f;
        }

        /// <summary>The roles a style asks for, in order of importance; repeated houses fill what space is left.</summary>
        private static string[] Wanted(TownStyle style) => style switch
        {
            TownStyle.GuildTown => new[] { "hall", "inn", "shop", "well" },
            TownStyle.Castle => new[] { "keep", "tower", "tower", "tower", "tower", "inn" },
            _ => new[] { "inn", "well" },
        };

        private static int MaxHouses(TownStyle style) => style == TownStyle.GuildTown ? 14 : style == TownStyle.Castle ? 5 : 7;

        public static List<Placement> Plan(Site site, IReadOnlyList<Kind> kinds)
        {
            var placed = new List<Placement>();
            if (kinds == null || kinds.Count == 0) return placed;
            var seed = (int)EnvironmentScatter.Hash(Mathf.RoundToInt(site.Centre.x), Mathf.RoundToInt(site.Centre.y), 49, 0);
            var houseIndex = 0;
            foreach (var role in Wanted(site.Style))
            {
                var kind = First(kinds, role);
                if (kind.HasValue) TryPlace(site, kind.Value, placed, PreferredAngle(role, site.Style, seed, placed.Count));
            }
            var houses = new List<Kind>();
            foreach (var k in kinds) if (k.Role == "house") houses.Add(k);
            for (var n = 0; n < MaxHouses(site.Style) && houses.Count > 0; n++)
            {
                var house = houses[(int)(EnvironmentScatter.Hash(seed, 7, houseIndex++, 0) % (uint)houses.Count)];
                if (!TryPlace(site, house, placed, PreferredAngle("house", site.Style, seed, n))) break; // the town is full
            }
            return placed;
        }

        private static Kind? First(IReadOnlyList<Kind> kinds, string role)
        {
            foreach (var k in kinds) if (k.Role == role) return k;
            return null;
        }

        /// <summary>Radians round the centre, 0 at +X, π/2 at +Z (behind the board, which is north of the centre).</summary>
        private static float PreferredAngle(string role, TownStyle style, int seed, int n)
        {
            switch (role)
            {
                case "hall": return Mathf.PI / 2f; // behind the board
                case "inn": return Mathf.PI * 0.9f;
                case "shop": return Mathf.PI * 0.1f;
                case "well": return Mathf.PI * 1.25f;
                case "keep": return 0f;
                case "tower": return Mathf.PI / 4f + n * Mathf.PI / 2f;
                default: return EnvironmentScatter.Unit(EnvironmentScatter.Hash(seed, 11, n, 0)) * 2f * Mathf.PI;
            }
        }

        private static bool TryPlace(Site site, Kind kind, List<Placement> placed, float startAngle)
        {
            // The keep stands in the middle of a castle town; the well in the square, off the centre.
            if (kind.Role == "keep")
                return Accept(site, kind, placed, site.Centre, 0f, ignoreSquare: true);
            if (kind.Role == "well")
            {
                var r = site.SquareRadius * 0.7f;
                for (var step = 0; step < 16; step++)
                {
                    var a = startAngle + step * Mathf.PI / 8f;
                    if (Accept(site, kind, placed, site.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 0f, ignoreSquare: true)) return true;
                }
                return false;
            }

            var depth = Mathf.Max(kind.Footprint.x, kind.Footprint.y);
            for (var ring = site.SquareRadius + depth / 2f + site.Gap; ring + depth / 2f <= site.Radius; ring += depth + site.Gap)
            {
                var step = Mathf.Clamp(1f / ring, 0.01f, 0.2f); // about a metre along the ring
                for (var t = 0f; t < 2f * Mathf.PI; t += step)
                {
                    // Outward from the preferred angle, alternating sides, so a building lands as near it as it can.
                    var offset = (t / step) % 2f < 1f ? t / 2f : -t / 2f;
                    var a = startAngle + offset;
                    var at = site.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring;
                    var yaw = Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg; // the door to the square
                    if (Accept(site, kind, placed, at, yaw, ignoreSquare: false)) return true;
                }
            }
            return false;
        }

        private static bool Accept(Site site, Kind kind, List<Placement> placed, Vector2 at, float yaw, bool ignoreSquare)
        {
            var candidate = new Placement(kind.Id, at, yaw, kind.Footprint);
            var corners = Corners(candidate, 0f);
            foreach (var c in corners)
                if ((c - site.Centre).sqrMagnitude > site.Radius * site.Radius) return false;

            // Off roads, on buildable and flat enough ground: the corners, the edge middles and the centre.
            var samples = new List<Vector2>(corners) { at };
            for (var i = 0; i < 4; i++) samples.Add((corners[i] + corners[(i + 1) % 4]) / 2f);
            float low = float.MaxValue, high = float.MinValue;
            foreach (var p in samples)
            {
                if (site.DistanceToRoad != null && site.DistanceToRoad(p.x, p.y) < site.RoadClearance) return false;
                if (site.Buildable != null && !site.Buildable(p.x, p.y)) return false;
                if (site.HeightAt != null)
                {
                    var h = site.HeightAt(p.x, p.y);
                    low = Mathf.Min(low, h);
                    high = Mathf.Max(high, h);
                }
            }
            if (site.HeightAt != null && high - low > site.MaxRise) return false;

            if (!ignoreSquare && DistanceToRect(site.Centre, candidate) < site.SquareRadius) return false;
            foreach (var (spot, radius) in site.KeepClear)
                if (DistanceToRect(spot, candidate) < radius) return false;
            foreach (var other in placed)
                if (Overlap(candidate, other, site.Gap)) return false;
            placed.Add(candidate);
            return true;
        }

        // --- Footprint geometry (also used to block footprints and to find what stands under a point) ------

        /// <summary>The footprint's corners, grown by <paramref name="grow"/> on every side.</summary>
        public static Vector2[] Corners(Placement p, float grow)
        {
            Axes(p.Yaw, out var right, out var forward);
            var hx = p.Footprint.x / 2f + grow;
            var hz = p.Footprint.y / 2f + grow;
            return new[]
            {
                p.Centre - right * hx - forward * hz, p.Centre + right * hx - forward * hz,
                p.Centre + right * hx + forward * hz, p.Centre - right * hx + forward * hz,
            };
        }

        /// <summary>The building's local +X and +Z on the ground, for a yaw in degrees (Unity's: +Z turns towards +X).</summary>
        public static void Axes(float yawDegrees, out Vector2 right, out Vector2 forward)
        {
            var r = yawDegrees * Mathf.Deg2Rad;
            forward = new Vector2(Mathf.Sin(r), Mathf.Cos(r));
            right = new Vector2(Mathf.Cos(r), -Mathf.Sin(r));
        }

        /// <summary>Whether (x, z) is inside the footprint grown by <paramref name="grow"/>.</summary>
        public static bool Contains(Vector2 centre, float yawDegrees, Vector2 footprint, float x, float z, float grow = 0f)
        {
            Axes(yawDegrees, out var right, out var forward);
            var d = new Vector2(x - centre.x, z - centre.y);
            return Mathf.Abs(Vector2.Dot(d, right)) <= footprint.x / 2f + grow && Mathf.Abs(Vector2.Dot(d, forward)) <= footprint.y / 2f + grow;
        }

        private static float DistanceToRect(Vector2 point, Placement p)
        {
            Axes(p.Yaw, out var right, out var forward);
            var d = point - p.Centre;
            var dx = Mathf.Max(0f, Mathf.Abs(Vector2.Dot(d, right)) - p.Footprint.x / 2f);
            var dz = Mathf.Max(0f, Mathf.Abs(Vector2.Dot(d, forward)) - p.Footprint.y / 2f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Two footprints, each grown by half the gap, overlap (separating axes).</summary>
        private static bool Overlap(Placement a, Placement b, float gap)
        {
            var ca = Corners(a, gap / 2f);
            var cb = Corners(b, gap / 2f);
            Axes(a.Yaw, out var ar, out var af);
            Axes(b.Yaw, out var br, out var bf);
            foreach (var axis in new[] { ar, af, br, bf })
            {
                Project(ca, axis, out var minA, out var maxA);
                Project(cb, axis, out var minB, out var maxB);
                if (maxA < minB || maxB < minA) return false;
            }
            return true;
        }

        private static void Project(Vector2[] corners, Vector2 axis, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            foreach (var c in corners)
            {
                var d = Vector2.Dot(c, axis);
                min = Mathf.Min(min, d);
                max = Mathf.Max(max, d);
            }
        }
    }
}
