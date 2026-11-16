using System.Collections.Generic;
using System.IO;
using System.Linq;
using HunterSystem.Game;
using UnityEditor;
using UnityEngine;

namespace HunterSystem.Editor
{
    /// <summary>
    /// The whole continent's terrain recipe (tickets 39 and 45), from the elevation and water maps in
    /// docs/maps (made by scripts/make-overworld-heights.py). Copies the maps into the project with the
    /// import settings the pipeline needs (readable, linear, uncompressed, 16-bit heights), labels them
    /// "AI" (placeholders to replace), and makes or refreshes Assets/Data/Overworld/ContinentSource.asset.
    /// Generating and baking the full map is ticket 45.
    /// </summary>
    public static class ContinentSetup
    {
        public const string MapsFolder = "Assets/Art/Maps";
        public const string HeightsPath = MapsFolder + "/overworld-heights-ai.png";
        public const string WaterPath = MapsFolder + "/overworld-water-ai.png";
        public const string SourcePath = "Assets/Data/Overworld/ContinentSource.asset";
        public const string AiLabel = "AI";

        /// <summary>Metres for the heights map's white (scripts/make-overworld-heights.py SCALE).</summary>
        public const float HeightScale = 250f;

        /// <summary>The drawn map's size: 10.5 km east to west; 909 × 673 drawing, so about 7.77 km north to south.</summary>
        public const float TileSize = 250f;
        public const int TilesX = 42;   // 10.5 km
        public const int TilesZ = 31;   // 7.75 km

        [MenuItem("Hunter System/Overworld/Set Up Continent Recipe")]
        public static void SetUpFromMenu()
        {
            var source = SetUp();
            Selection.activeObject = source;
            Debug.Log($"Continent recipe ready: {SourcePath} ({TilesX} × {TilesZ} tiles of {TileSize} m), heights and water from {MapsFolder} (labelled {AiLabel}).");
        }

        /// <summary>For batch mode: Unity -batchmode -executeMethod HunterSystem.Editor.ContinentSetup.SetUpBatch -quit.</summary>
        public static void SetUpBatch() => SetUp();

        public static OverworldSourceAsset SetUp()
        {
            Directory.CreateDirectory(MapsFolder);
            var docs = Path.Combine(Application.dataPath, "..", "docs", "maps");
            ImportMap(Path.Combine(docs, "overworld-heights-ai.png"), HeightsPath, sixteenBit: true);
            ImportMap(Path.Combine(docs, "overworld-water-ai.png"), WaterPath, sixteenBit: false);

            var source = AssetDatabase.LoadAssetAtPath<OverworldSourceAsset>(SourcePath);
            var created = source == null;
            if (created) source = ScriptableObject.CreateInstance<OverworldSourceAsset>();

            source.outputName = "Continent";
            source.tileSize = TileSize;
            source.tilesX = TilesX;
            source.tilesZ = TilesZ;
            source.origin = new Vector2(-TilesX * TileSize / 2f, -TilesZ * TileSize / 2f); // the map's centre at the origin (ADR 0015)
            source.heightmapResolution = 257;
            source.maxHeight = 260f;
            source.heights.baseHeight = 0f;
            source.heights.noiseAmplitude = 1f; // the painted map already has its hills; a little extra texture only
            source.heights.noiseScale = 40f;
            source.heights.paintedHeights = AssetDatabase.LoadAssetAtPath<Texture2D>(HeightsPath);
            source.heights.paintedHeightScale = HeightScale;
            source.heights.paintedWater = AssetDatabase.LoadAssetAtPath<Texture2D>(WaterPath);
            source.rules.seaLevel = 4f;
            source.rules.maxWalkableSlopeDegrees = 40f;
            source.bakeFromRecipe = true; // 1,302 tiles: no Unity Terrain for all of them (ticket 45)
            source.heights.stamps = Stamps(source);
            source.heights.Refresh();

            if (created) AssetDatabase.CreateAsset(source, SourcePath);
            else EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssets();
            return source;
        }

        /// <summary>
        /// The continent's stamps from <see cref="ContinentLayout"/>: a level square at each town and castle, each
        /// road levelled across its width, and a bridge wherever a road crosses the sea, a river or a lake.
        /// </summary>
        public static List<HeightStamp> Stamps(OverworldSourceAsset source)
        {
            var min = source.origin;
            var size = source.AreaSize;
            Vector2 W(Vector2 pixel) => ContinentLayout.World(pixel, min, size);

            var stamps = new List<HeightStamp>
            {
                Stamp("Square: " + ContinentLayout.GuildTown.Name, HeightStampKind.Flatten, 45f, 0f, 25f, W(ContinentLayout.GuildTown.Pixel)),
            };
            stamps.AddRange(ContinentLayout.Settlements.Select(p => Stamp("Square: " + p.Name, HeightStampKind.Flatten, p.Castle ? 35f : 25f, 0f, 20f, W(p.Pixel))));
            source.heights.stamps = stamps;
            source.heights.Refresh();

            // Bridges: where a road, before it is laid, would be under the sea or in a river or lake.
            var bridges = new List<HeightStamp>();
            foreach (var road in ContinentLayout.Roads)
            {
                var line = road.Pixels.Select(W).ToList();
                var steps = Walk(line, 4f);
                int? wetFrom = null;
                for (var i = 0; i <= steps.Count; i++)
                {
                    var wet = i < steps.Count && (source.heights.Sample(steps[i].x, steps[i].y, min, size) < source.rules.seaLevel + 1f ||
                                                  source.heights.IsPaintedWater(steps[i].x, steps[i].y, min, size));
                    if (wet && wetFrom == null) wetFrom = i;
                    if (wet || wetFrom == null) continue;
                    var from = Mathf.Max(0, wetFrom.Value - 4);   // 16 m of ramp onto dry land each side
                    var to = Mathf.Min(steps.Count - 1, i + 3);
                    bridges.Add(Stamp($"Bridge: {road.Name} {bridges.Count + 1}", HeightStampKind.Bridge, 5f, source.rules.seaLevel + 3f, 10f,
                        steps.GetRange(from, to - from + 1).ToArray()));
                    wetFrom = null;
                }
            }
            stamps.AddRange(bridges);
            stamps.AddRange(ContinentLayout.Roads.Select(r => Stamp("Road: " + r.Name, HeightStampKind.Flatten, 4f, 0f, 6f, r.Pixels.Select(W).ToArray())));
            return stamps;
        }

        /// <summary>Points every <paramref name="step"/> metres along a polyline.</summary>
        public static List<Vector2> Walk(IReadOnlyList<Vector2> line, float step)
        {
            var points = new List<Vector2> { line[0] };
            for (var i = 1; i < line.Count; i++)
            {
                var a = line[i - 1];
                var b = line[i];
                var count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
                for (var k = 1; k <= count; k++) points.Add(Vector2.Lerp(a, b, k / (float)count));
            }
            return points;
        }

        private static HeightStamp Stamp(string label, HeightStampKind kind, float radius, float height, float blend, params Vector2[] points) =>
            new HeightStamp { label = label, kind = kind, radius = radius, height = height, blend = blend, points = new List<Vector2>(points) };

        private static void ImportMap(string from, string to, bool sixteenBit)
        {
            if (!File.Exists(from)) throw new FileNotFoundException($"Run scripts/make-overworld-heights.py first: {from} is missing.", from);
            File.Copy(from, to, overwrite: true);
            AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(to);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;           // heights are numbers, not colours
            importer.isReadable = true;             // the pipeline reads pixels
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = importer.GetDefaultPlatformTextureSettings();
            settings.format = sixteenBit ? TextureImporterFormat.R16 : TextureImporterFormat.R8;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();
            AssetDatabase.SetLabels(AssetDatabase.LoadMainAssetAtPath(to), new[] { AiLabel }); // a placeholder to replace
        }
    }
}
