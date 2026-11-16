using System.Linq;
using HunterSystem.Editor;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The continent's painted maps (ticket 39): rivers and lakes from a painted water map are water even
    /// above sea level, a road over a river is a crossing, they are drawn as water, and the continent
    /// recipe reads the elevation and water maps made from the reference drawing.
    /// </summary>
    public class ContinentMapsTest
    {
        private static readonly GroundRules Rules = new GroundRules { maxWalkableSlopeDegrees = 40f, seaLevel = 4f };

        /// <summary>A 4 × 4 water map: white (water) in the west half, black in the east.</summary>
        private static Texture2D WestHalfWater()
        {
            var texture = new Texture2D(4, 4, TextureFormat.R8, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
                texture.SetPixel(x, y, x < 2 ? Color.white : Color.black);
            texture.Apply();
            return texture;
        }

        [Test]
        public void PaintedWaterIsWaterEvenHighUp()
        {
            var heights = Enumerable.Repeat(80f, 3 * 2).ToArray(); // 2 × 1 cells, 80 m up, flat
            var kinds = GroundBake.Classify(heights, 2, 1, 1f, Rules, water: (c, r) => c == 0);

            Assert.That(kinds, Is.EqualTo(new[] { GroundKind.Water, GroundKind.Walkable }));
        }

        [Test]
        public void TheWaterMapMarksRiversButARoadCrossesThem()
        {
            var source = new HeightSource { paintedWater = WestHalfWater() };
            var min = new Vector2(0f, 0f);
            var size = new Vector2(100f, 100f);

            Assert.That(source.IsPaintedWater(20f, 50f, min, size), Is.True, "west half: water");
            Assert.That(source.IsPaintedWater(80f, 50f, min, size), Is.False, "east half: land");
            Assert.That(source.IsPaintedWater(-10f, 50f, min, size), Is.False, "outside the map: not painted");

            source.stamps.Add(new HeightStamp
            {
                kind = HeightStampKind.Flatten, radius = 4f, blend = 2f,
                points = new System.Collections.Generic.List<Vector2> { new Vector2(0f, 50f), new Vector2(100f, 50f) },
            });
            Assert.That(source.IsPaintedWater(20f, 50f, min, size), Is.False, "the road is a bridge or ford");
            Assert.That(source.IsPaintedWater(20f, 70f, min, size), Is.True, "off the road it is still river");
        }

        [Test]
        public void RiversAboveTheSeaAreDrawnAsWater()
        {
            var heights = Enumerable.Repeat(30f, 3 * 2).ToArray();
            var ground = new HeightmapGround(0f, 0f, 1f, 2, 1, heights, new[] { GroundKind.Water, GroundKind.Walkable });

            Assert.That(FlatTerrainMesh.SurfaceAt(ground, new Vector3(0.5f, 30f, 0.5f), 4f, 1f, null), Is.EqualTo(TerrainSurface.Water));
            Assert.That(FlatTerrainMesh.SurfaceAt(ground, new Vector3(1.5f, 30f, 0.5f), 4f, 1f, null), Is.EqualTo(TerrainSurface.Grass));
        }

        [Test]
        public void TheContinentRecipeReadsTheElevationAndWaterMaps()
        {
            var source = ContinentSetup.SetUp();

            var heights = source.heights.paintedHeights;
            Assert.That(heights, Is.Not.Null);
            Assert.That(heights.isReadable, Is.True);
            Assert.That(heights.format, Is.EqualTo(TextureFormat.R16), "16-bit, so slopes are smooth");
            Assert.That(AssetDatabase.GetLabels(heights), Has.Member(ContinentSetup.AiLabel), "a placeholder to replace");
            Assert.That(AssetDatabase.GetLabels(source.heights.paintedWater), Has.Member(ContinentSetup.AiLabel));
            Assert.That(source.maxHeight, Is.GreaterThanOrEqualTo(ContinentSetup.HeightScale));
            Assert.That(source.AreaSize.x, Is.EqualTo(10500f));

            // Points from the drawing (909 × 673), in world metres with the map's centre at the origin.
            Vector2 World(float px, float py) => new Vector2(
                source.origin.x + px / 909f * source.AreaSize.x, source.origin.y + (1f - py / 673f) * source.AreaSize.y);
            float Height(Vector2 p) => source.heights.Sample(p.x, p.y, source.origin, source.AreaSize);
            bool Water(Vector2 p) => source.heights.IsPaintedWater(p.x, p.y, source.origin, source.AreaSize);

            Assert.That(Height(World(546f, 360f)), Is.GreaterThan(150f), "the snowy mountains are high");
            Assert.That(Height(World(10f, 10f)), Is.LessThan(source.rules.seaLevel), "the far corner is sea");
            Assert.That(Height(World(600f, 260f)), Is.InRange(source.rules.seaLevel, 120f), "the plains are land, below the peaks");
            Assert.That(Water(World(690f, 300f)), Is.True, "the Steppe lake");
            Assert.That(Water(World(790f, 300f)), Is.True, "the Ember river");
            Assert.That(Water(World(600f, 260f)), Is.False, "the plains are dry");
        }
    }
}
