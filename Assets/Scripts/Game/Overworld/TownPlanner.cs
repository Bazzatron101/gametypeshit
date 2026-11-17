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
    /// Lays a town out at bake time (tickets 49, 83 and 84). Streets run out from the square: the roads that
    /// reach the place become its main streets, and lanes branch off them. The most important buildings (the
    /// hall, the inn, the chapel, the market) face the square; the rest line the streets with their doors to
    /// the street. Buildings keep off roads, streets, the square and each other, and only go where the ground
    /// is buildable and flat enough. The same inputs always give the same town. The bake writes the result into
    /// the overworld, draws the streets as road, and blocks each footprint so nobody walks through a wall.
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

        /// <summary>A street: a straight run of road from <see cref="From"/> to <see cref="To"/>, <see cref="HalfWidth"/> each side.</summary>
        public sealed class Street
        {
            public Vector2 From, To;
            public float HalfWidth;
            public bool Main;
        }

        /// <summary>A planned town: its buildings and its streets.</summary>
        public sealed class Layout
        {
            public readonly List<Placement> Buildings = new List<Placement>();
            public readonly List<Street> Streets = new List<Street>();
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

        /// <summary>The roles a style asks for, in order of importance; houses fill what space is left (ticket 84).</summary>
        private static string[] Wanted(TownStyle style) => style switch
        {
            TownStyle.GuildTown => new[] { "hall", "inn", "shop", "well", "market", "chapel", "smithy", "stables", "warehouse", "workshop" },
            TownStyle.Castle => new[] { "keep", "tower", "tower", "tower", "tower", "inn", "market", "chapel", "smithy", "stables", "warehouse" },
            _ => new[] { "inn", "well", "chapel", "smithy", "stables", "barn", "workshop" },
        };

        /// <summary>Houses on top of the wanted buildings: a village comes to about 10 to 20 buildings, a town 30 to 60 (ticket 84).</summary>
        public static int MaxHouses(TownStyle style) => style == TownStyle.GuildTown ? 34 : style == TownStyle.Castle ? 18 : 12;

        /// <summary>Roles that face the square rather than a street.</summary>
        private static bool OnTheSquare(string role) => role == "hall" || role == "inn" || role == "shop" || role == "market" || role == "chapel";

        /// <summary>How far a building stands back from a street's edge, in metres.</summary>
        public const float StreetSetback = 1.5f;

        public static List<Placement> Plan(Site site, IReadOnlyList<Kind> kinds) => PlanTown(site, kinds).Buildings;

        public static Layout PlanTown(Site site, IReadOnlyList<Kind> kinds)
        {
            var layout = new Layout();
            if (kinds == null || kinds.Count == 0) return layout;
            var seed = (int)EnvironmentScatter.Hash(Mathf.RoundToInt(site.Centre.x), Mathf.RoundToInt(site.Centre.y), 49, 0);
            layout.Streets.AddRange(LayStreets(site, seed));
            var placed = layout.Buildings;
            var streets = layout.Streets;

            var forStreets = new List<Kind>();
            var n = 0;
            foreach (var role in Wanted(site.Style))
            {
                var kind = First(kinds, role);
                if (!kind.HasValue) continue;
                var onSquare = OnTheSquare(role) || role == "keep" || role == "well" || role == "tower";
                if (onSquare && TryPlace(site, kind.Value, placed, streets, PreferredAngle(role, site.Style, seed, n++), firstRingOnly: role != "tower")) continue;
                if (role != "keep" && role != "well" && role != "tower") forStreets.Add(kind.Value); // no room on the square: a street then
            }
            var houses = new List<Kind>();
            foreach (var k in kinds) if (k.Role == "house") houses.Add(k);
            for (var h = 0; h < MaxHouses(site.Style) && houses.Count > 0; h++)
                forStreets.Add(houses[(int)(EnvironmentScatter.Hash(seed, 7, h, 0) % (uint)houses.Count)]);
            FillStreets(site, layout, forStreets);
            // What no street had room for fills the gaps between them, door to the square.
            var leftover = 0;
            foreach (var kind in forStreets)
                if (TryPlace(site, kind, placed, streets, PreferredAngle(kind.Role, site.Style, seed, 100 + leftover++), firstRingOnly: false) == false && kind.Role == "house") break;
            return layout;
        }

        // --- Streets (ticket 83) --------------------------------------------------------------------

        /// <summary>
        /// The main streets run out from the square along the roads that reach the place (two opposite ways for
        /// a place no road reaches, and the way opposite for a place one road reaches); lanes branch off them.
        /// </summary>
        public static List<Street> LayStreets(Site site, int seed)
        {
            var angles = new List<float>();
            if (site.DistanceToRoad != null)
            {
                var r = Mathf.Min(site.SquareRadius + 6f, site.Radius - 1f);
                const int samples = 120;
                var wet = new bool[samples];
                for (var i = 0; i < samples; i++)
                {
                    var a = i * 2f * Mathf.PI / samples;
                    var p = site.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    wet[i] = site.DistanceToRoad(p.x, p.y) <= 0f;
                }
                // Each run of samples on a road is one way in; start from a sample off the roads so no run wraps round.
                var start = Array.IndexOf(wet, false);
                if (start >= 0)
                {
                    var runFrom = -1;
                    for (var k = 1; k <= samples; k++)
                    {
                        var i = (start + k) % samples;
                        if (wet[i] && runFrom < 0) runFrom = k;
                        if ((!wet[i] || k == samples) && runFrom >= 0)
                        {
                            var middle = (start + (runFrom + k - 1) / 2f) % samples;
                            angles.Add(middle * 2f * Mathf.PI / samples);
                            runFrom = -1;
                        }
                    }
                }
            }
            if (angles.Count == 0)
            {
                // No road: a street east and west, turned a little, so north of the square (behind the board) stays free.
                var a = (EnvironmentScatter.Unit(EnvironmentScatter.Hash(seed, 31, 0, 0)) - 0.5f) * Mathf.PI / 3f;
                angles.Add(a);
            }
            if (angles.Count == 1) angles.Add(angles[0] + Mathf.PI);
            if (angles.Count > 4) angles.RemoveRange(4, angles.Count - 4);

            var streets = new List<Street>();
            foreach (var a in angles)
            {
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                streets.Add(new Street { From = site.Centre + dir * site.SquareRadius, To = site.Centre + dir * (site.Radius - 2f), HalfWidth = 3f, Main = true });
            }
            var mains = new List<Street>(streets);
            foreach (var main in mains)
            {
                var dir = (main.To - main.From).normalized;
                var left = new Vector2(-dir.y, dir.x);
                for (var d = site.SquareRadius + 12f; d < site.Radius - 10f; d += 18f)
                foreach (var side in new[] { -1f, 1f })
                {
                    var from = site.Centre + dir * d;
                    var laneDir = left * side;
                    var length = 28f;
                    while (length > 8f && (from + laneDir * length - site.Centre).magnitude > site.Radius - 2f) length -= 2f;
                    if (length <= 8f) continue;
                    var to = from + laneDir * length;
                    var crosses = false;
                    foreach (var other in mains)
                        if (other != main && DistanceToSegment(to, other.From, other.To) < 10f) crosses = true; // it would run into another main street
                    if (!crosses) streets.Add(new Street { From = from, To = to, HalfWidth = 2f });
                }
            }
            return streets;
        }

        /// <summary>
        /// Lines the streets with the buildings given, in order: each goes to the free frontage nearest the square,
        /// door to the street, set back from it, on whichever side has room.
        /// </summary>
        private static void FillStreets(Site site, Layout layout, List<Kind> queue)
        {
            var fronts = new List<(Street street, float side, float s)>();
            foreach (var street in layout.Streets)
            foreach (var side in new[] { -1f, 1f })
                fronts.Add((street, side, street.Main ? 0f : 3f)); // a lane's first metres are the main street's frontage
            var active = new List<int>();
            for (var i = 0; i < fronts.Count; i++) active.Add(i);

            while (queue.Count > 0 && active.Count > 0)
            {
                // The free frontage nearest the square.
                var pick = active[0];
                var best = float.MaxValue;
                foreach (var i in active)
                {
                    var f = fronts[i];
                    var at = Vector2.Lerp(f.street.From, f.street.To, Mathf.Clamp01(f.s / Mathf.Max(0.01f, (f.street.To - f.street.From).magnitude)));
                    var d = (at - site.Centre).sqrMagnitude;
                    if (d < best) { best = d; pick = i; }
                }
                var (street, side, s) = fronts[pick];
                var kind = queue[0];
                var length = (street.To - street.From).magnitude;
                if (s + kind.Footprint.x > length)
                {
                    active.Remove(pick);
                    continue;
                }
                var dir = (street.To - street.From) / length;
                var normal = new Vector2(-dir.y, dir.x) * side;
                var centre = street.From + dir * (s + kind.Footprint.x / 2f) + normal * (street.HalfWidth + StreetSetback + kind.Footprint.y / 2f);
                var door = -normal;
                var yaw = Mathf.Atan2(door.x, door.y) * Mathf.Rad2Deg;
                if (Accept(site, kind, layout.Buildings, layout.Streets, centre, yaw, ignoreSquare: false))
                {
                    queue.RemoveAt(0);
                    fronts[pick] = (street, side, s + kind.Footprint.x + site.Gap);
                }
                else
                {
                    fronts[pick] = (street, side, s + 2f);
                }
            }
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
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
                case "market": return Mathf.PI * 1.6f;
                case "chapel": return Mathf.PI * 0.6f;
                case "well": return Mathf.PI * 1.25f;
                case "keep": return 0f;
                case "tower": return Mathf.PI / 4f + n * Mathf.PI / 2f;
                default: return EnvironmentScatter.Unit(EnvironmentScatter.Hash(seed, 11, n, 0)) * 2f * Mathf.PI;
            }
        }

        private static bool TryPlace(Site site, Kind kind, List<Placement> placed, List<Street> streets, float startAngle, bool firstRingOnly)
        {
            // The keep stands in the middle of a castle town; the well in the square, off the centre.
            if (kind.Role == "keep")
                return Accept(site, kind, placed, null, site.Centre, 0f, ignoreSquare: true); // the streets start at the square's edge, past it
            if (kind.Role == "well")
            {
                var r = site.SquareRadius * 0.7f;
                for (var step = 0; step < 16; step++)
                {
                    var a = startAngle + step * Mathf.PI / 8f;
                    if (Accept(site, kind, placed, streets, site.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 0f, ignoreSquare: true)) return true;
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
                    if (Accept(site, kind, placed, streets, at, yaw, ignoreSquare: false)) return true;
                }
                if (firstRingOnly) break; // the square's own frontage only
            }
            return false;
        }

        private static bool Accept(Site site, Kind kind, List<Placement> placed, List<Street> streets, Vector2 at, float yaw, bool ignoreSquare)
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
                if (streets != null)
                    foreach (var street in streets)
                        if (DistanceToSegment(p, street.From, street.To) < street.HalfWidth + StreetSetback - 0.01f) return false; // off the streets
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
