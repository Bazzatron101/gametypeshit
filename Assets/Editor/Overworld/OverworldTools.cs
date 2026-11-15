using System.Collections.Generic;
using System.IO;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HunterSystem.Editor
{
    /// <summary>
    /// The overworld terrain pipeline (ticket 39), for the open scene's <see cref="OverworldScene"/>:
    /// <list type="bullet">
    /// <item><b>Generate</b> makes or refreshes one Unity Terrain per tile from the source's heights. Sculpting
    /// done on the tiles since the last generation is kept: each tile remembers what was last generated
    /// (Tile_x_z.base.bytes beside its terrain data), and the difference is laid on the new heights. Painted
    /// climbable faces (the terrain's second layer) are kept too.</item>
    /// <item><b>Bake</b> reads the tiles into the simulation's ground (heights and kinds: too steep is blocked,
    /// under the sea is water, painted climbable faces are climbable) and every <see cref="OverworldMarker"/>
    /// into the overworld asset. The ground players see is built from that same bake when the scene loads.</item>
    /// </list>
    /// </summary>
    public static class OverworldTools
    {
        private const string TerrainRootName = "Overworld Terrain (edit)";

        [MenuItem("Hunter System/Overworld/Generate Terrain Tiles")]
        public static void GenerateFromMenu() => Generate(SceneOverworld(), SceneSource());

        [MenuItem("Hunter System/Overworld/Bake")]
        public static void BakeFromMenu() => Bake(SceneOverworld(), SceneSource());

        private static OverworldScene SceneOverworld()
        {
            var scene = Object.FindAnyObjectByType<OverworldScene>();
            if (scene == null) throw new System.InvalidOperationException("The open scene has no OverworldScene.");
            return scene;
        }

        private static OverworldSourceAsset SceneSource()
        {
            var source = SceneOverworld().Source as OverworldSourceAsset;
            if (source == null) throw new System.InvalidOperationException("The OverworldScene has no OverworldSourceAsset as its source.");
            return source;
        }

        // --- Generate --------------------------------------------------------------------------------

        public static Terrain[,] Generate(OverworldScene scene, OverworldSourceAsset source)
        {
            EnsureFolders(source.TerrainFolder);
            var root = GameObject.Find(TerrainRootName) ?? new GameObject(TerrainRootName);
            root.tag = "EditorOnly"; // the editing surface never goes into a build
            root.transform.SetParent(scene.transform, false);
            var layers = TerrainLayers(source.TerrainFolder);
            var material = TerrainMaterial(source.TerrainFolder);

            var res = source.heightmapResolution;
            var terrains = new Terrain[source.tilesX, source.tilesZ];
            for (var j = 0; j < source.tilesZ; j++)
            for (var i = 0; i < source.tilesX; i++)
            {
                var dataPath = $"{source.TerrainFolder}/Tile_{i}_{j}.asset";
                var basePath = $"{source.TerrainFolder}/Tile_{i}_{j}.base.bytes";
                var data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
                var existed = data != null && data.heightmapResolution == res;
                if (data == null)
                {
                    data = new TerrainData();
                    AssetDatabase.CreateAsset(data, dataPath);
                }
                if (!existed) data.heightmapResolution = res;
                data.size = new Vector3(source.tileSize, source.maxHeight, source.tileSize);
                data.alphamapResolution = source.CellsPerTile;
                data.terrainLayers = layers;

                var minX = source.origin.x + i * source.tileSize;
                var minZ = source.origin.y + j * source.tileSize;
                var step = source.tileSize / source.CellsPerTile;
                var generated = new float[res * res];
                for (var z = 0; z < res; z++)
                for (var x = 0; x < res; x++)
                    generated[z * res + x] = source.heights.Sample(minX + x * step, minZ + z * step, source.origin, source.AreaSize) / source.maxHeight;

                var current = existed ? Flatten(data.GetHeights(0, 0, res, res)) : null;
                var lastGenerated = existed && File.Exists(basePath) ? ReadFloats(basePath, res * res) : null;
                data.SetHeights(0, 0, Unflatten(GroundBake.KeepHandEdits(current, lastGenerated, generated, 0f, 1f), res));
                WriteFloats(basePath, generated);

                // Climbable faces: the second layer. Stamps add to whatever was painted by hand.
                var cells = source.CellsPerTile;
                var painted = existed && data.alphamapLayers == 2 ? data.GetAlphamaps(0, 0, cells, cells) : null;
                var alpha = new float[cells, cells, 2];
                for (var z = 0; z < cells; z++)
                for (var x = 0; x < cells; x++)
                {
                    var climb = source.heights.IsClimbable(minX + (x + 0.5f) * step, minZ + (z + 0.5f) * step) ? 1f : 0f;
                    if (painted != null) climb = Mathf.Max(climb, painted[z, x, 1]);
                    alpha[z, x, 0] = 1f - climb;
                    alpha[z, x, 1] = climb;
                }
                data.SetAlphamaps(0, 0, alpha);
                EditorUtility.SetDirty(data);

                var name = $"Tile {i},{j}";
                var found = root.transform.Find(name);
                var tileObject = found != null ? found.gameObject : null;
                if (tileObject == null)
                {
                    tileObject = Terrain.CreateTerrainGameObject(data);
                    tileObject.name = name;
                    tileObject.transform.SetParent(root.transform, false);
                }
                tileObject.tag = "EditorOnly";
                tileObject.transform.position = new Vector3(minX, 0f, minZ);
                var terrain = tileObject.GetComponent<Terrain>();
                terrain.terrainData = data;
                var collider = tileObject.GetComponent<TerrainCollider>();
                if (collider != null) collider.terrainData = data;
                if (material != null) terrain.materialTemplate = material;
                terrains[i, j] = terrain;
            }

            for (var j = 0; j < source.tilesZ; j++)
            for (var i = 0; i < source.tilesX; i++)
                terrains[i, j].SetNeighbors(
                    i > 0 ? terrains[i - 1, j] : null, j < source.tilesZ - 1 ? terrains[i, j + 1] : null,
                    i < source.tilesX - 1 ? terrains[i + 1, j] : null, j > 0 ? terrains[i, j - 1] : null);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene.gameObject.scene);
            return terrains;
        }

        // --- Bake ------------------------------------------------------------------------------------

        public static OverworldAsset Bake(OverworldScene scene, OverworldSourceAsset source)
        {
            var root = GameObject.Find(TerrainRootName) ?? throw new System.InvalidOperationException("Generate the terrain tiles first.");
            var cellsPerTile = source.CellsPerTile;
            var columns = source.tilesX * cellsPerTile;
            var rows = source.tilesZ * cellsPerTile;
            var cellSize = source.tileSize / cellsPerTile;
            var heights = new float[(columns + 1) * (rows + 1)];
            var climbable = new bool[columns * rows];

            for (var j = 0; j < source.tilesZ; j++)
            for (var i = 0; i < source.tilesX; i++)
            {
                var tileTransform = root.transform.Find($"Tile {i},{j}");
                var terrain = tileTransform != null ? tileTransform.GetComponent<Terrain>() : null;
                var data = terrain != null ? terrain.terrainData : null;
                if (data == null) throw new System.InvalidOperationException($"Tile {i},{j} is missing: generate the terrain tiles first.");
                var tileHeights = data.GetHeights(0, 0, cellsPerTile + 1, cellsPerTile + 1);
                for (var z = 0; z <= cellsPerTile; z++)
                for (var x = 0; x <= cellsPerTile; x++)
                    heights[(j * cellsPerTile + z) * (columns + 1) + i * cellsPerTile + x] = tileHeights[z, x] * source.maxHeight;
                if (data.alphamapLayers < 2) continue;
                var alpha = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
                for (var z = 0; z < cellsPerTile; z++)
                for (var x = 0; x < cellsPerTile; x++)
                {
                    var ax = Mathf.Min(x * data.alphamapWidth / cellsPerTile, data.alphamapWidth - 1);
                    var az = Mathf.Min(z * data.alphamapHeight / cellsPerTile, data.alphamapHeight - 1);
                    climbable[(j * cellsPerTile + z) * columns + i * cellsPerTile + x] = alpha[az, ax, 1] > 0.5f;
                }
            }

            var kinds = GroundBake.Classify(heights, columns, rows, cellSize, source.rules, (c, r) => climbable[r * columns + c]);
            EnsureFolders("Assets/Data/Overworld");
            File.WriteAllBytes(source.GroundPath, GroundBake.Encode(source.origin.x, source.origin.y, cellSize, columns, rows, heights, kinds));
            AssetDatabase.ImportAsset(source.GroundPath, ImportAssetOptions.ForceUpdate);
            var groundAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(source.GroundPath);
            var ground = new HeightmapGround(source.origin.x, source.origin.y, cellSize, columns, rows, heights, kinds);

            var gates = new List<OverworldAsset.GateSiteData>();
            var roads = new List<OverworldAsset.RoadData>();
            var landmarks = new List<OverworldAsset.LandmarkData>();
            OverworldMarker town = null;
            foreach (var marker in Object.FindObjectsByType<OverworldMarker>(FindObjectsSortMode.InstanceID))
            {
                var at = OnGround(ground, marker.transform.position);
                switch (marker.Kind)
                {
                    case OverworldMarkerKind.Gate:
                        if (marker.Gate == null) { Debug.LogWarning($"Gate marker {marker.name} names no gate; skipped.", marker); break; }
                        gates.Add(new OverworldAsset.GateSiteData { gateId = marker.Gate.Id, entrance = at, region = marker.Region, knownFromStart = marker.KnownFromStart });
                        break;
                    case OverworldMarkerKind.Town:
                        town = marker;
                        break;
                    case OverworldMarkerKind.Road:
                        roads.Add(new OverworldAsset.RoadData { halfWidth = marker.Radius, points = marker.RoadPoints().Select(p => OnGround(ground, p)).ToList() });
                        break;
                    case OverworldMarkerKind.Landmark:
                        landmarks.Add(new OverworldAsset.LandmarkData { label = marker.Label, position = at });
                        break;
                }
            }
            gates.Sort((a, b) => string.CompareOrdinal(a.gateId, b.gateId));

            var asset = AssetDatabase.LoadAssetAtPath<OverworldAsset>(source.OverworldPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<OverworldAsset>();
                AssetDatabase.CreateAsset(asset, source.OverworldPath);
            }
            asset.Set(groundAsset, gates, town != null, town != null ? OnGround(ground, town.transform.position) : Vector3.zero, town != null ? town.Radius : 0f,
                roads, landmarks, source.tileSize, source.rules.seaLevel, source.meshCellSize, source.farMeshCellSize);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"Baked {source.outputName}: {columns} × {rows} cells of {cellSize:0.###} m, {gates.Count} gates, {roads.Count} roads, " +
                      $"{kinds.Count(k => k == GroundKind.Blocked)} blocked, {kinds.Count(k => k == GroundKind.Water)} water, {kinds.Count(k => k == GroundKind.Climbable)} climbable cells.");
            return AssetDatabase.LoadAssetAtPath<OverworldAsset>(source.OverworldPath);
        }

        private static Vector3 OnGround(IGround ground, Vector3 p) => new Vector3(p.x, ground.HeightAt(new Vec3(p.x, 0f, p.z)), p.z);

        // --- Helpers ---------------------------------------------------------------------------------

        private static TerrainLayer[] TerrainLayers(string folder)
        {
            return new[]
            {
                TerrainLayer(folder, "Ground", new Color(0.45f, 0.6f, 0.32f)),
                TerrainLayer(folder, "Climbable", new Color(0.55f, 0.42f, 0.3f)),
            };
        }

        private static TerrainLayer TerrainLayer(string folder, string name, Color colour)
        {
            var path = $"{folder}/{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer != null) return layer;
            var texturePath = $"{folder}/{name}.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = new Texture2D(4, 4);
                texture.SetPixels(Enumerable.Repeat(colour, 16).ToArray());
                texture.Apply();
                AssetDatabase.CreateAsset(texture, texturePath);
            }
            layer = new TerrainLayer { diffuseTexture = texture, tileSize = new Vector2(10f, 10f) };
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        private static Material TerrainMaterial(string folder)
        {
            var path = $"{folder}/TerrainEdit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null) return null;
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolders(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static float[] Flatten(float[,] grid)
        {
            var rows = grid.GetLength(0);
            var columns = grid.GetLength(1);
            var flat = new float[rows * columns];
            for (var z = 0; z < rows; z++)
            for (var x = 0; x < columns; x++)
                flat[z * columns + x] = grid[z, x];
            return flat;
        }

        private static float[,] Unflatten(float[] flat, int res)
        {
            var grid = new float[res, res];
            for (var z = 0; z < res; z++)
            for (var x = 0; x < res; x++)
                grid[z, x] = flat[z * res + x];
            return grid;
        }

        private static void WriteFloats(string path, float[] values)
        {
            using var w = new BinaryWriter(File.Create(path));
            foreach (var v in values) w.Write(v);
        }

        private static float[] ReadFloats(string path, int count)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length != count * 4) return null; // a different resolution: start over
            var values = new float[count];
            System.Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
    }
}
