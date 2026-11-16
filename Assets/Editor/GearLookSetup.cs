using System;
using System.Collections.Generic;
using System.IO;
using HunterSystem.Game;
using UnityEditor;
using UnityEngine;

namespace HunterSystem.Editor
{
    /// <summary>
    /// Placeholder looks for every piece of gear (ticket 34): small prefabs built from primitives, one
    /// per piece, in Assets/Art/Gear, with a handful of flat materials. Each is labelled "AI" (a
    /// placeholder to replace) and set as its gear asset's look. Running it again rebuilds these
    /// placeholders in place, but a look put in by hand (any other prefab) is never overwritten. A look is authored in its slot's anchor
    /// space (<see cref="GearLooks.Anchor"/>): the origin is the right hand for a weapon, the top of the
    /// head, the chest, the middle between the hands or the feet, and the chest for a trinket.
    /// </summary>
    public static class GearLookSetup
    {
        public const string Folder = "Assets/Art/Gear";
        public const string MaterialsFolder = Folder + "/Materials";
        public const string GearFolder = "Assets/Data/Gear";
        public const string AiLabel = "AI";
        private const string ShaderSource = "Assets/Art/Materials/Greybox_Player.mat";

        [MenuItem("Hunter System/Gear/Make Placeholder Gear Looks")]
        public static void SetUpFromMenu()
        {
            var made = SetUp();
            Debug.Log($"Gear looks: {made} placeholder look(s) made in {Folder} (labelled {AiLabel}).");
        }

        /// <summary>For batch mode: Unity -batchmode -executeMethod HunterSystem.Editor.GearLookSetup.SetUpBatch -quit.</summary>
        public static void SetUpBatch() => SetUp();

        private sealed class Part
        {
            public PrimitiveType Shape;
            public Vector3 Position;
            public Vector3 Scale;
            public Vector3 Rotation;
            public string Material;
        }

        private static Part P(PrimitiveType shape, string material, Vector3 position, Vector3 scale, Vector3 rotation = default) =>
            new Part { Shape = shape, Material = material, Position = position, Scale = scale, Rotation = rotation };

        private static readonly Dictionary<string, Color> Colours = new Dictionary<string, Color>
        {
            ["Steel"] = new Color(0.78f, 0.80f, 0.84f),
            ["Iron"] = new Color(0.38f, 0.40f, 0.44f),
            ["Leather"] = new Color(0.42f, 0.26f, 0.14f),
            ["Wood"] = new Color(0.58f, 0.40f, 0.20f),
            ["Rust"] = new Color(0.48f, 0.36f, 0.24f),
            ["Cinder"] = new Color(0.50f, 0.08f, 0.06f),
            ["Charcoal"] = new Color(0.14f, 0.12f, 0.12f),
            ["Ember"] = new Color(1.00f, 0.45f, 0.08f),
            ["Arcane"] = new Color(0.55f, 0.80f, 1.00f),
            ["String"] = new Color(0.92f, 0.90f, 0.82f),
        };

        /// <summary>Each piece's placeholder, by gear id, in its slot's anchor space. Swords and staves stand up from the hand, tilted forward.</summary>
        private static readonly Dictionary<string, Part[]> Recipes = new Dictionary<string, Part[]>
        {
            ["hand-wraps"] = new[]
            {
                // Cloth wound round the fist, with a brass knuckle: the Monk's weapon (ticket 55).
                P(PrimitiveType.Cube, "Leather", new Vector3(0f, 0f, 0f), new Vector3(0.11f, 0.12f, 0.11f)),
                P(PrimitiveType.Cube, "Iron", new Vector3(0f, 0.02f, 0.06f), new Vector3(0.12f, 0.04f, 0.03f)),
            },
            ["acolyte-sceptre"] = new[]
            {
                // The Cleric's weapon (ticket 56): a short pale rod with a bright head.
                P(PrimitiveType.Cylinder, "Wood", new Vector3(0f, 0.2f, 0f), new Vector3(0.05f, 0.5f, 0.05f)),
                P(PrimitiveType.Sphere, "Arcane", new Vector3(0f, 0.75f, 0f), Vector3.one * 0.14f),
            },
            ["hexed-rod"] = new[]
            {
                // The Warlock's weapon (ticket 58): a dark rod with a charred head.
                P(PrimitiveType.Cylinder, "Charcoal", new Vector3(0f, 0.25f, 0f), new Vector3(0.05f, 0.6f, 0.05f)),
                P(PrimitiveType.Cube, "Cinder", new Vector3(0f, 0.9f, 0f), Vector3.one * 0.12f),
            },
            ["binding-staff"] = new[]
            {
                // The Summoner's weapon (ticket 57): a tall staff with a pale spirit stone.
                P(PrimitiveType.Cylinder, "Wood", new Vector3(0f, 0.35f, 0f), new Vector3(0.06f, 0.9f, 0.06f)),
                P(PrimitiveType.Sphere, "Arcane", new Vector3(0f, 1.3f, 0f), Vector3.one * 0.12f),
                P(PrimitiveType.Cube, "Charcoal", new Vector3(0f, 1.15f, 0f), new Vector3(0.2f, 0.03f, 0.03f)),
            },
            ["iron-sword"] = new[]
            {
                P(PrimitiveType.Cube, "Leather", new Vector3(0f, 0f, 0f), new Vector3(0.06f, 0.22f, 0.06f), new Vector3(30f, 0f, 0f)),
                P(PrimitiveType.Cube, "Iron", new Vector3(0f, 0.11f, 0.06f), new Vector3(0.3f, 0.05f, 0.08f), new Vector3(30f, 0f, 0f)),
                P(PrimitiveType.Cube, "Steel", new Vector3(0f, 0.5f, 0.29f), new Vector3(0.09f, 0.85f, 0.025f), new Vector3(30f, 0f, 0f)),
            },
            ["goblin-cleaver"] = new[]
            {
                P(PrimitiveType.Cube, "Leather", new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.28f, 0.07f), new Vector3(30f, 0f, 0f)),
                P(PrimitiveType.Cube, "Rust", new Vector3(0.06f, 0.42f, 0.24f), new Vector3(0.34f, 0.62f, 0.035f), new Vector3(30f, 0f, 0f)),
                P(PrimitiveType.Cube, "Iron", new Vector3(-0.1f, 0.42f, 0.24f), new Vector3(0.04f, 0.62f, 0.05f), new Vector3(30f, 0f, 0f)),
            },
            ["hunting-bow"] = new[]
            {
                P(PrimitiveType.Cube, "Leather", Vector3.zero, new Vector3(0.07f, 0.2f, 0.07f)),
                P(PrimitiveType.Cube, "Wood", new Vector3(0f, 0.38f, -0.06f), new Vector3(0.05f, 0.6f, 0.05f), new Vector3(-20f, 0f, 0f)),
                P(PrimitiveType.Cube, "Wood", new Vector3(0f, -0.38f, -0.06f), new Vector3(0.05f, 0.6f, 0.05f), new Vector3(20f, 0f, 0f)),
                P(PrimitiveType.Cube, "String", new Vector3(0f, 0f, -0.17f), new Vector3(0.012f, 1.3f, 0.012f)),
            },
            ["apprentice-staff"] = new[]
            {
                P(PrimitiveType.Cylinder, "Wood", new Vector3(0f, 0.3f, 0f), new Vector3(0.06f, 0.8f, 0.06f)),
                P(PrimitiveType.Sphere, "Arcane", new Vector3(0f, 1.15f, 0f), Vector3.one * 0.16f),
            },
            ["cinder-staff"] = new[]
            {
                P(PrimitiveType.Cylinder, "Charcoal", new Vector3(0f, 0.3f, 0f), new Vector3(0.07f, 0.8f, 0.07f)),
                P(PrimitiveType.Cylinder, "Cinder", new Vector3(0f, 1.08f, 0f), new Vector3(0.18f, 0.03f, 0.18f)),
                P(PrimitiveType.Sphere, "Ember", new Vector3(0f, 1.2f, 0f), Vector3.one * 0.22f),
            },
            ["cinder-hood"] = new[]
            {
                P(PrimitiveType.Sphere, "Cinder", new Vector3(0f, 0.02f, -0.02f), new Vector3(1.07f, 0.64f, 1.07f)),
                P(PrimitiveType.Cube, "Cinder", new Vector3(0f, 0.16f, -0.5f), Vector3.one * 0.2f, new Vector3(45f, 0f, 0f)),
            },
            ["cinder-robe"] = new[]
            {
                P(PrimitiveType.Cylinder, "Cinder", new Vector3(0f, -0.35f, 0f), new Vector3(1.08f, 0.55f, 1.08f)),
                P(PrimitiveType.Cylinder, "Ember", new Vector3(0f, 0.05f, 0f), new Vector3(1.1f, 0.04f, 1.1f)),
            },
            ["warlords-plate"] = new[]
            {
                P(PrimitiveType.Cylinder, "Iron", new Vector3(0f, 0.15f, 0f), new Vector3(1.1f, 0.35f, 1.1f)),
                P(PrimitiveType.Sphere, "Steel", new Vector3(-0.52f, 0.5f, 0f), Vector3.one * 0.42f),
                P(PrimitiveType.Sphere, "Steel", new Vector3(0.52f, 0.5f, 0f), Vector3.one * 0.42f),
            },
            ["cinder-gloves"] = new[]
            {
                P(PrimitiveType.Cube, "Charcoal", new Vector3(-0.55f, 0f, 0f), Vector3.one * 0.2f),
                P(PrimitiveType.Cube, "Charcoal", new Vector3(0.55f, 0f, 0f), Vector3.one * 0.2f),
                P(PrimitiveType.Cube, "Ember", new Vector3(-0.55f, 0.11f, 0f), new Vector3(0.22f, 0.04f, 0.22f)),
                P(PrimitiveType.Cube, "Ember", new Vector3(0.55f, 0.11f, 0f), new Vector3(0.22f, 0.04f, 0.22f)),
            },
            ["cinder-boots"] = new[]
            {
                P(PrimitiveType.Cube, "Charcoal", new Vector3(-0.2f, 0f, 0.06f), new Vector3(0.26f, 0.18f, 0.38f)),
                P(PrimitiveType.Cube, "Charcoal", new Vector3(0.2f, 0f, 0.06f), new Vector3(0.26f, 0.18f, 0.38f)),
                P(PrimitiveType.Cube, "Cinder", new Vector3(-0.2f, 0.14f, 0f), new Vector3(0.27f, 0.12f, 0.27f)),
                P(PrimitiveType.Cube, "Cinder", new Vector3(0.2f, 0.14f, 0f), new Vector3(0.27f, 0.12f, 0.27f)),
            },
            ["ember-heart"] = new[]
            {
                P(PrimitiveType.Sphere, "Ember", new Vector3(0f, 0f, 0.5f), Vector3.one * 0.2f),
                P(PrimitiveType.Cube, "Leather", new Vector3(0f, 0.12f, 0.47f), new Vector3(0.03f, 0.2f, 0.03f)),
            },
            ["hunters-quiver"] = new[]
            {
                P(PrimitiveType.Cylinder, "Leather", new Vector3(0.15f, 0f, -0.55f), new Vector3(0.22f, 0.32f, 0.22f), new Vector3(0f, 0f, -20f)),
                P(PrimitiveType.Cube, "Wood", new Vector3(0.27f, 0.38f, -0.55f), new Vector3(0.025f, 0.35f, 0.025f), new Vector3(0f, 0f, -20f)),
                P(PrimitiveType.Cube, "Wood", new Vector3(0.21f, 0.38f, -0.5f), new Vector3(0.025f, 0.35f, 0.025f), new Vector3(0f, 0f, -20f)),
                P(PrimitiveType.Cube, "String", new Vector3(0.31f, 0.55f, -0.55f), new Vector3(0.06f, 0.06f, 0.015f), new Vector3(0f, 0f, -20f)),
            },
        };

        /// <summary>Makes or rebuilds the placeholder looks. Returns how many were made.</summary>
        public static int SetUp()
        {
            Directory.CreateDirectory(MaterialsFolder);
            var materials = new Dictionary<string, Material>();
            Material MaterialFor(string name)
            {
                if (materials.TryGetValue(name, out var known)) return known;
                var path = $"{MaterialsFolder}/Gear_{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    var source = AssetDatabase.LoadAssetAtPath<Material>(ShaderSource);
                    material = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.SetColor("_BaseColor", Colours[name]);
                    material.SetColor("_Color", Colours[name]);
                    AssetDatabase.CreateAsset(material, path);
                    AssetDatabase.SetLabels(material, new[] { AiLabel });
                }
                materials[name] = material;
                return material;
            }

            var made = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:GearDefinitionAsset", new[] { GearFolder }))
            {
                var gear = AssetDatabase.LoadAssetAtPath<GearDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (gear == null) continue;
                var path = $"{Folder}/Gear_{ToPascal(gear.Id)}_AI.prefab";
                if (gear.Look != null && AssetDatabase.GetAssetPath(gear.Look) != path) continue; // a look put in by hand stays
                if (!Recipes.TryGetValue(gear.Id, out var recipe))
                {
                    Debug.LogWarning($"Gear '{gear.Id}' has no placeholder recipe; it is drawn with the plain stand-in until it gets a look.", gear);
                    continue;
                }

                var root = new GameObject($"Gear_{ToPascal(gear.Id)}");
                foreach (var part in recipe)
                {
                    var go = GameObject.CreatePrimitive(part.Shape);
                    UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                    go.name = part.Material;
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = part.Position;
                    go.transform.localRotation = Quaternion.Euler(part.Rotation);
                    go.transform.localScale = part.Scale;
                    go.GetComponent<Renderer>().sharedMaterial = MaterialFor(part.Material);
                }
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.SetLabels(prefab, new[] { AiLabel });

                var serialized = new SerializedObject(gear);
                serialized.FindProperty("look").objectReferenceValue = prefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(gear);
                made++;
            }
            AssetDatabase.SaveAssets();
            return made;
        }

        private static string ToPascal(string id)
        {
            var parts = id.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join("", parts);
        }
    }
}
