using System.Linq;
using HunterSystem.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HunterSystem.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/Greybox.unity: a grey floor, one capsule player, a camera and a
    /// light, and makes it the first scene in the build. Regenerate from the menu or with:
    /// Unity -batchmode -quit -projectPath . -executeMethod HunterSystem.Editor.GreyboxSceneBuilder.Build
    /// </summary>
    public static class GreyboxSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Greybox.unity";
        private const string MaterialsFolder = "Assets/Art/Materials";

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

            new GameObject("Simulation").AddComponent<SimulationRunner>();

            var player = new GameObject("Player 1");
            player.AddComponent<PlayerView>();
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Capsule";
            Object.DestroyImmediate(body.GetComponent<Collider>()); // movement is simulated, not physics-driven
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f); // PlayerView sits at the feet
            body.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Greybox_Player", new Color(0.2f, 0.45f, 0.85f));

            EditorSceneManager.SaveScene(scene, ScenePath);

            var others = EditorBuildSettings.scenes.Where(s => s.path != ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(others).ToArray();
            AssetDatabase.SaveAssets();
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
