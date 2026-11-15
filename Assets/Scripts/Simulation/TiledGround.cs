using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>A tile's place in a <see cref="TiledGround"/>: column from the west, row from the south.</summary>
    public readonly struct TileCoord : IEquatable<TileCoord>
    {
        public readonly int X, Z;

        public TileCoord(int x, int z) { X = x; Z = z; }

        public bool Equals(TileCoord other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is TileCoord other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Z);
        public override string ToString() => $"tile {X},{Z}";
    }

    /// <summary>
    /// The overworld's ground as square tiles that load and unload as players move (ticket 40, ADR 0020).
    /// A loaded tile answers for its square in full detail. Elsewhere on the map the ground is
    /// <see cref="GroundKind.Unloaded"/>: nobody steps onto it, and anyone on it waits. Heights there come
    /// from a coarse map of the whole world, which is always loaded. Off the map is blocked. Tiles are put
    /// in and taken out between ticks by whoever streams them; the simulation only reads.
    /// </summary>
    public sealed class TiledGround : IGround
    {
        private readonly Dictionary<TileCoord, IGround> _tiles = new Dictionary<TileCoord, IGround>();
        private readonly IGround _coarse;

        /// <param name="coarse">The whole world's heights at low detail, for ground whose tile is not loaded; null for flat at 0.</param>
        public TiledGround(float originX, float originZ, float tileSize, int tilesX, int tilesZ, IGround coarse = null)
        {
            if (tileSize <= 0f) throw new ArgumentOutOfRangeException(nameof(tileSize));
            if (tilesX < 1 || tilesZ < 1) throw new ArgumentOutOfRangeException(nameof(tilesX), "The map needs at least one tile.");
            OriginX = originX;
            OriginZ = originZ;
            TileSize = tileSize;
            TilesX = tilesX;
            TilesZ = tilesZ;
            _coarse = coarse;
        }

        public float OriginX { get; }
        public float OriginZ { get; }
        public float TileSize { get; }
        public int TilesX { get; }
        public int TilesZ { get; }
        public float MaxX => OriginX + TilesX * TileSize;
        public float MaxZ => OriginZ + TilesZ * TileSize;

        /// <summary>The tiles loaded now.</summary>
        public IReadOnlyCollection<TileCoord> Loaded => _tiles.Keys;

        public bool IsOnMap(TileCoord tile) => tile.X >= 0 && tile.Z >= 0 && tile.X < TilesX && tile.Z < TilesZ;

        /// <summary>The tile holding <paramref name="p"/> (it may be off the map: check <see cref="IsOnMap"/>).</summary>
        public TileCoord TileAt(Vec3 p) => new TileCoord((int)Math.Floor((p.X - OriginX) / TileSize), (int)Math.Floor((p.Z - OriginZ) / TileSize));

        /// <summary>The south-west corner of <paramref name="tile"/>.</summary>
        public Vec3 CornerOf(TileCoord tile) => new Vec3(OriginX + tile.X * TileSize, 0f, OriginZ + tile.Z * TileSize);

        public bool IsLoaded(TileCoord tile) => _tiles.ContainsKey(tile);

        /// <summary>Puts a tile's ground in (replacing any). It answers for that tile's square only.</summary>
        public void SetTile(TileCoord tile, IGround ground)
        {
            if (!IsOnMap(tile)) throw new ArgumentOutOfRangeException(nameof(tile), $"{tile} is off the map.");
            _tiles[tile] = ground ?? throw new ArgumentNullException(nameof(ground));
        }

        public bool RemoveTile(TileCoord tile) => _tiles.Remove(tile);

        public float HeightAt(Vec3 p)
        {
            if (_tiles.TryGetValue(Clamp(TileAt(p)), out var tile)) return tile.HeightAt(p);
            return _coarse?.HeightAt(p) ?? 0f;
        }

        public GroundKind KindAt(Vec3 p)
        {
            var at = TileAt(p);
            if (!IsOnMap(at)) return GroundKind.Blocked;
            return _tiles.TryGetValue(at, out var tile) ? tile.KindAt(p) : GroundKind.Unloaded;
        }

        public Vec3 Confine(Vec3 from, Vec3 to) => GroundExtensions.Slide(this, from, to);

        public float ReachAlong(Vec3 origin, Vec3 direction, float range) => GroundExtensions.StepAlong(this, origin, direction, range, 0.1f);

        /// <summary>
        /// The tiles that should be loaded for someone at each of <paramref name="centres"/>: every tile on the map
        /// whose square comes within <paramref name="radius"/> of any of them.
        /// </summary>
        public HashSet<TileCoord> TilesWithin(IEnumerable<Vec3> centres, float radius)
        {
            var wanted = new HashSet<TileCoord>();
            foreach (var c in centres)
            {
                var min = TileAt(new Vec3(c.X - radius, 0f, c.Z - radius));
                var max = TileAt(new Vec3(c.X + radius, 0f, c.Z + radius));
                for (var z = Math.Max(0, min.Z); z <= Math.Min(TilesZ - 1, max.Z); z++)
                for (var x = Math.Max(0, min.X); x <= Math.Min(TilesX - 1, max.X); x++)
                {
                    var tile = new TileCoord(x, z);
                    if (DistanceToTile(c, tile) <= radius) wanted.Add(tile);
                }
            }
            return wanted;
        }

        /// <summary>How far <paramref name="p"/> is from <paramref name="tile"/>'s square, on the floor (0 inside it).</summary>
        public float DistanceToTile(Vec3 p, TileCoord tile)
        {
            var corner = CornerOf(tile);
            var dx = Math.Max(Math.Max(corner.X - p.X, 0f), p.X - (corner.X + TileSize));
            var dz = Math.Max(Math.Max(corner.Z - p.Z, 0f), p.Z - (corner.Z + TileSize));
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>The nearest tile on the map, for heights at the very edge.</summary>
        private TileCoord Clamp(TileCoord tile) => new TileCoord(Math.Min(Math.Max(tile.X, 0), TilesX - 1), Math.Min(Math.Max(tile.Z, 0), TilesZ - 1));
    }
}
