using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// A baked overworld (tickets 39 and 40): how the map is cut into tiles, where each tile's ground file is
    /// (StreamingAssets, loaded as players move, ADR 0020), a coarse map of the whole world (always loaded),
    /// and the hand-placed markers as data: gate entrances, the town, roads and landmarks. Made by the bake
    /// (Hunter System menu), never by hand. <see cref="ApplyTo"/> gives a config its ground and overworld.
    /// </summary>
    public sealed class OverworldAsset : ScriptableObject
    {
        [Serializable]
        public sealed class GateSiteData
        {
            public string gateId;
            public Vector3 entrance;
            public string region;
            public bool knownFromStart;
        }

        [Serializable]
        public sealed class RoadData
        {
            public float halfWidth;
            public List<Vector3> points = new List<Vector3>();
        }

        [Serializable]
        public sealed class LandmarkData
        {
            public string label;
            public Vector3 position;
        }

        [Header("Tiles")]
        [SerializeField, Tooltip("The south-west corner of the map")] private Vector2 origin;
        [SerializeField, Min(1f)] private float tileSize = 250f;
        [SerializeField, Min(1)] private int tilesX = 1;
        [SerializeField, Min(1)] private int tilesZ = 1;
        [SerializeField, Tooltip("Under StreamingAssets: the folder of tile_<x>_<z>.bytes files")] private string tileFolder;
        [SerializeField, Tooltip("The whole world's ground at low detail (GroundBake's format): distant views, and heights where no tile is loaded")] private TextAsset coarse;

        [Header("Markers")]
        [SerializeField] private List<GateSiteData> gates = new List<GateSiteData>();
        [SerializeField] private bool hasTown;
        [SerializeField] private Vector3 townCentre;
        [SerializeField] private float townRadius;
        [SerializeField] private List<RoadData> roads = new List<RoadData>();
        [SerializeField] private List<LandmarkData> landmarks = new List<LandmarkData>();

        [Header("Rules")]
        [SerializeField] private List<string> startingRegions = new List<string> { "greenmarch" };
        [SerializeField, Min(0.5f)] private float entranceRadius = 3f;
        [SerializeField, Min(1f)] private float findRadius = 20f;
        [SerializeField, Range(0, 100)] private int unopenedRegionXpPercent = 25;

        [Header("Shown ground (built from the baked ground as tiles load)")]
        [SerializeField, Min(0.5f), Tooltip("Facet size close up, in metres")] private float meshCellSize = 4f;
        [SerializeField, Min(1f), Tooltip("Facet size far away, in metres (the coarse map's)")] private float farMeshCellSize = 16f;
        [SerializeField] private float seaLevel = 4f;
        [SerializeField, Min(0f), Tooltip("Ground this far above the sea is drawn as sand")] private float sandAboveSea = 1.2f;

        [NonSerialized] private List<(List<Vector2> line, float halfWidth)> _roadLines;

        public Vector2 Origin => origin;
        public float TileSize => tileSize;
        public int TilesX => tilesX;
        public int TilesZ => tilesZ;
        public string TileFolder => tileFolder;
        public TextAsset Coarse => coarse;
        public IReadOnlyList<GateSiteData> Gates => gates;
        public bool HasTown => hasTown;
        public Vector3 TownCentre => townCentre;
        public float TownRadius => townRadius;
        public IReadOnlyList<RoadData> Roads => roads;
        public IReadOnlyList<LandmarkData> Landmarks => landmarks;
        public float MeshCellSize => meshCellSize;
        public float FarMeshCellSize => farMeshCellSize;
        public float SeaLevel => seaLevel;
        public float SandAboveSea => sandAboveSea;

        /// <summary>True once the bake has written tiles for this world.</summary>
        public bool IsBaked => coarse != null && !string.IsNullOrEmpty(tileFolder);

        /// <summary>How far (x, z) is outside the nearest road's width: 0 or less on a road, infinity with none. Safe off the main thread once called once on it.</summary>
        public float DistanceToRoad(float x, float z)
        {
            _roadLines ??= roads.FindAll(r => r != null && r.points.Count > 0)
                .ConvertAll(r => (r.points.ConvertAll(q => new Vector2(q.x, q.z)), r.halfWidth));
            var best = float.PositiveInfinity;
            var p = new Vector2(x, z);
            foreach (var (line, halfWidth) in _roadLines)
                best = Mathf.Min(best, HeightSource.DistanceToLine(p, line, out _) - halfWidth);
            return best;
        }

        // --- Ground ------------------------------------------------------------------------------------

        public HeightmapGround LoadCoarse()
        {
            if (coarse == null) throw new InvalidOperationException($"{name} has no baked ground: run the bake.");
            return GroundBake.Decode(coarse.bytes);
        }

        /// <summary>A new tiled ground for this world, with only the coarse map loaded: tiles are streamed into it.</summary>
        public TiledGround CreateTiledGround() => new TiledGround(origin.x, origin.y, tileSize, tilesX, tilesZ, LoadCoarse());

        /// <summary>Where <paramref name="tile"/>'s ground file is.</summary>
        public string TilePath(TileCoord tile) => Path.Combine(Application.streamingAssetsPath, tileFolder, TileFileName(tile));

        public static string TileFileName(TileCoord tile) => $"tile_{tile.X}_{tile.Z}.bytes";

        /// <summary>Reads and decodes one tile now (tools, tests, the first tiles of a scene).</summary>
        public HeightmapGround LoadTile(TileCoord tile) => GroundBake.Decode(File.ReadAllBytes(TilePath(tile)));

        /// <summary>Reads and decodes one tile off the main thread. The path is worked out here, on the calling thread.</summary>
        public Task<HeightmapGround> LoadTileAsync(TileCoord tile)
        {
            var path = TilePath(tile);
            return Task.Run(() => GroundBake.Decode(File.ReadAllBytes(path)));
        }

        /// <summary>The whole world's ground in full detail, every tile read at once. For tools and tests: the game streams tiles instead.</summary>
        public HeightmapGround LoadGround()
        {
            var first = LoadTile(new TileCoord(0, 0));
            var cells = first.Columns;
            var columns = tilesX * cells;
            var rows = tilesZ * cells;
            var heights = new float[(columns + 1) * (rows + 1)];
            var kinds = new GroundKind[columns * rows];
            for (var tz = 0; tz < tilesZ; tz++)
            for (var tx = 0; tx < tilesX; tx++)
            {
                var tile = tx == 0 && tz == 0 ? first : LoadTile(new TileCoord(tx, tz));
                for (var z = 0; z <= cells; z++)
                for (var x = 0; x <= cells; x++)
                {
                    var p = new Vec3(tile.OriginX + x * tile.CellSize, 0f, tile.OriginZ + z * tile.CellSize);
                    heights[(tz * cells + z) * (columns + 1) + tx * cells + x] = tile.HeightAt(p);
                    if (x < cells && z < cells)
                        kinds[(tz * cells + z) * columns + tx * cells + x] = tile.KindAt(new Vec3(p.X + tile.CellSize / 2f, 0f, p.Z + tile.CellSize / 2f));
                }
            }
            return new HeightmapGround(origin.x, origin.y, first.CellSize, columns, rows, heights, kinds);
        }

        // --- The config --------------------------------------------------------------------------------

        public OverworldDefinition ToDefinition()
        {
            var overworld = new OverworldDefinition
            {
                EntranceRadius = entranceRadius, FindRadius = findRadius, UnopenedRegionXpPercent = unopenedRegionXpPercent,
            };
            overworld.StartingRegions.AddRange(startingRegions);
            foreach (var site in gates)
                overworld.Gates.Add(new GateSite
                {
                    GateId = site.gateId, Entrance = new Vec3(site.entrance.x, site.entrance.y, site.entrance.z),
                    Region = site.region, KnownFromStart = site.knownFromStart,
                });
            return overworld;
        }

        /// <summary>
        /// Gives <paramref name="config"/> this world: its ground (<paramref name="ground"/>, or a new tiled ground
        /// with no tiles loaded yet), its gate sites and regions, and the town as the hub's place. The config's
        /// hub must already be set (an overworld needs the town, ticket 38).
        /// </summary>
        public void ApplyTo(SimulationConfig config, IGround ground = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Ground = ground ?? CreateTiledGround();
            config.Overworld = ToDefinition();
            if (hasTown && config.Hub != null) config.Hub.Centre = new Vec3(townCentre.x, 0f, townCentre.z);
        }

        /// <summary>For the bake, which writes the whole asset at once.</summary>
        public void Set(Vector2 mapOrigin, float tile, int countX, int countZ, string folder, TextAsset coarseGround,
            List<GateSiteData> gateSites, bool town, Vector3 townAt, float townReach, List<RoadData> roadList, List<LandmarkData> landmarkList,
            float sea, float nearCell, float farCell)
        {
            origin = mapOrigin;
            tileSize = tile;
            tilesX = countX;
            tilesZ = countZ;
            tileFolder = folder;
            coarse = coarseGround;
            gates = gateSites ?? new List<GateSiteData>();
            hasTown = town;
            townCentre = townAt;
            townRadius = townReach;
            roads = roadList ?? new List<RoadData>();
            _roadLines = null;
            landmarks = landmarkList ?? new List<LandmarkData>();
            seaLevel = sea;
            meshCellSize = nearCell;
            farMeshCellSize = farCell;
        }
    }
}
