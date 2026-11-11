using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HunterSystem.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/Greybox.unity: a grey floor, one capsule player, three Goblins, the
    /// System HUD, skill visuals, a camera and a light, and makes it the first scene in the build.
    /// Data assets (progression, enemies, skills) are created once and then left alone, so tuning
    /// done in the Inspector survives a rebuild. Regenerate from the menu or with:
    /// Unity -batchmode -quit -projectPath . -executeMethod HunterSystem.Editor.GreyboxSceneBuilder.Build
    /// </summary>
    public static class GreyboxSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Greybox.unity";
        private const string MaterialsFolder = "Assets/Art/Materials";
        private const string EnemiesFolder = "Assets/Data/Enemies";
        private const string ProgressionPath = "Assets/Data/Progression.asset";
        private const string SkillsFolder = "Assets/Data/Skills";

        /// <summary>The first skills, as created on a fresh project. After that the assets are the source of truth.</summary>
        private static readonly SkillDefinition[] StarterSkillData =
        {
            new SkillDefinition
            {
                Id = "rending-slash", Name = "Rending Slash", Effect = SkillEffectKind.Cone, Damage = 30, Range = 3f, ArcDegrees = 160f,
                CooldownSeconds = 4f, UnlockLevel = 1, ScalingStat = Stat.Strength, Tags = new[] { "melee", "physical" },
            },
            new SkillDefinition
            {
                Id = "ground-slam", Name = "Ground Slam", Effect = SkillEffectKind.Area, Damage = 20, Radius = 3.5f,
                CooldownSeconds = 6f, UnlockLevel = 1, ScalingStat = Stat.Strength, Tags = new[] { "melee", "area", "physical" },
            },
            new SkillDefinition
            {
                Id = "fire-bolt", Name = "Fire Bolt", Effect = SkillEffectKind.Projectile, Damage = 25, Range = 12f, Radius = 0.5f,
                ProjectileSpeed = 15f, CooldownSeconds = 2f, UnlockLevel = 1, ScalingStat = Stat.Intelligence,
                Tags = new[] { "fire", "projectile", "magic" },
            },
            new SkillDefinition
            {
                Id = "flame-burst", Name = "Flame Burst", Effect = SkillEffectKind.Area, Damage = 35, Radius = 4f,
                CooldownSeconds = 8f, UnlockLevel = 3, ScalingStat = Stat.Intelligence, Tags = new[] { "fire", "area", "magic" },
            },
        };

        /// <summary>Three Goblins at 25 XP each: enough for the first level-up (50 XP) in one fight.</summary>
        private static readonly Vector3[] GoblinSpawns = { new Vector3(0f, 0f, 8f), new Vector3(-4f, 0f, 10f), new Vector3(4f, 0f, 10f) };

        [MenuItem("Hunter System/Rebuild Greybox Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            // Matches the rig's starting orbit so the scene view looks right before Play.
            var focus = Vector3.up * ThirdPersonCamera.DefaultTargetHeight;
            camera.transform.position = CameraMath.OrbitPosition(focus, 0f, ThirdPersonCamera.DefaultPitch, ThirdPersonCamera.DefaultDistance);
            camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position, Vector3.up);
            var cameraRig = camera.gameObject.AddComponent<ThirdPersonCamera>();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(5f, 1f, 5f); // 50 m x 50 m
            floor.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Greybox_Floor", new Color(0.55f, 0.55f, 0.55f));

            var simulation = new GameObject("Simulation");
            var runner = simulation.AddComponent<SimulationRunner>();
            Wire(runner, ("progression", GetOrCreateProgression()));
            WireArray(runner, "skills", StarterSkillData.Select(GetOrCreateSkill).ToArray<Object>());
            simulation.AddComponent<SystemHud>();
            Wire(simulation.AddComponent<SkillEffectsView>(),
                ("projectileMaterial", GetOrCreateMaterial("Greybox_Fire", new Color(1f, 0.45f, 0.1f))),
                ("areaMaterial", GetOrCreateMaterial("Greybox_SkillArea", new Color(1f, 0.75f, 0.3f))),
                ("coneMaterial", GetOrCreateMaterial("Greybox_SkillCone", new Color(0.6f, 0.85f, 1f))));

            // Views sit at the feet; their visuals are children above.
            var player = new GameObject("Player 1");
            var playerView = player.AddComponent<PlayerView>();
            var playerBody = CreateVisual(player, PrimitiveType.Capsule, "Capsule", new Vector3(0f, 1f, 0f), Vector3.one,
                "Greybox_Player", new Color(0.2f, 0.45f, 0.85f));
            // A flat slab in front of the player, shown for a moment on each light attack.
            var swing = CreateVisual(player, PrimitiveType.Cube, "Swing Marker", new Vector3(0f, 1f, 1.1f), new Vector3(1.6f, 0.1f, 0.9f),
                "Greybox_Swing", new Color(1f, 0.85f, 0.3f));
            Wire(playerView, ("swingMarker", swing.gameObject), ("body", playerBody));
            Wire(cameraRig, ("target", player.transform));

            var goblin = GetOrCreateGoblinDefinition();
            for (var i = 0; i < GoblinSpawns.Length; i++)
            {
                var enemy = new GameObject($"Goblin {i + 1}");
                enemy.transform.position = GoblinSpawns[i];
                var enemyView = enemy.AddComponent<EnemyView>();
                var enemyBody = CreateVisual(enemy, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.8f, 0f), new Vector3(0.8f, 0.8f, 0.8f),
                    "Greybox_Goblin", new Color(0.8f, 0.2f, 0.2f));
                Wire(enemyView, ("definition", goblin), ("body", enemyBody));
            }

            EditorSceneManager.SaveScene(scene, ScenePath);

            var others = EditorBuildSettings.scenes.Where(s => s.path != ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(others).ToArray();
            AssetDatabase.SaveAssets();
        }

        /// <summary>A collider-less primitive parented under <paramref name="parent"/>. Movement is simulated, not physics-driven.</summary>
        private static Renderer CreateVisual(GameObject parent, PrimitiveType shape, string name, Vector3 localPosition, Vector3 localScale,
            string materialName, Color color)
        {
            var visual = GameObject.CreatePrimitive(shape);
            visual.name = name;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(parent.transform, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;
            var renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = GetOrCreateMaterial(materialName, color);
            return renderer;
        }

        /// <summary>
        /// Assigns private [SerializeField] references, as dragging them in the Inspector would.
        /// Field names are strings, so a rename throws here (FindProperty returns null) instead of failing silently.
        /// </summary>
        private static void Wire(Object target, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in fields) so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Assigns a private [SerializeField] array of references.</summary>
        private static void WireArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SkillDefinitionAsset GetOrCreateSkill(SkillDefinition data)
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Skills");
            var path = $"{SkillsFolder}/{data.Name.Replace(" ", "")}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<SkillDefinitionAsset>(path);
            if (asset != null) return asset; // keep any tuning done in the Inspector

            asset = ScriptableObject.CreateInstance<SkillDefinitionAsset>();
            asset.CopyFrom(data);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        private static ProgressionAsset GetOrCreateProgression()
        {
            EnsureFolder("Assets", "Data");
            var asset = AssetDatabase.LoadAssetAtPath<ProgressionAsset>(ProgressionPath);
            if (asset != null) return asset; // keep any tuning done in the Inspector

            asset = ScriptableObject.CreateInstance<ProgressionAsset>(); // its defaults hold the default curve
            AssetDatabase.CreateAsset(asset, ProgressionPath);
            return asset;
        }

        private static EnemyDefinitionAsset GetOrCreateGoblinDefinition()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Enemies");
            var path = $"{EnemiesFolder}/Goblin.asset";
            var asset = AssetDatabase.LoadAssetAtPath<EnemyDefinitionAsset>(path);
            if (asset != null) return asset; // keep any tuning done in the Inspector

            asset = ScriptableObject.CreateInstance<EnemyDefinitionAsset>();
            asset.SetStats("Goblin", maxHealth: 30, moveSpeed: 3f, stopDistance: 1.2f);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.CreateFolder("Assets/Art", "Materials");
            var path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline.defaultShader
                    : Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
