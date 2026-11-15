using System;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Makes a scene an overworld scene (ticket 39). The simulation runner gives its config this scene's
    /// baked ground and markers. When the scene loads, the ground players see is built from that same
    /// baked ground: a flat-shaded mesh per tile with a coarser far version (ADR 0002), the sea, and a
    /// pillar at each gate's entrance. The Unity Terrain tiles are only the editing surface: tagged
    /// EditorOnly, so builds never carry them, and hidden in play in the editor.
    /// </summary>
    [DefaultExecutionOrder(-100)] // before the runner reads it
    public sealed class OverworldScene : MonoBehaviour
    {
        [SerializeField] private OverworldAsset overworld;
        [SerializeField, Tooltip("One per TerrainSurface: grass, rock, climbable, sand, road")] private Material[] surfaceMaterials = new Material[0];
        [SerializeField] private Material waterMaterial;
        [SerializeField] private Material gateMaterial;
        [SerializeField, Tooltip("Editor only: where the heights come from (an OverworldSourceAsset), for the menu's generate and bake")] private ScriptableObject source;

        private HeightmapGround _ground;

        public OverworldAsset Overworld => overworld;
        public ScriptableObject Source => source;

        /// <summary>The shown ground's root, once built.</summary>
        public GameObject Shown { get; private set; }

        /// <summary>Gives the runner's config this world. Does nothing (flat ground) without a baked asset.</summary>
        public void ApplyTo(SimulationConfig config)
        {
            if (overworld == null || overworld.GroundData == null)
            {
                Debug.LogWarning($"{name}: no baked overworld yet, so the ground is flat. Run Hunter System > Overworld > Bake.", this);
                return;
            }
            _ground ??= overworld.LoadGround();
            overworld.ApplyTo(config, _ground);
        }

        private void Awake()
        {
            if (!Application.isPlaying) return;
            foreach (var terrain in FindObjectsByType<Terrain>(FindObjectsSortMode.None)) terrain.gameObject.SetActive(false);
            if (overworld == null || overworld.GroundData == null) return;
            _ground = overworld.LoadGround();
            Shown = BuildShownGround();
        }

        /// <summary>The ground players see: tiles of flat-shaded mesh (near and far versions), the sea and the gates' pillars.</summary>
        public GameObject BuildShownGround()
        {
            _ground ??= overworld.LoadGround();
            var root = new GameObject("Overworld (shown)");
            root.transform.SetParent(transform, false);
            var tile = overworld.TileSize;
            var tilesX = Mathf.Max(1, Mathf.CeilToInt((_ground.MaxX - _ground.OriginX) / tile - 1e-3f));
            var tilesZ = Mathf.Max(1, Mathf.CeilToInt((_ground.MaxZ - _ground.OriginZ) / tile - 1e-3f));
            Func<Vector3, TerrainSurface> surface = at =>
                FlatTerrainMesh.SurfaceAt(_ground, at, overworld.SeaLevel, overworld.SandAboveSea, overworld.DistanceToRoad);

            for (var j = 0; j < tilesZ; j++)
            for (var i = 0; i < tilesX; i++)
            {
                var minX = _ground.OriginX + i * tile;
                var minZ = _ground.OriginZ + j * tile;
                var size = Mathf.Min(tile, Mathf.Min(_ground.MaxX - minX, _ground.MaxZ - minZ));
                var tileObject = new GameObject($"Tile {i},{j}");
                tileObject.transform.SetParent(root.transform, false);
                tileObject.transform.position = new Vector3(minX, 0f, minZ);
                var near = MeshObject("Near", tileObject.transform, FlatTerrainMesh.Build(_ground, minX, minZ, size, overworld.MeshCellSize, surface), surfaceMaterials);
                var far = MeshObject("Far", tileObject.transform, FlatTerrainMesh.Build(_ground, minX, minZ, size, overworld.FarMeshCellSize, surface), surfaceMaterials);
                var lods = tileObject.AddComponent<LODGroup>();
                lods.SetLODs(new[]
                {
                    new LOD(overworld.NearLodScreenHeight, new Renderer[] { near }),
                    new LOD(0f, new Renderer[] { far }), // never culled: the far version stays to the horizon
                });
                lods.RecalculateBounds();
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
                var y = _ground.HeightAt(new Vec3(site.entrance.x, 0f, site.entrance.z));
                pillar.transform.position = new Vector3(site.entrance.x, y + 2f, site.entrance.z);
                pillar.transform.localScale = new Vector3(2.5f, 2f, 2.5f); // 4 m tall
                if (gateMaterial != null) pillar.GetComponent<Renderer>().sharedMaterial = gateMaterial;
            }
            return root;
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

        /// <summary>For editor tooling.</summary>
        public void Set(OverworldAsset asset, ScriptableObject heightSource, Material[] surfaces, Material water, Material gates)
        {
            overworld = asset;
            source = heightSource;
            surfaceMaterials = surfaces;
            waterMaterial = water;
            gateMaterial = gates;
        }
    }
}
