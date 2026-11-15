using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The overworld terrain pipeline (ticket 39): generated heights from stamps, hand edits kept when
    /// regenerating, the bake's rules (too steep is blocked, under the sea is water, painted faces are
    /// climbable), the baked ground's bytes, the flat-shaded meshes players see, and the baked test patch.
    /// </summary>
    public class TerrainPipelineTest
    {
        private static readonly Vector2 AreaMin = new Vector2(-500f, -500f);
        private static readonly Vector2 AreaSize = new Vector2(1000f, 1000f);

        private static HeightSource Flat(params HeightStamp[] stamps) => new HeightSource
        {
            baseHeight = 10f, noiseAmplitude = 0f, stamps = stamps.ToList(),
        };

        private static HeightStamp Stamp(HeightStampKind kind, float radius, float height, float blend, params Vector2[] points) =>
            new HeightStamp { kind = kind, radius = radius, height = height, blend = blend, points = points.ToList() };

        private static float At(HeightSource source, float x, float z) => source.Sample(x, z, AreaMin, AreaSize);

        // --- Generated heights ------------------------------------------------------------------------

        [Test]
        public void AHillRisesInItsMiddleAndNotBeyondItsRadius()
        {
            var source = Flat(Stamp(HeightStampKind.Hill, 100f, 30f, 0f, new Vector2(0f, 0f)));

            Assert.That(At(source, 0f, 0f), Is.EqualTo(40f).Within(1e-3f));
            Assert.That(At(source, 50f, 0f), Is.GreaterThan(10f).And.LessThan(40f));
            Assert.That(At(source, 150f, 0f), Is.EqualTo(10f).Within(1e-3f));
        }

        [Test]
        public void APassCutsThroughARidge()
        {
            var ridge = Stamp(HeightStampKind.Ridge, 40f, 60f, 0f, new Vector2(0f, -200f), new Vector2(0f, 200f));
            var source = Flat(ridge, Stamp(HeightStampKind.Pass, 20f, 14f, 50f, new Vector2(0f, 0f)));

            Assert.That(At(source, 0f, 150f), Is.EqualTo(70f).Within(1e-3f), "the ridge's crest");
            Assert.That(At(source, 0f, 0f), Is.EqualTo(14f).Within(1e-3f), "the pass");
        }

        [Test]
        public void TheSeaLiesBeyondItsLineWithAShore()
        {
            var source = Flat(Stamp(HeightStampKind.Sea, 50f, 12f, 0f, new Vector2(-500f, 0f), new Vector2(500f, 0f))); // walking east: the sea is south

            Assert.That(At(source, 0f, 10f), Is.EqualTo(10f).Within(1e-3f), "inland");
            Assert.That(At(source, 0f, -25f), Is.LessThan(10f).And.GreaterThan(-2f), "the shore");
            Assert.That(At(source, 0f, -100f), Is.EqualTo(-2f).Within(1e-3f), "out at sea");
        }

        [Test]
        public void ARoadIsLevelAcrossItsWidth()
        {
            var hill = Stamp(HeightStampKind.Hill, 200f, 40f, 0f, new Vector2(0f, 100f));
            var road = Stamp(HeightStampKind.Flatten, 4f, 0f, 6f, new Vector2(-100f, 0f), new Vector2(100f, 0f));
            var withRoad = Flat(hill, road);
            var withoutRoad = Flat(hill);

            Assert.That(At(withoutRoad, 0f, 3f), Is.Not.EqualTo(At(withoutRoad, 0f, -3f)).Within(1e-2f), "the hill slopes across it");
            Assert.That(At(withRoad, 0f, 3f), Is.EqualTo(At(withRoad, 0f, -3f)).Within(1e-3f), "the road does not");
            Assert.That(At(withRoad, 0f, 0f), Is.EqualTo(At(withoutRoad, 0f, 0f)).Within(1e-3f), "it follows the hill along its line");
        }

        [Test]
        public void ClimbableStampsMarkGroundNearTheirLine()
        {
            var source = Flat(Stamp(HeightStampKind.Climbable, 10f, 0f, 0f, new Vector2(0f, 0f), new Vector2(0f, 50f)));

            Assert.That(source.IsClimbable(5f, 25f), Is.True);
            Assert.That(source.IsClimbable(20f, 25f), Is.False);
            Assert.That(At(source, 5f, 25f), Is.EqualTo(10f), "the height is not changed");
        }

        [Test]
        public void RegeneratingKeepsHandEdits()
        {
            var lastGenerated = new[] { 0.2f, 0.2f, 0.2f, 0.2f };
            var current = new[] { 0.2f, 0.5f, 0.2f, 0.1f }; // a mound and a dip sculpted since
            var regenerated = new[] { 0.3f, 0.3f, 0.3f, 0.3f }; // the source was raised

            var result = GroundBake.KeepHandEdits(current, lastGenerated, regenerated, 0f, 1f);

            Assert.That(result, Is.EqualTo(new[] { 0.3f, 0.6f, 0.3f, 0.2f }).Within(1e-5f));
            Assert.That(GroundBake.KeepHandEdits(null, null, regenerated), Is.EqualTo(regenerated), "the first generation is as generated");
            Assert.That(GroundBake.KeepHandEdits(new[] { 1f }, new[] { 0f }, new[] { 0.5f }, 0f, 1f), Is.EqualTo(new[] { 1f }), "kept within the terrain's range");
        }

        // --- The bake -----------------------------------------------------------------------------------

        private static readonly GroundRules Rules = new GroundRules { maxWalkableSlopeDegrees = 40f, seaLevel = 4f };

        /// <summary>A 2 × 1 grid of 1 m cells, each rising <paramref name="rise"/> metres per metre eastwards.</summary>
        private static GroundKind[] TwoCells(float rise, float baseHeight = 10f, Func<int, int, bool> climbable = null)
        {
            var heights = new[] { baseHeight, baseHeight + rise, baseHeight + 2 * rise, baseHeight, baseHeight + rise, baseHeight + 2 * rise };
            return GroundBake.Classify(heights, 2, 1, 1f, Rules, climbable);
        }

        [Test]
        public void GentleGroundIsWalkableAndSteepGroundIsBlocked()
        {
            Assert.That(TwoCells(Mathf.Tan(30f * Mathf.Deg2Rad)), Is.All.EqualTo(GroundKind.Walkable), "30°");
            Assert.That(TwoCells(Mathf.Tan(50f * Mathf.Deg2Rad)), Is.All.EqualTo(GroundKind.Blocked), "50°");
        }

        [Test]
        public void GroundUnderTheSeaIsWater()
        {
            Assert.That(TwoCells(0f, baseHeight: 2f), Is.All.EqualTo(GroundKind.Water));
        }

        [Test]
        public void PaintedFacesAreClimbable()
        {
            var kinds = TwoCells(Mathf.Tan(60f * Mathf.Deg2Rad), climbable: (column, row) => column == 1);

            Assert.That(kinds, Is.EqualTo(new[] { GroundKind.Blocked, GroundKind.Climbable }));
        }

        [Test]
        public void BakedGroundSurvivesItsBytes()
        {
            var heights = new[] { 0f, 1f, 2f, 3f, 4f, 5f };
            var kinds = new[] { GroundKind.Walkable, GroundKind.Climbable };

            var ground = GroundBake.Decode(GroundBake.Encode(-10f, 20f, 2f, 2, 1, heights, kinds));

            Assert.That(ground.OriginX, Is.EqualTo(-10f));
            Assert.That(ground.OriginZ, Is.EqualTo(20f));
            Assert.That(ground.CellSize, Is.EqualTo(2f));
            Assert.That(ground.HeightAt(new Vec3(-6f, 0f, 22f)), Is.EqualTo(5f).Within(1e-5f));
            Assert.That(ground.KindAt(new Vec3(-7f, 0f, 21f)), Is.EqualTo(GroundKind.Climbable));
            Assert.Throws<FormatException>(() => GroundBake.Decode(new byte[] { 1, 2, 3 }));
        }

        // --- What players see ---------------------------------------------------------------------------

        [Test]
        public void ShownGroundIsFlatShadedAndSitsOnTheSimulationsGround()
        {
            var ground = new HeightmapGround(0f, 0f, 1f, 4, 4, Enumerable.Range(0, 25).Select(i => (i % 5) * 0.7f + (i / 5) * 0.3f).ToArray());

            var mesh = FlatTerrainMesh.Build(ground, 0f, 0f, 4f, 1f, _ => TerrainSurface.Grass);

            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var triangles = mesh.GetTriangles((int)TerrainSurface.Grass);
            Assert.That(triangles.Length, Is.EqualTo(4 * 4 * 2 * 3));
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var a = vertices[triangles[t]];
                var b = vertices[triangles[t + 1]];
                var c = vertices[triangles[t + 2]];
                var face = Vector3.Cross(b - a, c - a).normalized;
                Assert.That(face.y, Is.GreaterThan(0f), "faces point up");
                foreach (var i in new[] { triangles[t], triangles[t + 1], triangles[t + 2] })
                    Assert.That(Vector3.Distance(normals[i], face), Is.LessThan(1e-4f), "one normal per face: hard facets");
            }
            foreach (var v in vertices)
                Assert.That(v.y, Is.EqualTo(ground.HeightAt(new Vec3(v.x, 0f, v.z))).Within(1e-4f), "on the simulation's ground");
        }

        [Test]
        public void EachSurfaceIsItsOwnSubmesh()
        {
            var ground = new HeightmapGround(0f, 0f, 1f, 2, 1, new float[6], new[] { GroundKind.Walkable, GroundKind.Blocked });

            var mesh = FlatTerrainMesh.Build(ground, 0f, 0f, 2f, 1f, at => FlatTerrainMesh.SurfaceAt(ground, at, -10f, 0f, null));

            Assert.That(mesh.subMeshCount, Is.EqualTo(FlatTerrainMesh.SurfaceCount));
            Assert.That(mesh.GetTriangles((int)TerrainSurface.Grass).Length, Is.GreaterThan(0));
            Assert.That(mesh.GetTriangles((int)TerrainSurface.Rock).Length, Is.GreaterThan(0), "blocked ground is drawn as rock");
        }

        // --- The overworld asset ------------------------------------------------------------------------

        [Test]
        public void TheAssetGivesAConfigATiledGroundItsGatesAndTown()
        {
            var coarse = GroundBake.Encode(0f, 0f, 250f, 2, 2, new[] { 1f, 1f, 1f, 1f, 1f, 1f, 9f, 9f, 9f }, new GroundKind[4]);
            var asset = ScriptableObject.CreateInstance<OverworldAsset>();
            asset.Set(Vector2.zero, 250f, 2, 2, "Overworld/Nowhere", new TextAsset(coarse), new List<OverworldAsset.GateSiteData>
                {
                    new OverworldAsset.GateSiteData { gateId = "warren", entrance = new Vector3(5f, 1f, 6f), region = "greenmarch", knownFromStart = true },
                }, true, new Vector3(-40f, 3f, 12f), 30f, new List<OverworldAsset.RoadData>(), new List<OverworldAsset.LandmarkData>(), 4f, 4f, 16f);
            var config = new SimulationConfig { Hub = new HubDefinition() };

            asset.ApplyTo(config);

            var ground = (TiledGround)config.Ground;
            Assert.That(ground.TilesX, Is.EqualTo(2));
            Assert.That(ground.Loaded, Is.Empty, "tiles stream in later");
            Assert.That(ground.KindAt(new Vec3(100f, 0f, 100f)), Is.EqualTo(GroundKind.Unloaded));
            Assert.That(ground.HeightAt(new Vec3(100f, 0f, 500f)), Is.EqualTo(9f).Within(1e-4f), "heights from the coarse map meanwhile");
            Assert.That(config.Overworld.Gates.Single().GateId, Is.EqualTo("warren"));
            Assert.That(config.Overworld.Gates.Single().KnownFromStart, Is.True);
            Assert.That(config.Overworld.StartingRegions, Does.Contain("greenmarch"));
            Assert.That(config.Hub.Centre.X, Is.EqualTo(-40f));
            Assert.That(config.Hub.Centre.Z, Is.EqualTo(12f));
            UnityEngine.Object.DestroyImmediate(asset);
        }

        // --- The baked test patch -----------------------------------------------------------------------

        private const string PatchPath = "Assets/Data/Overworld/TestPatch.asset";

        private static OverworldAsset Patch()
        {
            var patch = AssetDatabase.LoadAssetAtPath<OverworldAsset>(PatchPath);
            Assert.That(patch, Is.Not.Null, "the test patch is baked (Hunter System > Overworld > Rebuild Test Patch Scene)");
            return patch;
        }

        [Test]
        public void ThePatchIsOneSquareKilometreWithAllItsFeatures()
        {
            var patch = Patch();
            var ground = patch.LoadGround();
            GroundKind Kind(float x, float z) => ground.KindAt(new Vec3(x, 0f, z));
            float Height(float x, float z) => ground.HeightAt(new Vec3(x, 0f, z));

            Assert.That(ground.MaxX - ground.OriginX, Is.EqualTo(1000f).Within(1e-2f));
            Assert.That(ground.MaxZ - ground.OriginZ, Is.EqualTo(1000f).Within(1e-2f));
            Assert.That(ground.CellSize, Is.LessThanOrEqualTo(1f), "about a metre per cell");

            Assert.That(Height(-180f, 160f), Is.GreaterThan(40f), "a hill");
            Assert.That(Kind(-180f, 160f), Is.EqualTo(GroundKind.Walkable), "that can be walked up");
            Assert.That(Kind(212f, -100f), Is.EqualTo(GroundKind.Blocked), "a steep ridge (its flank; the crest itself is rounded)");
            Assert.That(Kind(230f, 80f), Is.EqualTo(GroundKind.Walkable), "with a pass");
            var climbable = Enumerable.Range(0, 60).Count(i => Kind(203f, 300f + i) == GroundKind.Climbable);
            Assert.That(climbable, Is.GreaterThan(30), "a climbable face");
            Assert.That(Kind(0f, -470f), Is.EqualTo(GroundKind.Water), "the sea");
            Assert.That(Enumerable.Range(0, 200).Any(i => Kind(0f, -300f - i) == GroundKind.Walkable && Kind(0f, -301f - i) == GroundKind.Water), Is.True, "and a shore");

            Assert.That(patch.Roads, Has.Count.EqualTo(1), "a road");
            Assert.That(patch.Gates.Select(g => g.gateId), Is.EquivalentTo(new[] { "goblin-warren", "thornwood-hollow" }), "two gate markers");
            Assert.That(patch.Gates.Single(g => g.gateId == "goblin-warren").knownFromStart, Is.True);
            Assert.That(patch.Gates.Single(g => g.gateId == "thornwood-hollow").knownFromStart, Is.False);
            Assert.That(patch.HasTown, Is.True);
            foreach (var site in patch.Gates)
                Assert.That(ground.IsWalkable(new Vec3(site.entrance.x, 0f, site.entrance.z)), Is.True, $"{site.gateId}'s entrance can be reached");
        }

        /// <summary>Walks a player along <paramref name="route"/> on the patch's ground; returns where they end up.</summary>
        private static Vec3 Walk(IGround ground, Vec3 from, IEnumerable<Vector2> route, int maxTicks = 60 * 600)
        {
            var id = new PlayerId(1);
            var world = new SimulationWorld(new SimulationConfig { TickRate = 60, PlayerMoveSpeed = 8f, Ground = ground });
            world.AddPlayer(id, from);
            var ticks = 0;
            foreach (var target in route)
            {
                while (ticks++ < maxTicks)
                {
                    var at = world.GetPlayer(id).Position;
                    var toward = new Vector2(target.x - at.X, target.y - at.Z);
                    if (toward.magnitude < 1f) break;
                    toward.Normalize();
                    world.Step(new Dictionary<PlayerId, PlayerCommand> { [id] = new PlayerCommand(toward.x, toward.y) });
                }
            }
            return world.GetPlayer(id).Position;
        }

        [Test]
        public void ThePatchCanBeWalkedFromTheTownAlongTheRoadThroughThePassToTheHiddenGate()
        {
            var patch = Patch();
            var ground = patch.LoadGround();
            var road = patch.Roads[0].points.Select(p => new Vector2(p.x, p.z)).ToList();
            var thornwood = patch.Gates.Single(g => g.gateId == "thornwood-hollow").entrance;
            road.Add(new Vector2(thornwood.x, thornwood.z));

            var end = Walk(ground, new Vec3(patch.TownCentre.x, 0f, patch.TownCentre.z), road);

            Assert.That(new Vector2(end.X - thornwood.x, end.Z - thornwood.z).magnitude, Is.LessThan(2f), "at the hidden gate, beyond the ridge");
        }

        [Test]
        public void TheRidgeStopsAnyoneWalkingStraightAtIt()
        {
            var ground = Patch().LoadGround();

            var end = Walk(ground, new Vec3(100f, 0f, -150f), new[] { new Vector2(400f, -150f) }, maxTicks: 60 * 60);

            Assert.That(end.X, Is.LessThan(230f), "held on the near side of the ridge");
        }
    }
}
