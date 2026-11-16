using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>What a spot of ground is, for the rules (ticket 37). The art may differ; the rules read this.</summary>
    public enum GroundKind : byte
    {
        /// <summary>Ordinary ground: walk on it.</summary>
        Walkable = 0,

        /// <summary>Too steep, a cliff, a wall, or off the edge of the map: nobody stands here, and it blocks a charge.</summary>
        Blocked = 1,

        /// <summary>The sea, a lake: not walkable (no swimming yet).</summary>
        Water = 2,

        /// <summary>A face marked as climbable (ledges, vines, a different rock; ticket 36). Walkable for now; climbing itself is later work.</summary>
        Climbable = 3,

        /// <summary>
        /// Ground whose tile is not loaded yet (ticket 40, ADR 0020). Never baked. Nobody steps onto it, and
        /// anyone standing on it waits where they are until it loads.
        /// </summary>
        Unloaded = 4,
    }

    /// <summary>
    /// The ground everyone stands on: how high it is, whether a spot can be walked on, and how far a
    /// straight line runs before something blocks it. The hub, a gate's rooms and the overworld are
    /// each one of these. Host and clients must hold the same ground, built from the same data, so
    /// that prediction agrees with the host (ADR 0004). Only X and Z of a point are read.
    /// </summary>
    public interface IGround
    {
        /// <summary>The ground's height at <paramref name="p"/>.</summary>
        float HeightAt(Vec3 p);

        GroundKind KindAt(Vec3 p);

        /// <summary>
        /// Where a body moving from <paramref name="from"/> towards <paramref name="to"/> this tick ends up:
        /// <paramref name="to"/> when it can stand there, otherwise as far as it can get, sliding along the edge.
        /// Keeps <paramref name="to"/>'s Y; settling on the ground's height is the caller's.
        /// </summary>
        Vec3 Confine(Vec3 from, Vec3 to);

        /// <summary>How far along <paramref name="direction"/> (on the floor, unit length) from <paramref name="origin"/> the ground can be walked, up to <paramref name="range"/>.</summary>
        float ReachAlong(Vec3 origin, Vec3 direction, float range);
    }

    public static class GroundExtensions
    {
        /// <summary>Whether someone can stand at <paramref name="p"/>.</summary>
        public static bool IsWalkable(this IGround ground, Vec3 p) => IsWalkable(ground.KindAt(p));

        public static bool IsWalkable(GroundKind kind) => kind == GroundKind.Walkable || kind == GroundKind.Climbable;

        /// <summary>
        /// The usual move onto ground with edges: straight there if it can stand there. Otherwise it slides: it
        /// keeps whichever along-the-axis part of the move it can make (the longer, if both), or stays put.
        /// Someone already on ground they cannot stand on (put there by a spawn) may move freely, so nobody is
        /// trapped; but ground not loaded yet is never stepped onto or off: they wait (ADR 0020).
        /// </summary>
        public static Vec3 Slide(IGround ground, Vec3 from, Vec3 to)
        {
            var kindTo = ground.KindAt(to);
            var kindFrom = ground.KindAt(from);
            var stay = new Vec3(from.X, to.Y, from.Z);
            if (kindTo == GroundKind.Unloaded || kindFrom == GroundKind.Unloaded) return stay;
            if (IsWalkable(kindTo) || !IsWalkable(kindFrom)) return to;
            var alongX = new Vec3(to.X, to.Y, from.Z);
            var alongZ = new Vec3(from.X, to.Y, to.Z);
            var canX = Math.Abs(to.X - from.X) > 0f && ground.IsWalkable(alongX);
            var canZ = Math.Abs(to.Z - from.Z) > 0f && ground.IsWalkable(alongZ);
            if (canX && canZ) return Math.Abs(to.X - from.X) >= Math.Abs(to.Z - from.Z) ? alongX : alongZ;
            if (canX) return alongX;
            if (canZ) return alongZ;
            return stay;
        }

        /// <summary>The usual blocked-line test: step along the line until the next step cannot be stood on.</summary>
        internal static float StepAlong(IGround ground, Vec3 origin, Vec3 direction, float range, float stepLength)
        {
            for (var travelled = stepLength; travelled <= range; travelled += stepLength)
                if (!ground.IsWalkable(origin + direction * travelled)) return travelled - stepLength;
            return range;
        }
    }

    /// <summary>
    /// Another ground with its climbable faces turned to cliffs: what a mounted rider can go over (ticket 42).
    /// Riders dismount to climb.
    /// </summary>
    public sealed class NoClimbingGround : IGround
    {
        public NoClimbingGround(IGround inner) => Inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public IGround Inner { get; }

        public float HeightAt(Vec3 p) => Inner.HeightAt(p);

        public GroundKind KindAt(Vec3 p)
        {
            var kind = Inner.KindAt(p);
            return kind == GroundKind.Climbable ? GroundKind.Blocked : kind;
        }

        public Vec3 Confine(Vec3 from, Vec3 to) => GroundExtensions.Slide(this, from, to);

        public float ReachAlong(Vec3 origin, Vec3 direction, float range) => GroundExtensions.StepAlong(this, origin, direction, range, 0.1f);
    }

    /// <summary>Flat ground at height 0 that goes on for ever: the arena and tests with no hub, gate or world.</summary>
    public sealed class OpenGround : IGround
    {
        public static readonly OpenGround Instance = new OpenGround();

        private OpenGround() { }

        public float HeightAt(Vec3 p) => 0f;
        public GroundKind KindAt(Vec3 p) => GroundKind.Walkable;
        public Vec3 Confine(Vec3 from, Vec3 to) => to;
        public float ReachAlong(Vec3 origin, Vec3 direction, float range) => range;
    }

    /// <summary>
    /// Flat floors at height 0 inside walls: the hub's one room, or a gate's rooms. Rooms share edges,
    /// so the ways between them stay open. Anyone off every floor is put back on the nearest one.
    /// </summary>
    public sealed class FloorGround : IGround
    {
        private const float StepLength = 0.1f;

        public FloorGround(IReadOnlyList<RoomRect> rooms)
        {
            Rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
            if (rooms.Count == 0) throw new ArgumentException("Floor ground needs at least one room.", nameof(rooms));
        }

        public IReadOnlyList<RoomRect> Rooms { get; }

        public float HeightAt(Vec3 p) => 0f;

        public GroundKind KindAt(Vec3 p)
        {
            foreach (var room in Rooms) if (room.Contains(p)) return GroundKind.Walkable;
            return GroundKind.Blocked;
        }

        /// <summary>The nearest point on some room's floor to <paramref name="to"/>: walls only push out, so where it came from does not matter.</summary>
        public Vec3 Confine(Vec3 from, Vec3 to) => Nearest(to);

        /// <summary>The nearest point that is on some room's floor: <paramref name="p"/> itself when it is on one.</summary>
        public Vec3 Nearest(Vec3 p)
        {
            var best = p;
            var bestDistance = float.MaxValue;
            foreach (var room in Rooms)
            {
                if (room.Contains(p)) return p;
                var nearest = room.Nearest(p);
                var distance = (nearest - p).Length;
                if (distance >= bestDistance) continue;
                best = nearest;
                bestDistance = distance;
            }
            return best;
        }

        public float ReachAlong(Vec3 origin, Vec3 direction, float range) => GroundExtensions.StepAlong(this, origin, direction, range, StepLength);
    }

    /// <summary>
    /// Ground from a heightmap and a grid of ground kinds: the overworld (ADR 0015). Heights are samples
    /// at the grid's corners, <see cref="CellSize"/> apart, read between samples by blending the four
    /// round a point; kinds are per cell. Outside the grid is <see cref="GroundKind.Blocked"/>, the edge
    /// of the map. Meant to be baked from the Unity Terrain (ticket 39) so the art and the rules agree.
    /// </summary>
    public sealed class HeightmapGround : IGround
    {
        private readonly float[] _heights;
        private readonly GroundKind[] _kinds;

        /// <param name="originX">X of the grid's first corner (its west edge).</param>
        /// <param name="originZ">Z of the grid's first corner (its south edge).</param>
        /// <param name="cellSize">Metres between samples.</param>
        /// <param name="columns">Cells along X.</param>
        /// <param name="rows">Cells along Z.</param>
        /// <param name="heights">(columns + 1) × (rows + 1) corner heights, row by row from the south, west to east along each row.</param>
        /// <param name="kinds">columns × rows cell kinds in the same order; null for all walkable.</param>
        public HeightmapGround(float originX, float originZ, float cellSize, int columns, int rows, float[] heights, GroundKind[] kinds = null)
        {
            if (cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize), "Cells need a size.");
            if (columns < 1 || rows < 1) throw new ArgumentOutOfRangeException(nameof(columns), "The grid needs at least one cell.");
            if (heights == null) throw new ArgumentNullException(nameof(heights));
            if (heights.Length != (columns + 1) * (rows + 1))
                throw new ArgumentException($"A {columns} × {rows} grid needs {(columns + 1) * (rows + 1)} heights, not {heights.Length}.", nameof(heights));
            if (kinds != null && kinds.Length != columns * rows)
                throw new ArgumentException($"A {columns} × {rows} grid needs {columns * rows} kinds, not {kinds.Length}.", nameof(kinds));
            OriginX = originX;
            OriginZ = originZ;
            CellSize = cellSize;
            Columns = columns;
            Rows = rows;
            _heights = (float[])heights.Clone();
            _kinds = kinds != null ? (GroundKind[])kinds.Clone() : new GroundKind[columns * rows];
        }

        public float OriginX { get; }
        public float OriginZ { get; }
        public float CellSize { get; }
        public int Columns { get; }
        public int Rows { get; }

        /// <summary>The grid's far corner.</summary>
        public float MaxX => OriginX + Columns * CellSize;
        public float MaxZ => OriginZ + Rows * CellSize;

        /// <summary>Blended between the four samples round <paramref name="p"/>; outside the grid, the height at its nearest edge.</summary>
        public float HeightAt(Vec3 p)
        {
            var gx = Math.Min(Math.Max((p.X - OriginX) / CellSize, 0f), Columns);
            var gz = Math.Min(Math.Max((p.Z - OriginZ) / CellSize, 0f), Rows);
            var cx = Math.Min((int)gx, Columns - 1);
            var cz = Math.Min((int)gz, Rows - 1);
            var fx = gx - cx;
            var fz = gz - cz;
            var stride = Columns + 1;
            var h00 = _heights[cz * stride + cx];
            var h10 = _heights[cz * stride + cx + 1];
            var h01 = _heights[(cz + 1) * stride + cx];
            var h11 = _heights[(cz + 1) * stride + cx + 1];
            var south = h00 + (h10 - h00) * fx;
            var north = h01 + (h11 - h01) * fx;
            return south + (north - south) * fz;
        }

        public GroundKind KindAt(Vec3 p)
        {
            if (p.X < OriginX || p.Z < OriginZ || p.X >= MaxX || p.Z >= MaxZ) return GroundKind.Blocked;
            var cx = Math.Min((int)((p.X - OriginX) / CellSize), Columns - 1);
            var cz = Math.Min((int)((p.Z - OriginZ) / CellSize), Rows - 1);
            return _kinds[cz * Columns + cx];
        }

        /// <summary>
        /// Straight there if it can stand there. Otherwise it slides: it keeps whichever of the two
        /// along-the-axis parts of the move it can make (the longer, if both), or stays put. Someone
        /// already on ground they cannot stand on (put there by a spawn) may move freely, so nobody is trapped.
        /// </summary>
        public Vec3 Confine(Vec3 from, Vec3 to) => GroundExtensions.Slide(this, from, to);

        public float ReachAlong(Vec3 origin, Vec3 direction, float range) =>
            GroundExtensions.StepAlong(this, origin, direction, range, Math.Min(0.1f, CellSize / 4f));
    }
}
