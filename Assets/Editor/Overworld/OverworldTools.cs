using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

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

        /// <summary>
        /// For batch mode: opens the scene named by -scene (default the test patch), bakes it and saves it. Unlike a
        /// rebuild it leaves the game content alone. Unity -batchmode -quit -executeMethod HunterSystem.Editor.OverworldTools.BakeSceneBatch -scene Assets/Scenes/OverworldPatch.unity
        /// </summary>
        public static void BakeSceneBatch()
        {
            var args = Environment.GetCommandLineArgs();
            var at = Array.IndexOf(args, "-scene");
            var path = at >= 0 && at + 1 < args.Length ? args[at + 1] : GreyboxSceneBuilder.OverworldPatchScenePath;
            var opened = EditorSceneManager.OpenScene(path);
            Bake(SceneOverworld(), SceneSource());
            EditorSceneManager.SaveScene(opened);
        }

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
            source.heights.Refresh();
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
                if (source.bakeFromRecipe && !source.sculptTiles.Contains(new Vector2Int(i, j))) continue; // a big map: only the tiles chosen for sculpting
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
            // A scene made before ticket 49 is given the environment, so it is dressed once baked.
            if (scene.Environment == null && AssetDatabase.LoadAssetAtPath<EnvironmentAsset>(EnvironmentArtSetup.EnvironmentPath) is { } environment)
            {
                scene.SetEnvironment(environment);
                EditorUtility.SetDirty(scene);
                EditorSceneManager.MarkSceneDirty(scene.gameObject.scene);
            }
            source.heights.Refresh();
            if (source.bakeFromRecipe) return BakeFromRecipe(source);
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

            // Rivers and lakes from the painted water map are water even above sea level; a road over one is a bridge or ford.
            var areaMin = source.origin;
            var areaSize = source.AreaSize;
            var kinds = GroundBake.Classify(heights, columns, rows, cellSize, source.rules, (c, r) => climbable[r * columns + c],
                (c, r) => source.heights.IsPaintedWater(areaMin.x + (c + 0.5f) * cellSize, areaMin.y + (r + 0.5f) * cellSize, areaMin, areaSize));
            var ground = new HeightmapGround(source.origin.x, source.origin.y, cellSize, columns, rows, heights, kinds);

            // Towns and villages get their buildings (ticket 49), and nobody walks through them: their footprints are blocked.
            var buildings = PlanBuildings(source, (x, z) => ground.HeightAt(new Vec3(x, 0f, z)), (x, z) => ground.KindAt(new Vec3(x, 0f, z)) == GroundKind.Walkable);
            BlockFootprints(kinds, columns, rows, source.origin.x, source.origin.y, cellSize, buildings);
            ground = new HeightmapGround(source.origin.x, source.origin.y, cellSize, columns, rows, heights, kinds);

            // One file per tile, streamed in as players move (ticket 40, ADR 0020); each holds its own edge samples.
            EnsureFolders(source.TileFolderAsset);
            foreach (var old in Directory.GetFiles(source.TileFolderAsset, "tile_*.bytes")) File.Delete(old);
            for (var j = 0; j < source.tilesZ; j++)
            for (var i = 0; i < source.tilesX; i++)
            {
                var tileHeights = new float[(cellsPerTile + 1) * (cellsPerTile + 1)];
                var tileKinds = new GroundKind[cellsPerTile * cellsPerTile];
                for (var z = 0; z <= cellsPerTile; z++)
                for (var x = 0; x <= cellsPerTile; x++)
                {
                    tileHeights[z * (cellsPerTile + 1) + x] = heights[(j * cellsPerTile + z) * (columns + 1) + i * cellsPerTile + x];
                    if (x < cellsPerTile && z < cellsPerTile) tileKinds[z * cellsPerTile + x] = kinds[(j * cellsPerTile + z) * columns + i * cellsPerTile + x];
                }
                File.WriteAllBytes($"{source.TileFolderAsset}/{OverworldAsset.TileFileName(new TileCoord(i, j))}",
                    GroundBake.Encode(source.origin.x + i * source.tileSize, source.origin.y + j * source.tileSize, cellSize, cellsPerTile, cellsPerTile, tileHeights, tileKinds));
            }

            // The coarse map: the whole world at the far facet size, always loaded.
            var coarseColumns = Mathf.Max(1, Mathf.CeilToInt(source.AreaSize.x / source.farMeshCellSize));
            var coarseCellX = source.AreaSize.x / coarseColumns; // square cells: the rows may reach a little past a non-square map
            var coarseRows = Mathf.Max(1, Mathf.CeilToInt(source.AreaSize.y / coarseCellX - 1e-3f));
            var coarseHeights = new float[(coarseColumns + 1) * (coarseRows + 1)];
            var coarseKinds = new GroundKind[coarseColumns * coarseRows];
            for (var z = 0; z <= coarseRows; z++)
            for (var x = 0; x <= coarseColumns; x++)
            {
                var px = source.origin.x + x * coarseCellX;
                var pz = source.origin.y + z * coarseCellX;
                coarseHeights[z * (coarseColumns + 1) + x] = ground.HeightAt(new Vec3(px, 0f, pz));
                if (x < coarseColumns && z < coarseRows)
                    coarseKinds[z * coarseColumns + x] = ground.KindAt(new Vec3(px + coarseCellX / 2f, 0f, pz + coarseCellX / 2f));
            }
            EnsureFolders("Assets/Data/Overworld");
            File.WriteAllBytes(source.CoarsePath, GroundBake.Encode(source.origin.x, source.origin.y, coarseCellX, coarseColumns, coarseRows, coarseHeights, coarseKinds));
            if (File.Exists(source.OldGroundPath)) AssetDatabase.DeleteAsset(source.OldGroundPath); // ticket 39's one file for everything
            AssetDatabase.Refresh();
            var coarseAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(source.CoarsePath);

            var asset = WriteOverworldAsset(source, coarseAsset, p => OnGround(ground, p), buildings, out var gateCount, out var roadCount);
            Debug.Log($"Baked {source.outputName}: {columns} × {rows} cells of {cellSize:0.###} m, {gateCount} gates, {roadCount} roads, " +
                      $"{kinds.Count(k => k == GroundKind.Blocked)} blocked, {kinds.Count(k => k == GroundKind.Water)} water, {kinds.Count(k => k == GroundKind.Climbable)} climbable cells.");
            return asset;
        }

        private static Vector3 OnGround(IGround ground, Vector3 p) => new Vector3(p.x, ground.HeightAt(new Vec3(p.x, 0f, p.z)), p.z);

        // --- Bake from the recipe (big maps, ticket 45) ------------------------------------------------

        /// <summary>
        /// Bakes a big map tile by tile straight from the recipe, with no Unity Terrain for the whole area: each
        /// tile's heights are sampled from the heights recipe (or read from its Terrain, for a tile chosen for
        /// sculpting), classified (steep, sea, painted rivers and lakes, climbable stamps) and written to its
        /// streamed file. The coarse map is sampled from the recipe too. Memory stays at one tile.
        /// </summary>
        public static OverworldAsset BakeFromRecipe(OverworldSourceAsset source)
        {
            var clock = Stopwatch.StartNew();
            var heights = source.heights;
            heights.Refresh();
            var cellsPerTile = source.CellsPerTile;
            var cellSize = source.tileSize / cellsPerTile;
            var areaMin = source.origin;
            var areaSize = source.AreaSize;
            var root = GameObject.Find(TerrainRootName);

            EnsureFolders(source.TileFolderAsset);
            foreach (var old in Directory.GetFiles(source.TileFolderAsset, "tile_*.bytes")) File.Delete(old);
            long blocked = 0, water = 0, climbable = 0, sculpted = 0;
            var stride = cellsPerTile + 1;
            var tileHeights = new float[stride * stride];
            var tileClimb = new bool[cellsPerTile * cellsPerTile];
            var total = source.tilesX * source.tilesZ;
            var interactive = !Application.isBatchMode;

            // Towns and villages get their buildings (ticket 49), planned on the recipe's ground; each tile blocks the footprints on it.
            var dryAbove = source.rules.seaLevel + 1.5f;
            var buildings = PlanBuildings(source, (x, z) => heights.Sample(x, z, areaMin, areaSize),
                (x, z) => heights.Sample(x, z, areaMin, areaSize) >= dryAbove && !heights.IsPaintedWater(x, z, areaMin, areaSize));
            try
            {
                for (var j = 0; j < source.tilesZ; j++)
                for (var i = 0; i < source.tilesX; i++)
                {
                    var n = j * source.tilesX + i;
                    if (interactive && n % 8 == 0 && EditorUtility.DisplayCancelableProgressBar("Baking " + source.outputName, $"Tile {n + 1} of {total}", n / (float)total))
                        throw new OperationCanceledException("Bake cancelled.");
                    var minX = areaMin.x + i * source.tileSize;
                    var minZ = areaMin.y + j * source.tileSize;

                    var terrain = root != null ? root.transform.Find($"Tile {i},{j}")?.GetComponent<Terrain>() : null;
                    var data = terrain != null ? terrain.terrainData : null;
                    if (data != null && data.heightmapResolution == stride)
                    {
                        sculpted++;
                        var grid = data.GetHeights(0, 0, stride, stride);
                        for (var z = 0; z < stride; z++)
                        for (var x = 0; x < stride; x++)
                            tileHeights[z * stride + x] = grid[z, x] * source.maxHeight;
                        var alpha = data.alphamapLayers >= 2 ? data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight) : null;
                        for (var z = 0; z < cellsPerTile; z++)
                        for (var x = 0; x < cellsPerTile; x++)
                            tileClimb[z * cellsPerTile + x] = alpha != null &&
                                alpha[Mathf.Min(z * data.alphamapHeight / cellsPerTile, data.alphamapHeight - 1), Mathf.Min(x * data.alphamapWidth / cellsPerTile, data.alphamapWidth - 1), 1] > 0.5f;
                    }
                    else
                    {
                        for (var z = 0; z < stride; z++)
                        for (var x = 0; x < stride; x++)
                            tileHeights[z * stride + x] = Mathf.Clamp(heights.Sample(minX + x * cellSize, minZ + z * cellSize, areaMin, areaSize), 0f, source.maxHeight);
                        for (var z = 0; z < cellsPerTile; z++)
                        for (var x = 0; x < cellsPerTile; x++)
                            tileClimb[z * cellsPerTile + x] = heights.IsClimbable(minX + (x + 0.5f) * cellSize, minZ + (z + 0.5f) * cellSize);
                    }

                    var kinds = GroundBake.Classify(tileHeights, cellsPerTile, cellsPerTile, cellSize, source.rules,
                        (c, r) => tileClimb[r * cellsPerTile + c],
                        (c, r) => heights.IsPaintedWater(minX + (c + 0.5f) * cellSize, minZ + (r + 0.5f) * cellSize, areaMin, areaSize));
                    BlockFootprints(kinds, cellsPerTile, cellsPerTile, minX, minZ, cellSize, buildings);
                    foreach (var k in kinds)
                    {
                        if (k == GroundKind.Blocked) blocked++;
                        else if (k == GroundKind.Water) water++;
                        else if (k == GroundKind.Climbable) climbable++;
                    }
                    File.WriteAllBytes($"{source.TileFolderAsset}/{OverworldAsset.TileFileName(new TileCoord(i, j))}",
                        GroundBake.Encode(minX, minZ, cellSize, cellsPerTile, cellsPerTile, tileHeights, kinds));
                }
            }
            finally
            {
                if (interactive) EditorUtility.ClearProgressBar();
            }

            // The coarse map, sampled from the recipe at the far facet size.
            var coarseColumns = Mathf.Max(1, Mathf.CeilToInt(areaSize.x / source.farMeshCellSize));
            var coarseCell = areaSize.x / coarseColumns;
            var coarseRows = Mathf.Max(1, Mathf.CeilToInt(areaSize.y / coarseCell - 1e-3f));
            var coarseHeights = new float[(coarseColumns + 1) * (coarseRows + 1)];
            for (var z = 0; z <= coarseRows; z++)
            for (var x = 0; x <= coarseColumns; x++)
                coarseHeights[z * (coarseColumns + 1) + x] = Mathf.Clamp(heights.Sample(areaMin.x + x * coarseCell, areaMin.y + z * coarseCell, areaMin, areaSize), 0f, source.maxHeight);
            var coarseKinds = GroundBake.Classify(coarseHeights, coarseColumns, coarseRows, coarseCell, source.rules, null,
                (c, r) => heights.IsPaintedWater(areaMin.x + (c + 0.5f) * coarseCell, areaMin.y + (r + 0.5f) * coarseCell, areaMin, areaSize));
            EnsureFolders("Assets/Data/Overworld");
            File.WriteAllBytes(source.CoarsePath, GroundBake.Encode(areaMin.x, areaMin.y, coarseCell, coarseColumns, coarseRows, coarseHeights, coarseKinds));
            AssetDatabase.Refresh();
            var coarseAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(source.CoarsePath);

            Vector3 OnRecipe(Vector3 p) => new Vector3(p.x, heights.Sample(p.x, p.z, areaMin, areaSize), p.z);
            var asset = WriteOverworldAsset(source, coarseAsset, OnRecipe, buildings, out var gateCount, out var roadCount);
            Debug.Log($"Baked {source.outputName} from its recipe in {clock.Elapsed.TotalSeconds:0.0} s: {total} tiles ({sculpted} sculpted), " +
                      $"{gateCount} gates, {roadCount} roads, {buildings.Count} buildings, {blocked} blocked, {water} water, {climbable} climbable cells.");
            return asset;
        }

        /// <summary>The overworld asset from the scene's markers: gates, the town, roads and landmarks, each put on the ground.</summary>
        private static OverworldAsset WriteOverworldAsset(OverworldSourceAsset source, TextAsset coarseAsset, Func<Vector3, Vector3> onGround,
            List<OverworldAsset.BuildingData> buildings, out int gateCount, out int roadCount)
        {
            var gates = new List<OverworldAsset.GateSiteData>();
            var roads = new List<OverworldAsset.RoadData>();
            var landmarks = new List<OverworldAsset.LandmarkData>();
            OverworldMarker town = null;
            foreach (var marker in Object.FindObjectsByType<OverworldMarker>(FindObjectsSortMode.InstanceID))
            {
                var at = onGround(marker.transform.position);
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
                        roads.Add(new OverworldAsset.RoadData { halfWidth = marker.Radius, points = marker.RoadPoints().Select(onGround).ToList() });
                        break;
                    case OverworldMarkerKind.Landmark:
                    case OverworldMarkerKind.Village: // a village is named on the map like any landmark
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
            asset.Set(source.origin, source.tileSize, source.tilesX, source.tilesZ, source.TileFolder, coarseAsset,
                gates, town != null, town != null ? onGround(town.transform.position) : Vector3.zero, town != null ? town.Radius : 0f,
                roads, landmarks, source.rules.seaLevel, source.meshCellSize, source.farMeshCellSize, buildings);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            gateCount = gates.Count;
            roadCount = roads.Count;
            return AssetDatabase.LoadAssetAtPath<OverworldAsset>(source.OverworldPath);
        }

        // --- Buildings (ticket 49) ------------------------------------------------------------------------

        /// <summary>
        /// Lays out the buildings of the scene's town and villages (<see cref="TownPlanner"/>) from the environment's
        /// building kinds, on the ground given, off the roads and clear of the gate board. None without an environment.
        /// </summary>
        public static List<OverworldAsset.BuildingData> PlanBuildings(OverworldSourceAsset source, Func<float, float, float> heightAt, Func<float, float, bool> buildable)
        {
            var result = new List<OverworldAsset.BuildingData>();
            var environment = AssetDatabase.LoadAssetAtPath<EnvironmentAsset>(EnvironmentArtSetup.EnvironmentPath);
            if (environment == null || environment.buildings.Length == 0) return result;
            var kinds = environment.buildings.Where(b => b != null && !string.IsNullOrEmpty(b.id))
                .Select(b => new TownPlanner.Kind(b.id, b.role, b.footprint)).ToList();

            var markers = Object.FindObjectsByType<OverworldMarker>(FindObjectsSortMode.InstanceID);
            var roads = markers.Where(m => m.Kind == OverworldMarkerKind.Road)
                .Select(m => (line: m.RoadPoints().Select(p => new Vector2(p.x, p.z)).ToList(), halfWidth: m.Radius)).ToList();
            float DistanceToRoad(float x, float z)
            {
                var best = float.PositiveInfinity;
                foreach (var (line, halfWidth) in roads) best = Mathf.Min(best, HeightSource.DistanceToLine(new Vector2(x, z), line, out _) - halfWidth);
                return best;
            }

            var hubGuid = AssetDatabase.FindAssets("t:HubAsset").FirstOrDefault();
            var hubAsset = hubGuid != null ? AssetDatabase.LoadAssetAtPath<HubAsset>(AssetDatabase.GUIDToAssetPath(hubGuid)) : null;
            var hub = hubAsset != null ? hubAsset.ToDefinition() : null;
            var board = hub != null ? new Vector2(hub.BoardOffset.X, hub.BoardOffset.Z) : new Vector2(0f, 7f);

            foreach (var marker in markers.Where(m => m.Kind == OverworldMarkerKind.Town || m.Kind == OverworldMarkerKind.Village))
            {
                var centre = new Vector2(marker.transform.position.x, marker.transform.position.z);
                var isTown = marker.Kind == OverworldMarkerKind.Town;
                var site = new TownPlanner.Site
                {
                    Centre = centre,
                    Radius = marker.Radius,
                    Style = isTown ? TownStyle.GuildTown : marker.Castle ? TownStyle.Castle : TownStyle.Village,
                    SquareRadius = isTown ? environment.squareRadius : environment.squareRadius * 0.6f,
                    RoadClearance = environment.roadClearance,
                    Gap = environment.buildingGap,
                    HeightAt = heightAt,
                    Buildable = buildable,
                };
                if (roads.Count > 0) site.DistanceToRoad = DistanceToRoad;
                if (isTown) site.KeepClear.Add((centre + board, 4f));
                foreach (var p in TownPlanner.Plan(site, kinds))
                {
                    // It stands on its lowest corner, so no gap shows underneath on a gentle slope.
                    var low = TownPlanner.Corners(p, 0f).Append(p.Centre).Min(c => heightAt(c.x, c.y));
                    result.Add(new OverworldAsset.BuildingData { kindId = p.Id, position = new Vector3(p.Centre.x, low, p.Centre.y), yaw = p.Yaw, footprint = p.Footprint });
                }
            }
            return result;
        }

        /// <summary>Marks every cell whose middle lies under a building as blocked. Returns how many it changed.</summary>
        public static int BlockFootprints(GroundKind[] kinds, int columns, int rows, float minX, float minZ, float cellSize, IReadOnlyList<OverworldAsset.BuildingData> buildings)
        {
            var count = 0;
            foreach (var b in buildings)
            {
                var reach = b.footprint.magnitude / 2f;
                var c0 = Mathf.Max(0, Mathf.FloorToInt((b.position.x - reach - minX) / cellSize));
                var c1 = Mathf.Min(columns - 1, Mathf.FloorToInt((b.position.x + reach - minX) / cellSize));
                var r0 = Mathf.Max(0, Mathf.FloorToInt((b.position.z - reach - minZ) / cellSize));
                var r1 = Mathf.Min(rows - 1, Mathf.FloorToInt((b.position.z + reach - minZ) / cellSize));
                for (var r = r0; r <= r1; r++)
                for (var c = c0; c <= c1; c++)
                {
                    var x = minX + (c + 0.5f) * cellSize;
                    var z = minZ + (r + 0.5f) * cellSize;
                    if (!TownPlanner.Contains(new Vector2(b.position.x, b.position.z), b.yaw, b.footprint, x, z)) continue;
                    if (kinds[r * columns + c] != GroundKind.Blocked) count++;
                    kinds[r * columns + c] = GroundKind.Blocked;
                }
            }
            return count;
        }

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
