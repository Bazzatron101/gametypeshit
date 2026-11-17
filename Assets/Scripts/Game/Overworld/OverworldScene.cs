using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Makes a scene an overworld scene (tickets 39 and 40). The simulation runner gives its config this
    /// world's tiled ground and markers. Tiles stream in and out as players move (ADR 0020): a client
    /// around its own player, the host around every player. A tile's ground and its shown mesh are made
    /// off the main thread from the same file, so the art and the rules cannot drift. Every tile always has
    /// a far version from the coarse map, shown until its full tile arrives. The Unity Terrain tiles are only
    /// the editing surface: tagged EditorOnly, so builds never carry them, and hidden in play in the editor.
    /// </summary>
    [DefaultExecutionOrder(-100)] // before the runner reads it
    public sealed class OverworldScene : MonoBehaviour
    {
        [SerializeField] private OverworldAsset overworld;
        [SerializeField, Tooltip("One per TerrainSurface: grass, rock, climbable, sand, road, water (the water material if left out)")] private Material[] surfaceMaterials = new Material[0];
        [SerializeField] private Material waterMaterial;
        [SerializeField] private Material gateMaterial;
        [SerializeField, Min(0f), Tooltip("Tiles this close to a player are loaded. Set for a mount (16 m/s), not walking.")] private float loadRadius = 600f;
        [SerializeField, Min(0f), Tooltip("Tiles further than this from every player are dropped")] private float unloadRadius = 800f;
        [SerializeField, Tooltip("Editor only: where the heights come from (an OverworldSourceAsset), for the menu's generate and bake")] private ScriptableObject source;
        [SerializeField, Tooltip("Nature and buildings to dress the world with (ticket 49); none leaves it bare")] private EnvironmentAsset environment;

        private TiledGround _ground;
        private TileStreamer _streamer;
        private SimulationRunner _runner;
        private readonly Dictionary<TileCoord, (MeshRenderer far, GameObject near)> _shown = new Dictionary<TileCoord, (MeshRenderer, GameObject)>();
        private EnvironmentView _environmentView;
        private EnvironmentScatter.World _scatter;

        /// <summary>What a tile brings besides its ground: its near mesh, and its nature (ticket 49).</summary>
        private sealed class TileShown
        {
            public FlatTerrainMesh.MeshData Mesh;
            public Matrix4x4[][] Props;
        }

        public OverworldAsset Overworld => overworld;
        public ScriptableObject Source => source;
        public EnvironmentAsset Environment => environment;

        /// <summary>The nature on the loaded tiles, once the scene is playing (null with no environment).</summary>
        public EnvironmentView EnvironmentView => _environmentView;
        public TiledGround Ground => _ground;
        public TileStreamer Streamer => _streamer;

        /// <summary>The shown ground's root, once built.</summary>
        public GameObject Shown { get; private set; }

        /// <summary>The tiles shown in full detail now (their near mesh is up).</summary>
        public IEnumerable<TileCoord> ShownNear => _shown.Where(p => p.Value.near != null).Select(p => p.Key);

        /// <summary>Gives the runner's config this world. Does nothing (flat ground) without a baked asset.</summary>
        public void ApplyTo(SimulationConfig config)
        {
            if (overworld == null || !overworld.IsBaked)
            {
                Debug.LogWarning($"{name}: no baked overworld yet, so the ground is flat. Run Hunter System > Overworld > Bake.", this);
                return;
            }
            _ground ??= overworld.CreateTiledGround();
            overworld.ApplyTo(config, _ground);
        }

        /// <summary>Changes the streaming radii (tests, and tuning in play).</summary>
        public void SetRadii(float load, float unload)
        {
            loadRadius = load;
            unloadRadius = unload;
            if (_streamer != null) _streamer = NewStreamer();
        }

        private void Awake()
        {
            if (!Application.isPlaying) return;
            if (GetComponent<MountHud>() == null) gameObject.AddComponent<MountHud>(); // says why R does nothing (ticket 42)
            foreach (var terrain in FindObjectsByType<Terrain>(FindObjectsSortMode.None)) terrain.gameObject.SetActive(false);
            if (overworld == null || !overworld.IsBaked) return;
            _ground ??= overworld.CreateTiledGround();
            overworld.DistanceToRoad(0f, 0f); // fills its cache here, so tile meshes can read it off the main thread
            overworld.IsUnderBuilding(0f, 0f); // likewise
            _scatter = ScatterWorld(environment, overworld);
            _environmentView = environment != null ? new EnvironmentView(environment, _ground.TileSize) : null;
            Shown = BuildFarGround();
            EnvironmentView.PlaceBuildings(environment, overworld, Shown.transform);
            BuildBridges(Shown.transform);
            _streamer = NewStreamer();

            // The first tiles load before anyone moves: around the scene's players and the town.
            var starts = FindObjectsByType<PlayerView>(FindObjectsSortMode.None).Select(v => ToVec3(v.transform.position)).ToList();
            if (overworld.HasTown) starts.Add(ToVec3(overworld.TownCentre));
            _streamer.LoadNow(starts);
        }

        /// <summary>
        /// Draws each bridge (developer playtest, 2026-11-17): a deck along its points, a rail each side, and piers
        /// down into the water every few segments. Greybox shapes until there is a model; the water shows under it.
        /// </summary>
        private void BuildBridges(Transform parent)
        {
            if (overworld.Bridges.Count == 0) return;
            var root = new GameObject("Bridges").transform;
            root.SetParent(parent, false);
            var deckMaterial = SurfaceMaterial(TerrainSurface.Road);
            var stoneMaterial = SurfaceMaterial(TerrainSurface.Rock);
            foreach (var bridge in overworld.Bridges)
            {
                if (bridge == null || bridge.points == null || bridge.points.Count < 2) continue;
                var holder = new GameObject("Bridge: " + bridge.label).transform;
                holder.SetParent(root, false);
                for (var i = 0; i + 1 < bridge.points.Count; i++)
                {
                    var a = bridge.points[i];
                    var b = bridge.points[i + 1];
                    var along = b - a;
                    if (along.sqrMagnitude < 1e-4f) continue;
                    var rotation = Quaternion.LookRotation(along.normalized, Vector3.up);
                    var middle = (a + b) / 2f;
                    var length = along.magnitude + 0.05f;
                    var width = bridge.halfWidth * 2f;
                    Block(holder, "Deck", middle + rotation * new Vector3(0f, -0.25f, 0f), rotation, new Vector3(width, 0.5f, length), deckMaterial);
                    foreach (var side in new[] { -1f, 1f })
                        Block(holder, "Rail", middle + rotation * new Vector3(side * (bridge.halfWidth - 0.15f), 0.5f, 0f), rotation, new Vector3(0.3f, 1f, length), stoneMaterial);
                    if (i % 3 == 1)
                        Block(holder, "Pier", a + new Vector3(0f, -6.25f, 0f), Quaternion.Euler(0f, rotation.eulerAngles.y, 0f), new Vector3(width * 0.8f, 12f, 1.2f), stoneMaterial);
                }
            }
        }

        private Material SurfaceMaterial(TerrainSurface surface)
        {
            var index = (int)surface;
            return surfaceMaterials != null && index < surfaceMaterials.Length ? surfaceMaterials[index] : null;
        }

        private static void Block(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            Destroy(block.GetComponent<Collider>());
            block.transform.SetParent(parent, false);
            block.transform.SetPositionAndRotation(position, rotation);
            block.transform.localScale = scale;
            if (material != null) block.GetComponent<Renderer>().sharedMaterial = material;
        }

        private TileStreamer NewStreamer()
        {
            if (_streamer != null)
                foreach (var tile in _ground.Loaded.ToList()) { _ground.RemoveTile(tile); OnTileUnloaded(tile); }
            var streamer = new TileStreamer(_ground, LoadTile, loadRadius, unloadRadius);
            streamer.TileLoaded += OnTileLoaded;
            streamer.TileUnloaded += OnTileUnloaded;
            return streamer;
        }

        private void Start() => _runner = FindAnyObjectByType<SimulationRunner>();

        private void LateUpdate()
        {
            if (_environmentView == null) return;
            var eye = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            _environmentView.Draw(eye);
        }

        /// <summary>
        /// The scatter's view of this world (ticket 49), made on the main thread: the kinds' rules, the sea and
        /// beach, roads, and the clear areas (the town, each gate entrance, each building).
        /// </summary>
        public static EnvironmentScatter.World ScatterWorld(EnvironmentAsset environment, OverworldAsset overworld)
        {
            if (environment == null || overworld == null) return null;
            var clear = new List<EnvironmentScatter.ClearArea>();
            if (overworld.HasTown) clear.Add(new EnvironmentScatter.ClearArea(overworld.TownCentre.x, overworld.TownCentre.z, overworld.TownRadius));
            foreach (var gate in overworld.Gates) clear.Add(new EnvironmentScatter.ClearArea(gate.entrance.x, gate.entrance.z, 6f));
            foreach (var b in overworld.Buildings)
                if (b != null) clear.Add(new EnvironmentScatter.ClearArea(b.position.x, b.position.z, b.footprint.magnitude / 2f + 1f));
            var kinds = new EnvironmentScatter.Rules[environment.props.Length];
            for (var i = 0; i < kinds.Length; i++)
                kinds[i] = environment.props[i] != null ? EnvironmentScatter.Rules.From(environment.props[i], i) : default;
            overworld.DistanceToRoad(0f, 0f);
            return new EnvironmentScatter.World
            {
                Seed = environment.seed, Kinds = kinds, SeaLevel = overworld.SeaLevel, SandAboveSea = overworld.SandAboveSea,
                DistanceToRoad = overworld.DistanceToRoad, Clear = clear.ToArray(),
            };
        }

        private void Update()
        {
            if (_streamer == null) return;
            if (_runner == null) _runner = FindAnyObjectByType<SimulationRunner>();
            var world = _runner != null ? _runner.World : null;
            if (world == null) return;
            _streamer.Update(Centres(world));
            GiveMountForTesting(world);
        }

        /// <summary>
        /// Development builds only: F2 gives the local player's character a mount (ticket 42) before the D-rank
        /// promotion brings it (ticket 25). On the host or offline; a guest's world is the host's to change.
        /// </summary>
        private void GiveMountForTesting(SimulationWorld world)
        {
            if (!Debug.isDebugBuild || UnityEngine.InputSystem.Keyboard.current == null || !UnityEngine.InputSystem.Keyboard.current.f2Key.wasPressedThisFrame) return;
            if (_runner.IsOnline && _runner.Host == null) return;
            var me = _runner.LocalPlayerId;
            if (me.HasValue && world.HasPlayer(me.Value)) world.GrantMount(me.Value);
        }

        /// <summary>Who tiles are loaded for: a client's own player; on the host or offline, every player (ADR 0020).</summary>
        private IEnumerable<Vec3> Centres(SimulationWorld world)
        {
            var isClient = _runner.IsOnline && _runner.Host == null;
            if (isClient)
            {
                var me = _runner.LocalPlayerId;
                if (me.HasValue && world.HasPlayer(me.Value)) yield return world.GetPlayer(me.Value).Position;
                yield break;
            }
            foreach (var id in world.PlayerIds) yield return world.GetPlayer(id).Position;
        }

        /// <summary>Reads, decodes and builds a tile's near mesh contents off the main thread.</summary>
        private Task<TilePayload> LoadTile(TileCoord tile)
        {
            var path = overworld.TilePath(tile);
            var corner = _ground.CornerOf(tile);
            var size = _ground.TileSize;
            var cell = overworld.MeshCellSize;
            var sea = overworld.SeaLevel;
            var sand = overworld.SandAboveSea;
            var asset = overworld;
            var scatter = _scatter;
            return Task.Run(() =>
            {
                var ground = GroundBake.Decode(System.IO.File.ReadAllBytes(path));
                var mesh = FlatTerrainMesh.BuildData(ground, corner.X, corner.Z, size, cell,
                    at => FlatTerrainMesh.SurfaceAt(ground, at, sea, sand, asset.DistanceToRoad, asset.IsUnderBuilding));
                var props = scatter != null ? EnvironmentScatter.Scatter(ground, corner.X, corner.Z, size, scatter) : null;
                return new TilePayload { Ground = ground, Extra = new TileShown { Mesh = mesh, Props = props } };
            });
        }

        private void OnTileLoaded(TileCoord tile, TilePayload payload)
        {
            if (!_shown.TryGetValue(tile, out var shown) || payload.Extra is not TileShown extra) return;
            if (shown.near != null) Destroy(shown.near);
            var near = MeshObject("Near", shown.far.transform.parent, FlatTerrainMesh.ToMesh(extra.Mesh), SurfaceMaterials());
            shown.far.enabled = false;
            _shown[tile] = (shown.far, near.gameObject);
            var corner = _ground.CornerOf(tile);
            _environmentView?.AddTile(tile, new Vector3(corner.X + _ground.TileSize / 2f, 0f, corner.Z + _ground.TileSize / 2f), extra.Props);
        }

        private void OnTileUnloaded(TileCoord tile)
        {
            _environmentView?.RemoveTile(tile);
            if (!_shown.TryGetValue(tile, out var shown)) return;
            if (shown.near != null)
            {
                Destroy(shown.near.GetComponent<MeshFilter>().sharedMesh);
                Destroy(shown.near);
            }
            shown.far.enabled = true;
            _shown[tile] = (shown.far, null);
        }

        /// <summary>The ground players see before any tile loads: every tile's far version from the coarse map, the sea, and the gates' pillars.</summary>
        private GameObject BuildFarGround()
        {
            var coarse = overworld.LoadCoarse();
            var root = new GameObject("Overworld (shown)");
            root.transform.SetParent(transform, false);
            for (var z = 0; z < _ground.TilesZ; z++)
            for (var x = 0; x < _ground.TilesX; x++)
            {
                var tile = new TileCoord(x, z);
                var corner = _ground.CornerOf(tile);
                var tileObject = new GameObject($"Tile {x},{z}");
                tileObject.transform.SetParent(root.transform, false);
                tileObject.transform.position = new Vector3(corner.X, 0f, corner.Z);
                var far = MeshObject("Far", tileObject.transform,
                    FlatTerrainMesh.Build(coarse, corner.X, corner.Z, _ground.TileSize, overworld.FarMeshCellSize,
                        at => FlatTerrainMesh.SurfaceAt(coarse, at, overworld.SeaLevel, overworld.SandAboveSea, overworld.DistanceToRoad, overworld.IsUnderBuilding)),
                    SurfaceMaterials());
                _shown[tile] = (far, null);
            }

            if (waterMaterial != null)
            {
                var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
                water.name = "Sea";
                Destroy(water.GetComponent<Collider>());
                water.transform.SetParent(root.transform, false);
                var width = _ground.MaxX - _ground.OriginX;
                var depth = _ground.MaxZ - _ground.OriginZ;
                water.transform.position = new Vector3(_ground.OriginX + width / 2f, overworld.SeaLevel, _ground.OriginZ + depth / 2f);
                water.transform.localScale = new Vector3(width / 10f, 1f, depth / 10f); // a plane is 10 m across
                water.GetComponent<Renderer>().sharedMaterial = waterMaterial;
            }

            foreach (var site in overworld.Gates)
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"Gate: {site.gateId}";
                Destroy(pillar.GetComponent<Collider>());
                pillar.transform.SetParent(root.transform, false);
                pillar.transform.position = site.entrance + Vector3.up * 2f;
                pillar.transform.localScale = new Vector3(2.5f, 2f, 2.5f); // 4 m tall
                if (gateMaterial != null) pillar.GetComponent<Renderer>().sharedMaterial = gateMaterial;
            }
            return root;
        }

        /// <summary>A material for every surface: rivers and lakes use the water material when the list stops short of them.</summary>
        private Material[] SurfaceMaterials()
        {
            if (surfaceMaterials.Length >= FlatTerrainMesh.SurfaceCount) return surfaceMaterials;
            var all = new Material[FlatTerrainMesh.SurfaceCount];
            for (var i = 0; i < all.Length; i++)
                all[i] = i < surfaceMaterials.Length ? surfaceMaterials[i] : i == (int)TerrainSurface.Water ? waterMaterial : surfaceMaterials.LastOrDefault();
            return all;
        }

        private static MeshRenderer MeshObject(string name, Transform parent, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            return renderer;
        }

        private static Vec3 ToVec3(Vector3 v) => new Vec3(v.x, v.y, v.z);

        /// <summary>For editor tooling.</summary>
        public void SetEnvironment(EnvironmentAsset dressing) => environment = dressing;

        /// <summary>For editor tooling.</summary>
        public void Set(OverworldAsset asset, ScriptableObject heightSource, Material[] surfaces, Material water, Material gates, EnvironmentAsset dressing = null)
        {
            if (dressing != null) environment = dressing;
            overworld = asset;
            source = heightSource;
            surfaceMaterials = surfaces;
            waterMaterial = water;
            gateMaterial = gates;
        }
    }
}
