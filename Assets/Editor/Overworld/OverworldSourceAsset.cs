using System.Collections.Generic;
using HunterSystem.Game;
using UnityEngine;

namespace HunterSystem.Editor
{
    /// <summary>
    /// The recipe for an overworld's terrain (ticket 39): where the heights come from (stamps, noise, an
    /// optional painted map), how the area is cut into Unity Terrain tiles, and the rules the bake uses.
    /// Generating makes or refreshes the tiles from it, keeping any sculpting done on them since the last
    /// generation; baking turns the tiles into the simulation's ground and the overworld asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunter System/Overworld Source", fileName = "NewOverworldSource")]
    public sealed class OverworldSourceAsset : ScriptableObject
    {
        [Tooltip("Names the generated files: Assets/Art/Terrain/<name>/, Assets/Data/Overworld/<name>.asset and Assets/StreamingAssets/Overworld/<name>/")]
        public string outputName = "Overworld";

        [Header("Area and tiles")]
        [Tooltip("World X/Z of the area's south-west corner")] public Vector2 origin = new Vector2(-500f, -500f);
        [Min(10f), Tooltip("Metres per square tile")] public float tileSize = 250f;
        [Min(1)] public int tilesX = 4;
        [Min(1)] public int tilesZ = 4;
        [Tooltip("Samples along a tile's edge: 2^n + 1. 257 on a 250 m tile is about 1 m apart.")] public int heightmapResolution = 257;
        [Min(1f), Tooltip("The terrain's height range in metres (from 0)")] public float maxHeight = 120f;

        [Header("Heights")]
        public HeightSource heights = new HeightSource();

        [Header("Bake")]
        public GroundRules rules = new GroundRules();
        [Min(0.5f), Tooltip("Facet size close up, in metres")] public float meshCellSize = 4f;
        [Min(1f), Tooltip("Facet size far away, in metres")] public float farMeshCellSize = 16f;

        [Header("Big maps (ticket 45)")]
        [Tooltip("Bake straight from the recipe, without a Unity Terrain for every tile (the whole continent would be over a gigabyte). Only the tiles listed below get a Terrain to sculpt, and their sculpting is baked in.")]
        public bool bakeFromRecipe;
        [Tooltip("With Bake From Recipe: the tiles (x, z from the south-west) that Generate makes Terrains for, to sculpt by hand.")]
        public List<Vector2Int> sculptTiles = new List<Vector2Int>();

        public Vector2 AreaSize => new Vector2(tileSize * tilesX, tileSize * tilesZ);
        public int CellsPerTile => heightmapResolution - 1;
        public string TerrainFolder => $"Assets/Art/Terrain/{outputName}";
        public string OverworldPath => $"Assets/Data/Overworld/{outputName}.asset";
        public string CoarsePath => $"Assets/Data/Overworld/{outputName}.coarse.bytes";
        public string OldGroundPath => $"Assets/Data/Overworld/{outputName}.ground.bytes";

        /// <summary>The tiles' folder under StreamingAssets (ADR 0020), and the same as a project path.</summary>
        public string TileFolder => $"Overworld/{outputName}";
        public string TileFolderAsset => $"Assets/StreamingAssets/{TileFolder}";
    }
}
