using System.Linq;
using HunterSystem.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HunterSystem.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/Greybox.unity: a grey floor, one capsule player, one Goblin enemy,
    /// a camera and a light, and makes it the first scene in the build. Regenerate from the menu or with:
    /// Unity -batchmode -quit -projectPath . -executeMethod HunterSystem.Editor.GreyboxSceneBuilder.Build
    /// </summary>
    public static class GreyboxSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Greybox.unity";
        private const string MaterialsFolder = "Assets/Art/Materials";
        private const string EnemiesFolder = "Assets/Data/Enemies";

        [MenuItem("Hunter System/Rebuild Greybox Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 12f, -12f);
            camera.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(5f, 1f, 5f); // 50 m x 50 m
            floor.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Greybox_Floor", new Color(0.55f, 0.55f, 0.55f));

            var simulation = new GameObject("Simulation");
            simulation.AddComponent<SimulationRunner>();
            simulation.AddComponent<CombatHud>();

            // Views sit at the feet; their visuals are children above.
            var player = new GameObject("Player 1");
            var playerView = player.AddComponent<PlayerView>();
            var playerBody = CreateVisual(player, PrimitiveType.Capsule, "Capsule", new Vector3(0f, 1f, 0f), Vector3.one,
                "Greybox_Player", new Color(0.2f, 0.45f, 0.85f));
            // A flat slab in front of the player, shown for a moment on each light attack.
            var swing = CreateVisual(player, PrimitiveType.Cube, "Swing Marker", new Vector3(0f, 1f, 1.1f), new Vector3(1.6f, 0.1f, 0.9f),
                "Greybox_Swing", new Color(1f, 0.85f, 0.3f));
            Wire(playerView, ("swingMarker", swing.gameObject), ("body", playerBody));

            var enemy = new GameObject("Goblin");
            enemy.transform.position = new Vector3(0f, 0f, 8f);
            var enemyView = enemy.AddComponent<EnemyView>();
            var enemyBody = CreateVisual(enemy, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.8f, 0f), new Vector3(0.8f, 0.8f, 0.8f),
                "Greybox_Goblin", new Color(0.8f, 0.2f, 0.2f));
            Wire(enemyView, ("definition", GetOrCreateGoblinDefinition()), ("body", enemyBody));

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

        private static EnemyDefinitionAsset GetOrCreateGoblinDefinition()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            if (!AssetDatabase.IsValidFolder(EnemiesFolder)) AssetDatabase.CreateFolder("Assets/Data", "Enemies");
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
