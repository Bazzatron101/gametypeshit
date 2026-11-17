using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Game;
using UnityEditor;
using UnityEngine;

namespace HunterSystem.Editor
{
    /// <summary>
    /// Placeholder art for the overworld (ticket 49): low-poly, flat-shaded meshes made in code (trees, a bush,
    /// rocks, grass, houses, the guild hall, an inn, a shop, a well, a tower, a keep) and a handful of flat
    /// materials, in Assets/Art/Environment. Every file is labelled "AI" so it can be found and replaced (search
    /// l:AI in the Project window). Running it again remakes the meshes in place, keeping their references. It
    /// also makes Assets/Data/Overworld/Environment.asset the first time, with starting numbers; after that it only
    /// fills in art for kinds that have none, so tuning and swapped-in real art survive.
    /// </summary>
    public static class EnvironmentArtSetup
    {
        public const string Folder = "Assets/Art/Environment";
        public const string MeshFolder = Folder + "/Meshes";
        public const string MaterialFolder = Folder + "/Materials";
        public const string EnvironmentPath = "Assets/Data/Overworld/Environment.asset";
        public const string AiLabel = "AI";
        private const string ShaderSource = "Assets/Art/Materials/Greybox_Player.mat";

        [MenuItem("Hunter System/Overworld/Make Placeholder Environment Art")]
        public static void SetUpFromMenu()
        {
            var environment = SetUp();
            Debug.Log($"Environment: {environment.props.Length} nature kinds and {environment.buildings.Length} buildings in {EnvironmentPath}, " +
                      $"placeholder art in {Folder} (labelled {AiLabel}). Rebuild an overworld scene to lay out the towns.");
        }

        /// <summary>For batch mode: Unity -batchmode -executeMethod HunterSystem.Editor.EnvironmentArtSetup.SetUpBatch -quit.</summary>
        public static void SetUpBatch() => SetUp();

        private static readonly Dictionary<string, Color> Colours = new Dictionary<string, Color>
        {
            ["Bark"] = new Color(0.38f, 0.26f, 0.16f),
            ["Pine"] = new Color(0.16f, 0.36f, 0.22f),
            ["Leaves"] = new Color(0.33f, 0.55f, 0.22f),
            ["Bush"] = new Color(0.26f, 0.46f, 0.2f),
            ["Rock"] = new Color(0.5f, 0.49f, 0.47f),
            ["Grass"] = new Color(0.47f, 0.66f, 0.3f),
            ["Plaster"] = new Color(0.87f, 0.82f, 0.7f),
            ["Timber"] = new Color(0.42f, 0.28f, 0.17f),
            ["Thatch"] = new Color(0.72f, 0.6f, 0.34f),
            ["Tiles"] = new Color(0.62f, 0.25f, 0.18f),
            ["Slate"] = new Color(0.3f, 0.33f, 0.4f),
            ["Stone"] = new Color(0.62f, 0.6f, 0.56f),
            ["Door"] = new Color(0.3f, 0.19f, 0.11f),
            ["Banner"] = new Color(0.2f, 0.36f, 0.72f),
            // A look per region (ticket 84).
            ["Whitewash"] = new Color(0.95f, 0.94f, 0.9f),
            ["BlueTiles"] = new Color(0.25f, 0.4f, 0.62f),
            ["Sandstone"] = new Color(0.8f, 0.66f, 0.45f),
            ["GreyStone"] = new Color(0.52f, 0.54f, 0.58f),
            ["SnowSlate"] = new Color(0.85f, 0.88f, 0.92f),
            ["Reed"] = new Color(0.6f, 0.55f, 0.32f),
            ["DarkTimber"] = new Color(0.28f, 0.2f, 0.13f),
            ["Iron"] = new Color(0.25f, 0.25f, 0.27f),
            // Colour on the ground (developer playtest, 2026-11-17): flowers and mushrooms.
            ["Stem"] = new Color(0.3f, 0.52f, 0.22f),
            ["FlowerYellow"] = new Color(0.98f, 0.84f, 0.2f),
            ["FlowerPink"] = new Color(0.93f, 0.45f, 0.66f),
            ["FlowerBlue"] = new Color(0.45f, 0.55f, 0.95f),
            ["FlowerWhite"] = new Color(0.96f, 0.95f, 0.9f),
            ["MushroomStem"] = new Color(0.9f, 0.86f, 0.76f),
            ["CapRed"] = new Color(0.82f, 0.16f, 0.12f),
            ["CapBrown"] = new Color(0.55f, 0.36f, 0.2f),
        };

        public static EnvironmentAsset SetUp()
        {
            EnsureFolder("Assets/Art", "Environment");
            EnsureFolder(Folder, "Meshes");
            EnsureFolder(Folder, "Materials");
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Overworld");

            var materials = Colours.Keys.ToDictionary(k => k, MakeMaterial);
            Material[] M(params string[] names) => names.Select(n => materials[n]).ToArray();

            // Nature: id, mesh, materials, and the starting numbers (per hectare, cover, patch size, patch group, scale, slope, clearance, sink, draw distance, shadows).
            var props = new[]
            {
                // An old forest (developer playtest, 2026-11-17): tall, dense woods, with old pines standing over the younger ones.
                Prop("pine", Save("Pine", Pine()), M("Bark", "Pine"), 110f, 0.45f, 260f, 1, 1.1f, 1.9f, 30f, 4f, 0.3f, 900f, true),
                Prop("old-pine", Save("OldPine", OldPine()), M("Bark", "Pine"), 45f, 0.45f, 260f, 1, 0.9f, 1.35f, 28f, 5f, 0.5f, 1000f, true),
                Prop("leafy-tree", Save("LeafyTree", LeafyTree()), M("Bark", "Leaves"), 40f, 0.45f, 220f, 2, 1.1f, 1.8f, 22f, 4f, 0.3f, 900f, true),
                Prop("bush", Save("Bush", Bush()), M("Bush"), 60f, 0.5f, 160f, 1, 0.7f, 1.4f, 30f, 2f, 0.2f, 250f, true),
                Prop("rock", Save("Rock", Rock(3)), M("Rock"), 6f, 0.6f, 90f, 3, 0.6f, 1.6f, 45f, 2f, 0.3f, 400f, true),
                Prop("boulder", Save("Boulder", Rock(11)), M("Rock"), 1.2f, 0.5f, 140f, 4, 2f, 3.5f, 40f, 4f, 0.6f, 600f, true),
                Prop("grass", Save("Grass", GrassTuft()), M("Grass"), 900f, 0.75f, 60f, 5, 0.7f, 1.3f, 35f, 0.5f, 0.05f, 90f, false),
                // Flowers in meadow patches and mushrooms in the woods (developer playtest, 2026-11-17): colour on the ground.
                Prop("flowers-yellow", Save("FlowersYellow", Flowers(41)), M("Stem", "FlowerYellow"), 220f, 0.35f, 45f, 7, 0.8f, 1.3f, 30f, 1f, 0.02f, 140f, false),
                Prop("flowers-pink", Save("FlowersPink", Flowers(42)), M("Stem", "FlowerPink"), 180f, 0.3f, 40f, 8, 0.8f, 1.3f, 30f, 1f, 0.02f, 140f, false),
                Prop("flowers-blue", Save("FlowersBlue", Flowers(43)), M("Stem", "FlowerBlue"), 160f, 0.3f, 50f, 9, 0.8f, 1.3f, 30f, 1f, 0.02f, 140f, false),
                Prop("flowers-white", Save("FlowersWhite", Flowers(44)), M("Stem", "FlowerWhite"), 200f, 0.3f, 35f, 10, 0.8f, 1.2f, 30f, 1f, 0.02f, 140f, false),
                Prop("red-mushroom", Save("RedMushroom", Mushrooms(51)), M("MushroomStem", "CapRed"), 60f, 0.45f, 260f, 1, 0.7f, 1.4f, 30f, 1.5f, 0.02f, 110f, false),
                Prop("brown-mushroom", Save("BrownMushroom", Mushrooms(52)), M("MushroomStem", "CapBrown"), 90f, 0.45f, 260f, 1, 0.7f, 1.4f, 30f, 1.5f, 0.02f, 110f, false),
                // Large boulders under the mountains (developer playtest, 2026-11-17): only near steep ground.
                NearSteep(Prop("mountain-boulder", Save("MountainBoulder", Rock(17)), M("Rock"), 5f, 0.9f, 200f, 6, 3.5f, 7f, 40f, 4f, 1.2f, 1000f, true), 35f, 28f),
            };
            var buildings = new[]
            {
                Building("guild-hall", "hall", new Vector2(12f, 9f), Save("GuildHall", Hall()), M("Stone", "Slate", "Door", "Banner")),
                Building("inn", "inn", new Vector2(9f, 7f), Save("Inn", House(9f, 7f, 5.5f, 2.6f)), M("Plaster", "Tiles", "Door", "Timber")),
                Building("shop", "shop", new Vector2(6f, 6f), Save("Shop", Shop()), M("Plaster", "Tiles", "Door", "Banner")),
                Building("house", "house", new Vector2(6f, 5f), Save("House", House(6f, 5f, 3f, 2f)), M("Plaster", "Thatch", "Door", "Timber")),
                Building("cottage", "house", new Vector2(5f, 4.5f), Save("Cottage", House(5f, 4.5f, 2.6f, 1.8f)), M("Plaster", "Tiles", "Door", "Timber")),
                Building("long-house", "house", new Vector2(8f, 5f), Save("LongHouse", House(8f, 5f, 3f, 2.2f)), M("Timber", "Thatch", "Door", "Plaster")),
                Building("well", "well", new Vector2(2.4f, 2.4f), Save("Well", Well()), M("Stone", "Tiles", "Timber")),
                Building("tower", "tower", new Vector2(5.5f, 5.5f), Save("Tower", Tower()), M("Stone", "Slate")),
                Building("keep", "keep", new Vector2(11f, 11f), Save("Keep", Keep()), M("Stone", "Slate", "Door", "Banner")),
                // More kinds, with a purpose (ticket 84).
                Building("two-storey-house", "house", new Vector2(6f, 6f), Save("TwoStoreyHouse", House(6f, 6f, 5.5f, 2.4f)), M("Plaster", "Tiles", "Door", "Timber")),
                Building("small-cottage", "house", new Vector2(4.5f, 4f), Save("SmallCottage", House(4.5f, 4f, 2.4f, 1.7f)), M("Plaster", "Thatch", "Door", "Timber")),
                Building("smithy", "smithy", new Vector2(7f, 6f), Save("Smithy", Smithy()), M("Stone", "Slate", "Door", "Iron")),
                Building("chapel", "chapel", new Vector2(6f, 11f), Save("Chapel", Chapel()), M("Stone", "Slate", "Door", "Banner")),
                Building("stables", "stables", new Vector2(11f, 5f), Save("Stables", House(11f, 5f, 2.6f, 1.6f)), M("Timber", "Thatch", "Door", "Timber")),
                Building("barn", "barn", new Vector2(9f, 7f), Save("Barn", House(9f, 7f, 3.6f, 3.2f)), M("Timber", "Thatch", "Door", "Timber")),
                Building("warehouse", "warehouse", new Vector2(10f, 8f), Save("Warehouse", House(10f, 8f, 4.2f, 2.2f)), M("Timber", "Tiles", "Door", "Timber")),
                Building("workshop", "workshop", new Vector2(7f, 5f), Save("Workshop", Workshop()), M("Plaster", "Tiles", "Door", "Timber")),
                Building("market-hall", "market", new Vector2(9f, 7f), Save("MarketHall", MarketHall()), M("Stone", "Tiles", "Timber", "Banner")),
            };

            // A look per region (ticket 84): the same buildings in each region's own materials.
            EnvironmentAsset.MaterialSwap Swap(string from, string to) => new EnvironmentAsset.MaterialSwap { from = materials[from], to = materials[to] };
            var looks = new[]
            {
                new EnvironmentAsset.RegionLook { region = "greenmarch" }, // the kinds' own: plaster, thatch, red tiles, timber
                new EnvironmentAsset.RegionLook { region = "brokenstone", swaps = new[] { Swap("Plaster", "Stone"), Swap("Thatch", "Slate"), Swap("Tiles", "Slate"), Swap("Timber", "Stone") } },
                new EnvironmentAsset.RegionLook { region = "saltreach", swaps = new[] { Swap("Plaster", "Whitewash"), Swap("Tiles", "BlueTiles"), Swap("Thatch", "BlueTiles") } },
                new EnvironmentAsset.RegionLook { region = "fenward", swaps = new[] { Swap("Plaster", "DarkTimber"), Swap("Tiles", "Reed"), Swap("Thatch", "Reed"), Swap("Timber", "DarkTimber") } },
                new EnvironmentAsset.RegionLook { region = "ashen", swaps = new[] { Swap("Plaster", "Sandstone"), Swap("Thatch", "Tiles"), Swap("Stone", "Sandstone") } },
                new EnvironmentAsset.RegionLook { region = "riftlands", swaps = new[] { Swap("Plaster", "GreyStone"), Swap("Thatch", "SnowSlate"), Swap("Tiles", "SnowSlate"), Swap("Slate", "SnowSlate"), Swap("Stone", "GreyStone") } },
            };

            var environment = AssetDatabase.LoadAssetAtPath<EnvironmentAsset>(EnvironmentPath);
            if (environment == null)
            {
                environment = ScriptableObject.CreateInstance<EnvironmentAsset>();
                environment.props = props;
                environment.buildings = buildings;
                environment.regionLooks = looks;
                AssetDatabase.CreateAsset(environment, EnvironmentPath);
            }
            else
            {
                // Keep the numbers and any real art; fill in what is missing, and add kinds that are new.
                environment.props = Merge(environment.props, props, k => k.id, (have, made) => { if (have.mesh == null) { have.mesh = made.mesh; have.materials = made.materials; } });
                environment.buildings = Merge(environment.buildings, buildings, k => k.id, (have, made) => { if (have.mesh == null) { have.mesh = made.mesh; have.materials = made.materials; } });
                environment.regionLooks = Merge(environment.regionLooks, looks, l => l.region, (have, made) => { if (have.swaps == null || have.swaps.Length == 0) have.swaps = made.swaps; });
                EditorUtility.SetDirty(environment);
            }
            AssetDatabase.SaveAssets();
            return environment;
        }

        private static T[] Merge<T>(T[] have, T[] made, Func<T, string> id, Action<T, T> fill) where T : class
        {
            var list = (have ?? new T[0]).Where(h => h != null).ToList();
            foreach (var m in made)
            {
                var existing = list.FirstOrDefault(h => id(h) == id(m));
                if (existing == null) list.Add(m);
                else fill(existing, m);
            }
            return list.ToArray();
        }

        private static EnvironmentAsset.PropKind Prop(string id, Mesh mesh, Material[] materials, float perHectare, float cover, float patch, int group,
            float minScale, float maxScale, float slope, float clearance, float sink, float draw, bool shadows) => new EnvironmentAsset.PropKind
        {
            id = id, mesh = mesh, materials = materials, perHectare = perHectare, cover = cover, patchSize = patch, patchGroup = group,
            minScale = minScale, maxScale = maxScale, maxSlope = slope, clearance = clearance, sink = sink, drawDistance = draw, castShadows = shadows,
        };

        private static EnvironmentAsset.PropKind NearSteep(EnvironmentAsset.PropKind kind, float within, float degrees)
        {
            kind.nearSteepWithin = within;
            kind.steepDegrees = degrees;
            return kind;
        }

        private static EnvironmentAsset.BuildingKind Building(string id, string role, Vector2 footprint, Mesh mesh, Material[] materials) =>
            new EnvironmentAsset.BuildingKind { id = id, role = role, footprint = footprint, mesh = mesh, materials = materials };

        // --- Assets --------------------------------------------------------------------------------------

        private static Material MakeMaterial(string name)
        {
            var path = $"{MaterialFolder}/Env_{name}_AI.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var template = AssetDatabase.LoadAssetAtPath<Material>(ShaderSource);
                var shader = template != null ? template.shader : Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            var colour = Colours[name];
            material.color = colour;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
            material.enableInstancing = true; // nature is drawn instanced
            EditorUtility.SetDirty(material);
            AssetDatabase.SetLabels(material, new[] { AiLabel });
            return material;
        }

        /// <summary>Saves the mesh at its path, or copies it into the mesh already there so references to it hold.</summary>
        private static Mesh Save(string name, Mesh made)
        {
            var path = $"{MeshFolder}/Env_{name}_AI.asset";
            made.name = $"Env_{name}_AI";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Mesh mesh;
            if (existing == null)
            {
                AssetDatabase.CreateAsset(made, path);
                mesh = made;
            }
            else
            {
                existing.Clear();
                existing.vertices = made.vertices;
                existing.normals = made.normals;
                existing.subMeshCount = made.subMeshCount;
                for (var i = 0; i < made.subMeshCount; i++) existing.SetTriangles(made.GetTriangles(i), i);
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(made);
                mesh = existing;
            }
            AssetDatabase.SetLabels(mesh, new[] { AiLabel });
            return mesh;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        // --- Shapes (door to +Z, standing on the origin) ---------------------------------------------------

        private static Mesh Pine()
        {
            var kit = new MeshKit(2);
            kit.Prism(0, Vector3.zero, 0.25f, 0.18f, 1.6f, 6);
            kit.Cone(1, new Vector3(0f, 1.2f, 0f), 1.7f, 2.4f, 7);
            kit.Cone(1, new Vector3(0f, 2.6f, 0f), 1.3f, 2.1f, 7, 0.4f);
            kit.Cone(1, new Vector3(0f, 3.9f, 0f), 0.9f, 2.0f, 7, 0.8f);
            return kit.Build();
        }

        /// <summary>An old pine: a tall bare trunk and a narrow crown high up, about 20 m in all.</summary>
        private static Mesh OldPine()
        {
            var kit = new MeshKit(2);
            kit.Prism(0, Vector3.zero, 0.55f, 0.3f, 10f, 7);
            kit.Cone(1, new Vector3(0f, 7.5f, 0f), 3.2f, 5.0f, 8);
            kit.Cone(1, new Vector3(0f, 10.5f, 0f), 2.6f, 4.4f, 8, 0.4f);
            kit.Cone(1, new Vector3(0f, 13.5f, 0f), 2.0f, 4.0f, 8, 0.8f);
            kit.Cone(1, new Vector3(0f, 16.5f, 0f), 1.3f, 3.6f, 8, 1.2f);
            return kit.Build();
        }

        /// <summary>A small cluster of flowers: stems (submesh 0) and round heads (submesh 1).</summary>
        private static Mesh Flowers(int seed)
        {
            var kit = new MeshKit(2);
            var random = new System.Random(seed);
            for (var i = 0; i < 6; i++)
            {
                var a = i * 1.05f + (float)random.NextDouble();
                var r = 0.08f + 0.2f * (float)random.NextDouble();
                var at = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                var height = 0.28f + 0.2f * (float)random.NextDouble();
                kit.Prism(0, at, 0.015f, 0.012f, height, 4);
                kit.Blob(1, at + new Vector3(0f, height + 0.03f, 0f), new Vector3(0.09f, 0.06f, 0.09f), seed * 10 + i, 0.1f);
            }
            return kit.Build();
        }

        /// <summary>A few mushrooms of different sizes: pale stems (submesh 0) and domed caps (submesh 1).</summary>
        private static Mesh Mushrooms(int seed)
        {
            var kit = new MeshKit(2);
            var random = new System.Random(seed);
            for (var i = 0; i < 3; i++)
            {
                var a = i * 2.1f + (float)random.NextDouble();
                var r = i == 0 ? 0f : 0.15f + 0.1f * (float)random.NextDouble();
                var at = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                var size = i == 0 ? 1f : 0.55f + 0.25f * (float)random.NextDouble();
                kit.Prism(0, at, 0.045f * size, 0.035f * size, 0.18f * size, 6);
                kit.Blob(1, at + new Vector3(0f, 0.2f * size, 0f), new Vector3(0.26f, 0.12f, 0.26f) * size, seed * 10 + i, 0.08f);
            }
            return kit.Build();
        }

        private static Mesh LeafyTree()
        {
            var kit = new MeshKit(2);
            kit.Prism(0, Vector3.zero, 0.32f, 0.22f, 2.4f, 6);
            kit.Blob(1, new Vector3(0f, 3.6f, 0f), new Vector3(2.1f, 1.7f, 2.1f), 21);
            kit.Blob(1, new Vector3(0.9f, 3.0f, 0.5f), new Vector3(1.2f, 1.0f, 1.2f), 22);
            kit.Blob(1, new Vector3(-0.8f, 3.2f, -0.6f), new Vector3(1.1f, 0.9f, 1.1f), 23);
            return kit.Build();
        }

        private static Mesh Bush()
        {
            var kit = new MeshKit(1);
            kit.Blob(0, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.6f, 0.9f), 31);
            kit.Blob(0, new Vector3(0.5f, 0.35f, 0.3f), new Vector3(0.55f, 0.45f, 0.55f), 32);
            return kit.Build();
        }

        private static Mesh Rock(int seed)
        {
            var kit = new MeshKit(1);
            kit.Blob(0, new Vector3(0f, 0.35f, 0f), new Vector3(1f, 0.65f, 0.85f), seed, 0.3f);
            return kit.Build();
        }

        private static Mesh GrassTuft()
        {
            var kit = new MeshKit(1);
            for (var i = 0; i < 5; i++)
            {
                var a = i * 2.4f;
                var at = new Vector3(Mathf.Cos(a) * 0.12f, 0f, Mathf.Sin(a) * 0.12f);
                var lean = new Vector3(Mathf.Cos(a) * 0.12f, 0f, Mathf.Sin(a) * 0.12f);
                kit.Blade(0, at, 0.05f, 0.45f + 0.08f * (i % 3), lean, a);
            }
            return kit.Build();
        }

        /// <summary>Walls, a gable roof with its ridge along X, a door on +Z and timber corner posts.</summary>
        private static Mesh House(float width, float depth, float wallHeight, float roofHeight)
        {
            var kit = new MeshKit(4);
            kit.Box(0, new Vector3(-width / 2f, 0f, -depth / 2f), new Vector3(width / 2f, wallHeight, depth / 2f));
            kit.Gable(1, width + 0.6f, depth + 0.8f, wallHeight, roofHeight);
            kit.Box(2, new Vector3(-0.6f, 0f, depth / 2f - 0.05f), new Vector3(0.6f, 2.1f, depth / 2f + 0.08f));
            foreach (var x in new[] { -width / 2f, width / 2f })
            foreach (var z in new[] { -depth / 2f, depth / 2f })
                kit.Box(3, new Vector3(x - 0.15f, 0f, z - 0.15f), new Vector3(x + 0.15f, wallHeight, z + 0.15f));
            return kit.Build();
        }

        /// <summary>A smithy: a stone house with a big chimney and an anvil out front.</summary>
        private static Mesh Smithy()
        {
            var kit = new MeshKit(4);
            kit.Box(0, new Vector3(-3.5f, 0f, -3f), new Vector3(3.5f, 3f, 3f));
            kit.Gable(1, 7.6f, 6.8f, 3f, 1.8f);
            kit.Box(2, new Vector3(-0.7f, 0f, 2.95f), new Vector3(0.7f, 2.2f, 3.08f));
            kit.Box(0, new Vector3(2f, 0f, -2.5f), new Vector3(3.3f, 6.5f, -1.2f)); // the chimney
            kit.Box(3, new Vector3(-2.2f, 0f, 3.6f), new Vector3(-1.4f, 0.7f, 4.1f)); // the anvil
            kit.Box(3, new Vector3(-2.5f, 0.7f, 3.5f), new Vector3(-1.1f, 0.9f, 4.2f));
            return kit.Build();
        }

        /// <summary>A chapel: a long, tall nave with a bell tower over the door.</summary>
        private static Mesh Chapel()
        {
            var kit = new MeshKit(4);
            kit.Box(0, new Vector3(-3f, 0f, -5.5f), new Vector3(3f, 5f, 5.5f));
            kit.Gable(1, 6.6f, 11.4f, 5f, 3.4f);
            kit.Box(0, new Vector3(-1.5f, 0f, 3.5f), new Vector3(1.5f, 10f, 5.5f)); // the tower at the front
            kit.Cone(1, new Vector3(0f, 10f, 4.5f), 2f, 4f, 4, Mathf.PI / 4f);
            kit.Box(2, new Vector3(-0.8f, 0f, 5.45f), new Vector3(0.8f, 2.8f, 5.6f));
            kit.Box(3, new Vector3(-0.5f, 6.5f, 5.5f), new Vector3(0.5f, 8f, 5.6f)); // the bell window
            return kit.Build();
        }

        /// <summary>A workshop: a house with a lean-to shelter on one side.</summary>
        private static Mesh Workshop()
        {
            var kit = new MeshKit(4);
            kit.Box(0, new Vector3(-3.5f, 0f, -2.5f), new Vector3(1.5f, 3f, 2.5f));
            kit.Gable(1, 5.6f, 5.8f, 3f, 1.8f);
            kit.Box(2, new Vector3(-1.6f, 0f, 2.45f), new Vector3(-0.4f, 2.1f, 2.58f));
            kit.Slab(1, new Vector3(3.5f, 2f, -2.5f), new Vector3(1.5f, 2.8f, 2.5f), 0.1f); // the lean-to roof
            foreach (var z in new[] { -2.3f, 2.3f }) kit.Box(3, new Vector3(3.3f, 0f, z - 0.12f), new Vector3(3.55f, 2.1f, z + 0.12f));
            return kit.Build();
        }

        /// <summary>A market hall: a roof on posts, open underneath, over a stone floor.</summary>
        private static Mesh MarketHall()
        {
            var kit = new MeshKit(4);
            kit.Box(0, new Vector3(-4.5f, 0f, -3.5f), new Vector3(4.5f, 0.3f, 3.5f));
            foreach (var x in new[] { -4.2f, 0f, 4.2f })
            foreach (var z in new[] { -3.2f, 3.2f })
                kit.Box(2, new Vector3(x - 0.2f, 0.3f, z - 0.2f), new Vector3(x + 0.2f, 3.6f, z + 0.2f));
            kit.Gable(1, 9.8f, 7.8f, 3.6f, 2.2f);
            kit.Box(3, new Vector3(-0.6f, 2.6f, 3.4f), new Vector3(0.6f, 3.6f, 3.5f)); // a banner over the front
            return kit.Build();
        }

        private static Mesh Shop()
        {
            var kit = new MeshKit(4);
            kit.Box(0, new Vector3(-3f, 0f, -3f), new Vector3(3f, 3.2f, 3f));
            kit.Gable(1, 6.6f, 6.8f, 3.2f, 2f);
            kit.Box(2, new Vector3(-0.6f, 0f, 2.95f), new Vector3(0.6f, 2.1f, 3.08f));
            kit.Slab(3, new Vector3(-2.8f, 2.4f, 3f), new Vector3(2.8f, 2.0f, 4.4f), 0.08f); // the awning over the counter
            return kit.Build();
        }

        private static Mesh Hall()
        {
            var kit = new MeshKit(4);
            kit.Box(0, new Vector3(-6f, 0f, -4.5f), new Vector3(6f, 5f, 4.5f));
            kit.Gable(1, 12.8f, 10f, 5f, 3.2f);
            kit.Box(0, new Vector3(-1.6f, 0f, 4.4f), new Vector3(1.6f, 0.5f, 5.6f)); // the steps
            kit.Box(2, new Vector3(-1.1f, 0.5f, 4.45f), new Vector3(1.1f, 3.4f, 4.6f));
            kit.Box(3, new Vector3(-4.5f, 2f, 4.5f), new Vector3(-3.1f, 4.4f, 4.6f)); // banners either side of the door
            kit.Box(3, new Vector3(3.1f, 2f, 4.5f), new Vector3(4.5f, 4.4f, 4.6f));
            kit.Prism(0, new Vector3(4.5f, 0f, -2.5f), 1.4f, 1.4f, 8.5f, 6); // a bell tower at the back
            kit.Cone(1, new Vector3(4.5f, 8.5f, -2.5f), 1.8f, 2.6f, 6);
            return kit.Build();
        }

        private static Mesh Well()
        {
            var kit = new MeshKit(3);
            kit.Prism(0, Vector3.zero, 1.05f, 1.05f, 0.9f, 8);
            kit.Box(2, new Vector3(-0.95f, 0.9f, -0.1f), new Vector3(-0.8f, 2.3f, 0.1f));
            kit.Box(2, new Vector3(0.8f, 0.9f, -0.1f), new Vector3(0.95f, 2.3f, 0.1f));
            kit.Gable(1, 2.4f, 1.6f, 2.3f, 0.7f);
            return kit.Build();
        }

        private static Mesh Tower()
        {
            var kit = new MeshKit(2);
            kit.Prism(0, Vector3.zero, 2.75f, 2.5f, 10f, 8);
            kit.Cone(1, new Vector3(0f, 10f, 0f), 3.2f, 4f, 8);
            return kit.Build();
        }

        private static Mesh Keep()
        {
            var kit = new MeshKit(4);
            kit.Box(0, new Vector3(-5.5f, 0f, -5.5f), new Vector3(5.5f, 9f, 5.5f));
            for (var i = 0; i < 6; i++) // battlements round the top
            {
                var t = -5.5f + 0.4f + i * 2f;
                kit.Box(0, new Vector3(t, 9f, 5f), new Vector3(t + 1f, 10.2f, 5.5f));
                kit.Box(0, new Vector3(t, 9f, -5.5f), new Vector3(t + 1f, 10.2f, -5f));
                kit.Box(0, new Vector3(5f, 9f, t), new Vector3(5.5f, 10.2f, t + 1f));
                kit.Box(0, new Vector3(-5.5f, 9f, t), new Vector3(-5f, 10.2f, t + 1f));
            }
            kit.Prism(0, new Vector3(0f, 9f, 0f), 2f, 2f, 4f, 8);
            kit.Cone(1, new Vector3(0f, 13f, 0f), 2.5f, 3f, 8);
            kit.Box(2, new Vector3(-1.3f, 0f, 5.45f), new Vector3(1.3f, 3.2f, 5.6f));
            kit.Box(3, new Vector3(-0.6f, 5f, 5.5f), new Vector3(0.6f, 8f, 5.6f));
            return kit.Build();
        }

        /// <summary>
        /// Builds flat-shaded meshes: every triangle has its own vertices, so each face is lit as one flat
        /// colour (ADR 0002). Triangles are turned to face away from their part's middle, so winding never matters.
        /// </summary>
        private sealed class MeshKit
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<int>[] _triangles;

            public MeshKit(int submeshes) => _triangles = Enumerable.Range(0, submeshes).Select(_ => new List<int>()).ToArray();

            public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
            {
                var normal = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(normal, (a + b + c) / 3f - inside) < 0f) (b, c) = (c, b);
                var i = _vertices.Count;
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);
                _triangles[sub].Add(i);
                _triangles[sub].Add(i + 1);
                _triangles[sub].Add(i + 2);
            }

            public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
            {
                Tri(sub, a, b, c, inside);
                Tri(sub, a, c, d, inside);
            }

            public void Box(int sub, Vector3 min, Vector3 max)
            {
                var c = (min + max) / 2f;
                Vector3 P(int x, int y, int z) => new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                Quad(sub, P(0, 0, 0), P(1, 0, 0), P(1, 1, 0), P(0, 1, 0), c);
                Quad(sub, P(0, 0, 1), P(1, 0, 1), P(1, 1, 1), P(0, 1, 1), c);
                Quad(sub, P(0, 0, 0), P(0, 0, 1), P(0, 1, 1), P(0, 1, 0), c);
                Quad(sub, P(1, 0, 0), P(1, 0, 1), P(1, 1, 1), P(1, 1, 0), c);
                Quad(sub, P(0, 1, 0), P(1, 1, 0), P(1, 1, 1), P(0, 1, 1), c);
                Quad(sub, P(0, 0, 0), P(1, 0, 0), P(1, 0, 1), P(0, 0, 1), c);
            }

            /// <summary>A sloped board: from its high front edge to its low back edge, <paramref name="thickness"/> thick.</summary>
            public void Slab(int sub, Vector3 back, Vector3 front, float thickness)
            {
                var minX = Mathf.Min(back.x, front.x);
                var maxX = Mathf.Max(back.x, front.x);
                Vector3 B(float x, float dy) => new Vector3(x, back.y + dy, back.z);
                Vector3 F(float x, float dy) => new Vector3(x, front.y + dy, front.z);
                var c = (back + front) / 2f + Vector3.up * thickness / 2f;
                Quad(sub, B(minX, thickness), B(maxX, thickness), F(maxX, thickness), F(minX, thickness), c);
                Quad(sub, B(minX, 0f), B(maxX, 0f), F(maxX, 0f), F(minX, 0f), c);
                Quad(sub, F(minX, 0f), F(maxX, 0f), F(maxX, thickness), F(minX, thickness), c);
                Quad(sub, B(minX, 0f), F(minX, 0f), F(minX, thickness), B(minX, thickness), c);
                Quad(sub, B(maxX, 0f), F(maxX, 0f), F(maxX, thickness), B(maxX, thickness), c);
            }

            /// <summary>A gable roof on walls <paramref name="baseY"/> high: the ridge runs along X.</summary>
            public void Gable(int sub, float width, float depth, float baseY, float height)
            {
                var hx = width / 2f;
                var hz = depth / 2f;
                var inside = new Vector3(0f, baseY + height / 3f, 0f);
                var ridgeA = new Vector3(-hx, baseY + height, 0f);
                var ridgeB = new Vector3(hx, baseY + height, 0f);
                Quad(sub, new Vector3(-hx, baseY, hz), new Vector3(hx, baseY, hz), ridgeB, ridgeA, inside);
                Quad(sub, new Vector3(-hx, baseY, -hz), new Vector3(hx, baseY, -hz), ridgeB, ridgeA, inside);
                Tri(sub, new Vector3(-hx, baseY, -hz), new Vector3(-hx, baseY, hz), ridgeA, inside);
                Tri(sub, new Vector3(hx, baseY, -hz), new Vector3(hx, baseY, hz), ridgeB, inside);
                Quad(sub, new Vector3(-hx, baseY, -hz), new Vector3(hx, baseY, -hz), new Vector3(hx, baseY, hz), new Vector3(-hx, baseY, hz), inside + Vector3.up * height);
            }

            /// <summary>An upright prism (a trunk, a tower) of <paramref name="sides"/> sides, narrowing to the top.</summary>
            public void Prism(int sub, Vector3 bottom, float radiusBottom, float radiusTop, float height, int sides)
            {
                var inside = bottom + Vector3.up * height / 2f;
                for (var i = 0; i < sides; i++)
                {
                    var a0 = i * 2f * Mathf.PI / sides;
                    var a1 = (i + 1) * 2f * Mathf.PI / sides;
                    var b0 = bottom + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radiusBottom;
                    var b1 = bottom + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radiusBottom;
                    var t0 = bottom + new Vector3(Mathf.Cos(a0) * radiusTop, height, Mathf.Sin(a0) * radiusTop);
                    var t1 = bottom + new Vector3(Mathf.Cos(a1) * radiusTop, height, Mathf.Sin(a1) * radiusTop);
                    Quad(sub, b0, b1, t1, t0, inside);
                    Tri(sub, t0, t1, bottom + Vector3.up * height, inside);
                    Tri(sub, b0, b1, bottom, inside);
                }
            }

            /// <summary>A cone (a pine's tier, a roof) of <paramref name="sides"/> sides, turned by <paramref name="turn"/> radians.</summary>
            public void Cone(int sub, Vector3 bottom, float radius, float height, int sides, float turn = 0f)
            {
                var tip = bottom + Vector3.up * height;
                var inside = bottom + Vector3.up * height / 4f;
                for (var i = 0; i < sides; i++)
                {
                    var a0 = turn + i * 2f * Mathf.PI / sides;
                    var a1 = turn + (i + 1) * 2f * Mathf.PI / sides;
                    var b0 = bottom + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                    var b1 = bottom + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                    Tri(sub, b0, b1, tip, inside);
                    Tri(sub, b0, b1, bottom, inside + Vector3.up);
                }
            }

            /// <summary>A lumpy ball (a tree's crown, a rock): an icosahedron, stretched and jittered.</summary>
            public void Blob(int sub, Vector3 centre, Vector3 size, int seed, float jitter = 0.18f)
            {
                var t = (1f + Mathf.Sqrt(5f)) / 2f;
                var corners = new[]
                {
                    new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                    new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                    new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
                };
                var random = new System.Random(seed);
                var points = corners.Select(c =>
                {
                    var n = c.normalized * (1f + ((float)random.NextDouble() * 2f - 1f) * jitter);
                    return centre + Vector3.Scale(n, size);
                }).ToArray();
                int[] faces =
                {
                    0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                    3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
                };
                for (var i = 0; i < faces.Length; i += 3) Tri(sub, points[faces[i]], points[faces[i + 1]], points[faces[i + 2]], centre);
            }

            /// <summary>A grass blade: a thin three-sided spike, leaning, so it shows from every side.</summary>
            public void Blade(int sub, Vector3 root, float width, float height, Vector3 lean, float turn)
            {
                var tip = root + Vector3.up * height + lean;
                var b = new Vector3[3];
                for (var i = 0; i < 3; i++)
                {
                    var a = turn + i * 2f * Mathf.PI / 3f;
                    b[i] = root + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * width;
                }
                var inside = root + Vector3.up * height * 0.3f + lean * 0.3f;
                Tri(sub, b[0], b[1], tip, inside);
                Tri(sub, b[1], b[2], tip, inside);
                Tri(sub, b[2], b[0], tip, inside);
            }

            public Mesh Build()
            {
                var mesh = new Mesh();
                if (_vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(_vertices);
                mesh.subMeshCount = _triangles.Length;
                for (var i = 0; i < _triangles.Length; i++) mesh.SetTriangles(_triangles[i], i);
                mesh.RecalculateNormals(); // no shared vertices: every face flat
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
