using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Builds greybox stand-in visuals from primitives. Shared by the scene builders (in the
    /// editor) and the test arena (at runtime), so a spawned enemy looks the same as a placed one.
    /// </summary>
    public static class GreyboxVisuals
    {
        public static readonly Vector3 EnemyBodyOffset = new Vector3(0f, 0.8f, 0f);
        public static readonly Vector3 EnemyBodyScale = new Vector3(0.8f, 0.8f, 0.8f);

        /// <summary>A collider-less primitive under <paramref name="parent"/>. Movement is simulated, not physics-driven.</summary>
        public static Renderer CreateVisual(GameObject parent, PrimitiveType shape, string name, Vector3 localPosition, Vector3 localScale,
            Material material)
        {
            var visual = GameObject.CreatePrimitive(shape);
            visual.name = name;
            var collider = visual.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
            visual.transform.SetParent(parent.transform, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localScale = localScale;
            var renderer = visual.GetComponent<Renderer>();
            if (material != null) renderer.sharedMaterial = material;
            return renderer;
        }

        /// <summary>A player at their feet: a capsule body above, and a flat swing marker in front shown on attacks.</summary>
        public static PlayerView CreatePlayer(string name, PlayerId id, bool isLocal, Material bodyMaterial, Material swingMaterial)
        {
            var root = new GameObject(name);
            var view = root.AddComponent<PlayerView>();
            var body = CreateVisual(root, PrimitiveType.Capsule, "Capsule", new Vector3(0f, 1f, 0f), Vector3.one, bodyMaterial);
            var swing = CreateVisual(root, PrimitiveType.Cube, "Swing Marker", new Vector3(0f, 1f, 1.1f), new Vector3(1.6f, 0.1f, 0.9f), swingMaterial);
            view.Configure(id, isLocal, body, swing.gameObject);
            return view;
        }

        /// <summary>An enemy at its feet with a capsule body above, configured from its definition.</summary>
        public static EnemyView CreateEnemy(string name, Vector3 position, EnemyDefinitionAsset definition, Material bodyMaterial)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            var view = root.AddComponent<EnemyView>();
            var body = CreateVisual(root, PrimitiveType.Capsule, "Body", EnemyBodyOffset, EnemyBodyScale, bodyMaterial);
            view.Configure(definition, body);
            return view;
        }
    }
}
