using System;
using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// A baked overworld (ticket 39): the ground the simulation walks on (heights and kinds, as bytes) and
    /// the hand-placed markers as data: gate entrances, the town, roads and landmarks. Made by the bake
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

        [SerializeField, Tooltip("The baked ground: heights and kinds (GroundBake's format)")] private TextAsset ground;
        [SerializeField] private List<GateSiteData> gates = new List<GateSiteData>();
        [SerializeField] private bool hasTown;
        [SerializeField] private Vector3 townCentre;
        [SerializeField] private float townRadius;
        [SerializeField] private List<RoadData> roads = new List<RoadData>();
        [SerializeField] private List<LandmarkData> landmarks = new List<LandmarkData>();
        [SerializeField] private List<string> startingRegions = new List<string> { "greenmarch" };
        [SerializeField, Min(0.5f)] private float entranceRadius = 3f;
        [SerializeField, Min(1f)] private float findRadius = 20f;
        [SerializeField, Range(0, 100)] private int unopenedRegionXpPercent = 25;

        [Header("Shown ground (built from the baked ground when the scene loads)")]
        [SerializeField, Min(10f), Tooltip("Metres per shown tile")] private float tileSize = 250f;
        [SerializeField, Min(0.5f), Tooltip("Facet size close up, in metres")] private float meshCellSize = 4f;
        [SerializeField, Min(1f), Tooltip("Facet size far away, in metres")] private float farMeshCellSize = 16f;
        [SerializeField, Range(0.01f, 1f), Tooltip("Below this share of the screen's height a tile switches to its far version")] private float nearLodScreenHeight = 0.12f;
        [SerializeField] private float seaLevel = 4f;
        [SerializeField, Min(0f), Tooltip("Ground this far above the sea is drawn as sand")] private float sandAboveSea = 1.2f;

        public IReadOnlyList<GateSiteData> Gates => gates;
        public bool HasTown => hasTown;
        public Vector3 TownCentre => townCentre;
        public float TownRadius => townRadius;
        public IReadOnlyList<RoadData> Roads => roads;
        public IReadOnlyList<LandmarkData> Landmarks => landmarks;
        public TextAsset GroundData => ground;
        public float TileSize => tileSize;
        public float MeshCellSize => meshCellSize;
        public float FarMeshCellSize => farMeshCellSize;
        public float NearLodScreenHeight => nearLodScreenHeight;
        public float SeaLevel => seaLevel;
        public float SandAboveSea => sandAboveSea;

        [NonSerialized] private List<(List<Vector2> line, float halfWidth)> _roadLines;

        /// <summary>How far (x, z) is outside the nearest road's width: 0 or less on a road, infinity with none.</summary>
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

        /// <summary>The baked ground, read fresh each time (it is a few megabytes; callers keep it).</summary>
        public HeightmapGround LoadGround()
        {
            if (ground == null) throw new InvalidOperationException($"{name} has no baked ground: run the bake.");
            return GroundBake.Decode(ground.bytes);
        }

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
        /// Gives <paramref name="config"/> this world: its ground, its gate sites and regions, and the town as
        /// the hub's place. The config's hub must already be set (an overworld needs the town, ticket 38).
        /// </summary>
        public void ApplyTo(SimulationConfig config, IGround loadedGround = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Ground = loadedGround ?? LoadGround();
            config.Overworld = ToDefinition();
            if (hasTown && config.Hub != null) config.Hub.Centre = new Vec3(townCentre.x, 0f, townCentre.z);
        }

        /// <summary>For the bake, which writes the whole asset at once.</summary>
        public void Set(TextAsset groundData, List<GateSiteData> gateSites, bool town, Vector3 townAt, float townReach,
            List<RoadData> roadList, List<LandmarkData> landmarkList, float shownTileSize, float sea, float nearCell, float farCell)
        {
            tileSize = shownTileSize;
            seaLevel = sea;
            meshCellSize = nearCell;
            farMeshCellSize = farCell;
            ground = groundData;
            gates = gateSites ?? new List<GateSiteData>();
            hasTown = town;
            townCentre = townAt;
            townRadius = townReach;
            roads = roadList ?? new List<RoadData>();
            _roadLines = null;
            landmarks = landmarkList ?? new List<LandmarkData>();
        }
    }
}
