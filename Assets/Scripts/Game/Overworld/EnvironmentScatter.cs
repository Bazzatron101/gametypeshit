using System;
using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Where a tile's nature stands (ticket 49), worked out from the tile's own ground as it loads, off the
    /// main thread. Every kind sits on a jittered grid in world space, so tiles meet without seams and the same
    /// tile always grows the same way. A kind grows only on open grass (walkable, above the beach, off roads),
    /// not too steep, inside its patches (smooth noise: woods, meadows), and clear of towns, buildings and gate
    /// entrances. Nothing here touches the simulation: nature is for looks and does not block anyone.
    /// </summary>
    public static class EnvironmentScatter
    {
        /// <summary>One kind's rules, copied off its <see cref="EnvironmentAsset.PropKind"/> on the main thread.</summary>
        public readonly struct Rules
        {
            public readonly float PerHectare, Cover, PatchSize, MinScale, MaxScale, MaxSlope, Clearance, Sink;
            public readonly int PatchGroup, Salt;

            public Rules(float perHectare, float cover, float patchSize, int patchGroup, float minScale, float maxScale, float maxSlope, float clearance, float sink, int salt)
            {
                PerHectare = perHectare; Cover = cover; PatchSize = Math.Max(1f, patchSize); PatchGroup = patchGroup;
                MinScale = minScale; MaxScale = Math.Max(minScale, maxScale); MaxSlope = maxSlope; Clearance = clearance; Sink = sink; Salt = salt;
            }

            public static Rules From(EnvironmentAsset.PropKind kind, int index) => new Rules(kind.perHectare, kind.cover, kind.patchSize, kind.patchGroup,
                kind.minScale, kind.maxScale, kind.maxSlope, kind.clearance, kind.sink, index * 7919 + 17);
        }

        /// <summary>A round area kept clear of nature: a town, a building, a gate entrance.</summary>
        public readonly struct ClearArea
        {
            public readonly float X, Z, Radius;
            public ClearArea(float x, float z, float radius) { X = x; Z = z; Radius = radius; }
        }

        /// <summary>Everything the scatter reads besides the tile: copied on the main thread, then safe on any thread.</summary>
        public sealed class World
        {
            public int Seed;
            public Rules[] Kinds = Array.Empty<Rules>();
            public float SeaLevel;
            public float SandAboveSea;
            /// <summary>How far a point is outside the nearest road (0 or less on one); null for no roads.</summary>
            public Func<float, float, float> DistanceToRoad;
            public ClearArea[] Clear = Array.Empty<ClearArea>();
        }

        /// <summary>
        /// The transforms of every piece of each kind on the tile whose south-west corner is (<paramref name="minX"/>,
        /// <paramref name="minZ"/>), one list per kind in <see cref="World.Kinds"/>'s order.
        /// </summary>
        public static Matrix4x4[][] Scatter(IGround ground, float minX, float minZ, float size, World world)
        {
            var result = new Matrix4x4[world.Kinds.Length][];
            var found = new List<Matrix4x4>();
            for (var k = 0; k < world.Kinds.Length; k++)
            {
                found.Clear();
                var rules = world.Kinds[k];
                if (rules.PerHectare > 0f && rules.Cover > 0f)
                {
                    var spacing = 100f / (float)Math.Sqrt(rules.PerHectare);
                    var x0 = (int)Math.Floor(minX / spacing);
                    var z0 = (int)Math.Floor(minZ / spacing);
                    var x1 = (int)Math.Ceiling((minX + size) / spacing);
                    var z1 = (int)Math.Ceiling((minZ + size) / spacing);
                    for (var iz = z0; iz <= z1; iz++)
                    for (var ix = x0; ix <= x1; ix++)
                    {
                        var h = Hash(world.Seed, rules.Salt, ix, iz);
                        var x = (ix + Unit(h)) * spacing;
                        var z = (iz + Unit(Hash(h, 1, 0, 0))) * spacing;
                        if (x < minX || x >= minX + size || z < minZ || z >= minZ + size) continue; // the next tile's
                        if (Patch(world.Seed, rules.PatchGroup, x / rules.PatchSize, z / rules.PatchSize) > rules.Cover) continue;
                        if (!Grows(ground, x, z, rules, world, out var y)) continue;
                        var yaw = Unit(Hash(h, 2, 0, 0)) * 2f * (float)Math.PI;
                        var scale = rules.MinScale + Unit(Hash(h, 3, 0, 0)) * (rules.MaxScale - rules.MinScale);
                        found.Add(Transform(x, y - rules.Sink, z, yaw, scale));
                    }
                }
                result[k] = found.ToArray();
            }
            return result;
        }

        /// <summary>Whether the kind can stand at (x, z): open grass, gentle enough, clear of roads and the clear areas. Gives the ground's height.</summary>
        public static bool Grows(IGround ground, float x, float z, Rules rules, World world, out float y)
        {
            var p = new Vec3(x, 0f, z);
            y = ground.HeightAt(p);
            if (ground.KindAt(p) != GroundKind.Walkable) return false; // not cliffs, water, climbing faces or buildings
            if (y < world.SeaLevel + world.SandAboveSea) return false; // not the beach
            if (world.DistanceToRoad != null && world.DistanceToRoad(x, z) < rules.Clearance) return false;
            foreach (var area in world.Clear)
            {
                var dx = x - area.X;
                var dz = z - area.Z;
                var reach = area.Radius + rules.Clearance;
                if (dx * dx + dz * dz < reach * reach) return false;
            }
            const float step = 1f;
            var gx = (ground.HeightAt(new Vec3(x + step, 0f, z)) - ground.HeightAt(new Vec3(x - step, 0f, z))) / (2f * step);
            var gz = (ground.HeightAt(new Vec3(x, 0f, z + step)) - ground.HeightAt(new Vec3(x, 0f, z - step))) / (2f * step);
            var slope = Math.Atan(Math.Sqrt(gx * gx + gz * gz)) * 180.0 / Math.PI;
            return slope <= rules.MaxSlope;
        }

        /// <summary>Upright, turned <paramref name="yaw"/> radians about Y, scaled evenly. Built by hand: safe off the main thread.</summary>
        public static Matrix4x4 Transform(float x, float y, float z, float yaw, float scale)
        {
            var c = (float)Math.Cos(yaw) * scale;
            var s = (float)Math.Sin(yaw) * scale;
            var m = new Matrix4x4();
            m.m00 = c; m.m02 = s; m.m03 = x;
            m.m11 = scale; m.m13 = y;
            m.m20 = -s; m.m22 = c; m.m23 = z;
            m.m33 = 1f;
            return m;
        }

        /// <summary>Smooth noise from 0 to 1 over a lattice of unit cells: a kind grows where it is under its cover.</summary>
        public static float Patch(int seed, int group, float u, float v)
        {
            var iu = (int)Math.Floor(u);
            var iv = (int)Math.Floor(v);
            var fu = Smooth(u - iu);
            var fv = Smooth(v - iv);
            var salt = 104729 + group * 31;
            var a = Unit(Hash(seed, salt, iu, iv));
            var b = Unit(Hash(seed, salt, iu + 1, iv));
            var c = Unit(Hash(seed, salt, iu, iv + 1));
            var d = Unit(Hash(seed, salt, iu + 1, iv + 1));
            var value = a + (b - a) * fu + (c - a) * fv + (a - b - c + d) * fu * fv;
            // Spread the middle-heavy blend back out, so Cover is close to the share of ground covered.
            return Clamp01(0.5f + (value - 0.5f) * 1.6f);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;

        /// <summary>A well-mixed 32-bit hash of four integers.</summary>
        public static uint Hash(int a, int b, int c, int d) => Hash(unchecked((uint)a), b, c, d);

        private static uint Hash(uint a, int b, int c, int d)
        {
            unchecked
            {
                var h = a * 0x9E3779B1u;
                h ^= (uint)b * 0x85EBCA77u; h = (h << 13) | (h >> 19);
                h ^= (uint)c * 0xC2B2AE3Du; h = (h << 17) | (h >> 15);
                h ^= (uint)d * 0x27D4EB2Fu;
                h ^= h >> 16; h *= 0x7FEB352Du;
                h ^= h >> 15; h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>A hash as a number from 0 (inclusive) to 1 (exclusive).</summary>
        public static float Unit(uint h) => (h >> 8) * (1f / 16777216f);
    }
}
