using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using UnityEditor;
using UnityEngine;

namespace HunterSystem.Editor
{
    /// <summary>
    /// The whole continent as a walkable scene (ticket 45): the terrain from the continent recipe
    /// (<see cref="ContinentSetup"/>: the elevation and water maps, town squares, roads and bridges), towns,
    /// castles, gate spots and roads as markers from <see cref="ContinentLayout"/>, and the player in the
    /// guild town. Its 1,302 tiles are baked straight from the recipe into StreamingAssets and are not kept
    /// in git (rebuild them with this menu item). Takes a few minutes.
    /// Unity -batchmode -quit -projectPath . -executeMethod HunterSystem.Editor.GreyboxSceneBuilder.BuildContinent
    /// </summary>
    public static partial class GreyboxSceneBuilder
    {
        public const string ContinentScenePath = "Assets/Scenes/Continent.unity";

        [MenuItem("Hunter System/Overworld/Rebuild Continent Scene")]
        public static void BuildContinent()
        {
            var source = ContinentSetup.SetUp();
            EnvironmentArtSetup.SetUp(); // nature and buildings (ticket 49)
            var min = source.origin;
            var size = source.AreaSize;
            Vector3 At(Vector2 pixel)
            {
                var w = ContinentLayout.World(pixel, min, size);
                return new Vector3(w.x, 0f, w.y);
            }
            var town = At(ContinentLayout.GuildTown.Pixel);

            BuildScene(ContinentScenePath, simulation =>
            {
                Object.DestroyImmediate(GameObject.Find("Floor")); // the ground is the overworld's
                var camera = Object.FindAnyObjectByType<Camera>();
                camera.farClipPlane = 2500f;

                var runner = simulation.GetComponent<SimulationRunner>();
                var runnerSettings = new SerializedObject(runner);
                runnerSettings.FindProperty("useHub").boolValue = true; // the guild town is the hub, in the world (ticket 38)
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
                player.transform.position = town + new Vector3(4f, 0f, 6f); // in the guild town, by the board

                var surfaces = new[] // in TerrainSurface's order
                {
                    GetOrCreateMaterial("Overworld_Grass", new Color(0.42f, 0.6f, 0.3f)),
                    GetOrCreateMaterial("Overworld_Rock", new Color(0.45f, 0.43f, 0.42f)),
                    GetOrCreateMaterial("Overworld_Climbable", new Color(0.62f, 0.45f, 0.3f)),
                    GetOrCreateMaterial("Overworld_Sand", new Color(0.86f, 0.8f, 0.58f)),
                    GetOrCreateMaterial("Overworld_Road", new Color(0.6f, 0.5f, 0.38f)),
                    GetOrCreateMaterial("Overworld_River", new Color(0.25f, 0.5f, 0.72f)),
                };
                var sea = GetOrCreateMaterial("Overworld_Sea", new Color(0.2f, 0.42f, 0.65f));
                var gateMarker = GetOrCreateMaterial("Greybox_Portal", new Color(0.55f, 0.95f, 1f));
                source = AssetDatabase.LoadAssetAtPath<OverworldSourceAsset>(ContinentSetup.SourcePath); // the new scene unloaded what was loaded before
                var environment = AssetDatabase.LoadAssetAtPath<EnvironmentAsset>(EnvironmentArtSetup.EnvironmentPath);
                var overworld = new GameObject("Overworld").AddComponent<OverworldScene>();
                overworld.Set(null, source, surfaces, sea, gateMarker, environment);
                PlaceContinentMarkers(overworld.transform, At);

                var baked = OverworldTools.BakeFromRecipe(source);
                overworld.Set(baked, source, surfaces, sea, gateMarker, environment);

                // The board stands in the guild town, where the hub puts it.
                var hub = AssetDatabase.LoadAssetAtPath<HubAsset>(HubPath);
                var boardAt = town + (hub != null ? ToVector3(hub.ToDefinition().BoardOffset) : new Vector3(0f, 0f, 7f));
                boardAt.y = source.heights.Sample(boardAt.x, boardAt.z, min, size);
                GreyboxVisuals.CreateVisual(overworld.gameObject, PrimitiveType.Cube, "Gate Board", boardAt + new Vector3(0f, 1.25f, 0f), new Vector3(3f, 2.5f, 0.3f),
                    GetOrCreateMaterial("Greybox_GateBoard", new Color(0.25f, 0.4f, 0.75f)));
            });
        }

        private static void PlaceContinentMarkers(Transform parent, System.Func<Vector2, Vector3> at)
        {
            OverworldMarker Marker(string name, Vector3 position)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = position;
                return go.AddComponent<OverworldMarker>();
            }

            Marker("Town: " + ContinentLayout.GuildTown.Name, at(ContinentLayout.GuildTown.Pixel))
                .Set(OverworldMarkerKind.Town, markerRadius: 60f, markerLabel: ContinentLayout.GuildTown.Name);
            // Villages and castle towns get buildings from the bake (ticket 49), inside their flattened squares.
            foreach (var place in ContinentLayout.Settlements)
                Marker((place.Castle ? "Castle: " : "Village: ") + place.Name, at(place.Pixel))
                    .Set(OverworldMarkerKind.Village, markerRegion: ContinentLayout.RegionNear(place.Pixel), markerRadius: place.Castle ? 58f : 48f, // bigger places (ticket 84)
                        markerLabel: place.Name, isCastle: place.Castle);

            var gates = AssetDatabase.FindAssets("t:GateDefinitionAsset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GateDefinitionAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(g => g != null).ToDictionary(g => g.Id);
            foreach (var spot in ContinentLayout.Gates)
            {
                if (spot.GateId != null && gates.TryGetValue(spot.GateId, out var gate))
                    Marker($"Gate: {spot.Name} ({gate.DisplayName})", at(spot.Pixel)).Set(OverworldMarkerKind.Gate, gate, spot.Region, known: spot.Known);
                else
                    Marker($"Gate spot: {spot.Name}", at(spot.Pixel)).Set(OverworldMarkerKind.Landmark, markerLabel: $"Gate spot: {spot.Name} ({spot.Region})");
            }

            foreach (var road in ContinentLayout.Roads)
            {
                var marker = Marker("Road: " + road.Name, at(road.Pixels[0]));
                marker.Set(OverworldMarkerKind.Road, markerRadius: 4f, markerLabel: road.Name);
                for (var i = 1; i < road.Pixels.Length; i++)
                {
                    var point = new GameObject($"Point {i}");
                    point.transform.SetParent(marker.transform, false);
                    point.transform.position = at(road.Pixels[i]);
                }
            }
        }
    }
}
