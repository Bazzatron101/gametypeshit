using System.Collections.Generic;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HunterSystem.Editor
{
    /// <summary>
    /// The overworld test patch (ticket 39): one square kilometre, four by four tiles of 250 m, built to try
    /// the terrain pipeline before the whole map (ticket 45). It has the guild town on its west side, a
    /// road east from it, a walkable hill, a steep ridge with a pass and a climbable face, a stretch of
    /// shore to the south, and two gates: the Goblin Warren by the road (known from the start) and
    /// Thornwood Hollow hidden beyond the ridge. Its stamps live in Assets/Data/Overworld/TestPatchSource.asset
    /// and are created once, so tuning there survives a rebuild; sculpting on the tiles survives it too.
    /// Unity -batchmode -quit -projectPath . -executeMethod HunterSystem.Editor.GreyboxSceneBuilder.BuildOverworldPatch
    /// </summary>
    public static partial class GreyboxSceneBuilder
    {
        public const string OverworldPatchScenePath = "Assets/Scenes/OverworldPatch.unity";
        private const string PatchSourcePath = "Assets/Data/Overworld/TestPatchSource.asset";

        private static readonly Vector3 PatchTown = new Vector3(-400f, 0f, -100f);

        [MenuItem("Hunter System/Overworld/Rebuild Test Patch Scene")]
        public static void BuildOverworldPatch()
        {
            var source = GetOrCreatePatchSource();
            EnvironmentArtSetup.SetUp(); // nature and buildings (ticket 49)
            BuildScene(OverworldPatchScenePath, simulation =>
            {
                Object.DestroyImmediate(GameObject.Find("Floor")); // the ground is the overworld's
                var camera = Object.FindAnyObjectByType<Camera>();
                camera.farClipPlane = 1500f;

                var runner = simulation.GetComponent<SimulationRunner>();
                var runnerSettings = new SerializedObject(runner);
                runnerSettings.FindProperty("useHub").boolValue = true; // the town is the hub, in the world (ticket 38)
                runnerSettings.ApplyModifiedPropertiesWithoutUndo();
                simulation.AddComponent<GateBoard>();
                simulation.AddComponent<TextScenePlayer>();
                Wire(simulation.AddComponent<GateView>(),
                    ("floorMaterial", GetOrCreateMaterial("Greybox_GateFloor", new Color(0.42f, 0.44f, 0.5f))),
                    ("wallMaterial", GetOrCreateMaterial("Greybox_GateWall", new Color(0.3f, 0.3f, 0.34f))),
                    ("checkpointMaterial", GetOrCreateMaterial("Greybox_Checkpoint", new Color(0.35f, 0.35f, 0.4f))),
                    ("checkpointActiveMaterial", GetOrCreateMaterial("Greybox_CheckpointActive", new Color(0.3f, 0.9f, 1f))),
                    ("portalMaterial", GetOrCreateMaterial("Greybox_Portal", new Color(0.55f, 0.95f, 1f))));
                simulation.AddComponent<GateHud>();

                var player = GameObject.Find("Player 1");
                player.transform.position = PatchTown + new Vector3(4f, 0f, 6f); // in town, by the board

                var surfaces = new[] // in TerrainSurface's order
                {
                    GetOrCreateMaterial("Overworld_Grass", new Color(0.42f, 0.6f, 0.3f)),
                    GetOrCreateMaterial("Overworld_Rock", new Color(0.45f, 0.43f, 0.42f)),
                    GetOrCreateMaterial("Overworld_Climbable", new Color(0.62f, 0.45f, 0.3f)),
                    GetOrCreateMaterial("Overworld_Sand", new Color(0.86f, 0.8f, 0.58f)),
                    GetOrCreateMaterial("Overworld_Road", new Color(0.6f, 0.5f, 0.38f)),
                };
                var sea = GetOrCreateMaterial("Overworld_Sea", new Color(0.2f, 0.42f, 0.65f));
                var gateMarker = GetOrCreateMaterial("Greybox_Portal", new Color(0.55f, 0.95f, 1f));
                var environment = AssetDatabase.LoadAssetAtPath<EnvironmentAsset>(EnvironmentArtSetup.EnvironmentPath);
                var overworld = new GameObject("Overworld").AddComponent<OverworldScene>();
                overworld.Set(null, source, surfaces, sea, gateMarker, environment);
                PlacePatchMarkers(overworld.transform);

                OverworldTools.Generate(overworld, source);
                var baked = OverworldTools.Bake(overworld, source);
                overworld.Set(baked, source, surfaces, sea, gateMarker, environment);

                // The board stands in the town, where the hub puts it.
                var hub = AssetDatabase.LoadAssetAtPath<HubAsset>(HubPath);
                var boardAt = PatchTown + (hub != null ? ToVector3(hub.ToDefinition().BoardOffset) : new Vector3(0f, 0f, 7f));
                var ground = baked.LoadGround();
                boardAt.y = ground.HeightAt(new Vec3(boardAt.x, 0f, boardAt.z));
                GreyboxVisuals.CreateVisual(overworld.gameObject, PrimitiveType.Cube, "Gate Board", boardAt + new Vector3(0f, 1.25f, 0f), new Vector3(3f, 2.5f, 0.3f),
                    GetOrCreateMaterial("Greybox_GateBoard", new Color(0.25f, 0.4f, 0.75f)));
            });
        }

        private static Vector3 ToVector3(Vec3 v) => new Vector3(v.X, v.Y, v.Z);

        private static void PlacePatchMarkers(Transform parent)
        {
            OverworldMarker Marker(string name, Vector3 at)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = at;
                return go.AddComponent<OverworldMarker>();
            }

            Marker("Town: guild town", PatchTown).Set(OverworldMarkerKind.Town, markerRadius: 40f, markerLabel: "Guild town");
            Marker("Gate: Goblin Warren", new Vector3(-120f, 0f, -45f)).Set(OverworldMarkerKind.Gate, GetOrCreateGoblinWarren(DemoModifierData.Select(GetOrCreateModifier).ToArray()), "greenmarch", known: true);
            Marker("Gate: Thornwood Hollow", new Vector3(360f, 0f, 330f)).Set(OverworldMarkerKind.Gate, GetOrCreateThornwoodHollow(), "greenmarch", known: false);
            Marker("Landmark: Lookout Hill", new Vector3(-180f, 0f, 160f)).Set(OverworldMarkerKind.Landmark, markerLabel: "Lookout Hill");

            var road = Marker("Road: east road", RoadPoints[0]);
            road.Set(OverworldMarkerKind.Road, markerRadius: 4f, markerLabel: "East road");
            for (var i = 1; i < RoadPoints.Length; i++)
            {
                var point = new GameObject($"Point {i}");
                point.transform.SetParent(road.transform, false);
                point.transform.position = RoadPoints[i];
            }
        }

        /// <summary>From the town east, past the warren, through the ridge's pass, and on to the north-east.</summary>
        private static readonly Vector3[] RoadPoints =
        {
            new Vector3(-400f, 0f, -100f), new Vector3(-250f, 0f, -60f), new Vector3(-60f, 0f, -20f),
            new Vector3(120f, 0f, 60f), new Vector3(230f, 0f, 80f), new Vector3(330f, 0f, 110f), new Vector3(420f, 0f, 250f),
        };

        /// <summary>The patch's recipe, created once: tune it in the Inspector, then regenerate and bake from the menu.</summary>
        private static OverworldSourceAsset GetOrCreatePatchSource()
        {
            var source = AssetDatabase.LoadAssetAtPath<OverworldSourceAsset>(PatchSourcePath);
            if (source != null) return source;
            EnsureFolder("Assets", "Data");
            EnsureFolder(DataFolder, "Overworld");
            source = ScriptableObject.CreateInstance<OverworldSourceAsset>();
            source.outputName = "TestPatch";
            source.origin = new Vector2(-500f, -500f);
            source.tileSize = 250f;
            source.tilesX = source.tilesZ = 4;
            source.heightmapResolution = 257;
            source.maxHeight = 120f;
            source.rules = new GroundRules { maxWalkableSlopeDegrees = 40f, seaLevel = 4f };
            source.heights = new HeightSource
            {
                baseHeight = 10f, noiseAmplitude = 3f, noiseScale = 140f, noiseSeed = 7,
                stamps = new List<HeightStamp>
                {
                    Stamp("Lookout Hill", HeightStampKind.Hill, 170f, 45f, 0f, new Vector2(-180f, 160f)),
                    Stamp("Spine ridge", HeightStampKind.Ridge, 45f, 70f, 0f, new Vector2(230f, -250f), new Vector2(230f, 480f)),
                    Stamp("Spine pass", HeightStampKind.Pass, 30f, 10f, 60f, new Vector2(230f, 80f)),
                    Stamp("South sea", HeightStampKind.Sea, 60f, 12f, 0f, new Vector2(-500f, -330f), new Vector2(500f, -330f)),
                    Stamp("Climbable face", HeightStampKind.Climbable, 14f, 0f, 0f, new Vector2(203f, 300f), new Vector2(203f, 360f)),
                    Stamp("Town square", HeightStampKind.Flatten, 40f, 0f, 20f, new Vector2(PatchTown.x, PatchTown.z)),
                    Stamp("East road", HeightStampKind.Flatten, 4f, 0f, 6f, RoadPoints.Select(p => new Vector2(p.x, p.z)).ToArray()),
                },
            };
            AssetDatabase.CreateAsset(source, PatchSourcePath);
            AssetDatabase.SaveAssets();
            return source;
        }

        private static HeightStamp Stamp(string label, HeightStampKind kind, float radius, float height, float blend, params Vector2[] points) =>
            new HeightStamp { label = label, kind = kind, radius = radius, height = height, blend = blend, points = new List<Vector2>(points) };

        /// <summary>
        /// A farming gate in the Greenmarch (ticket 36: "Thornwood hollow ... hidden, farming"), made of kit pieces.
        /// It is the test patch's hidden gate; on the board it is listed like any other until a scene has an overworld.
        /// </summary>
        private static GateDefinitionAsset GetOrCreateThornwoodHollow()
        {
            var goblin = GetOrCreateGoblinDefinition();
            var entrance = GetOrCreateRoom("Entrance Hall", 10f, 10f, checkpoint: false);
            var corridor = GetOrCreateRoom("Corridor", 4f, 8f, checkpoint: false);
            var den = GetOrCreateRoom("Goblin Den", 14f, 18f, checkpoint: true, At(goblin, -4f, 4f), At(goblin, 4f, 4f), At(goblin, 0f, 7f));
            return GetOrCreate<GateDefinitionAsset>("Gates", "Thornwood Hollow",
                a => a.Set("thornwood-hollow", "Thornwood Hollow", new[] { entrance, corridor, den }));
        }
    }
}
