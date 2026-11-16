using System.Collections.Generic;
using System.Linq;
using HunterSystem.Editor;
using HunterSystem.Game;
using NUnit.Framework;
using UnityEngine;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The continent (ticket 45), read straight from its recipe so no bake is needed: every road can be
    /// walked end to end (no water, nothing too steep), bridges carry roads over the sea and rivers, the
    /// towns sit on land, and the crossing from the guild town to Saltreach is about the length ticket 36 set.
    /// </summary>
    public class ContinentCrossingTest
    {
        private static OverworldSourceAsset _source;

        [OneTimeSetUp]
        public void SetUp() => _source = ContinentSetup.SetUp();

        private static float Height(Vector2 p) => _source.heights.Sample(p.x, p.y, _source.origin, _source.AreaSize);

        /// <summary>Why <paramref name="p"/> cannot be walked, or null when it can: the bake's rules at about 1 m.</summary>
        private static string Problem(Vector2 p)
        {
            var h = Height(p);
            if (h < _source.rules.seaLevel) return $"under the sea ({h:0.0} m)";
            if (_source.heights.IsPaintedWater(p.x, p.y, _source.origin, _source.AreaSize)) return "in a river or lake";
            var maxRise = Mathf.Tan(_source.rules.maxWalkableSlopeDegrees * Mathf.Deg2Rad);
            foreach (var d in new[] { Vector2.right, Vector2.up })
            {
                var rise = Mathf.Abs(Height(p + d) - h);
                if (rise > maxRise) return $"too steep ({Mathf.Atan(rise) * Mathf.Rad2Deg:0}°)";
            }
            return null;
        }

        private static List<Vector2> World(IEnumerable<Vector2> pixels) => pixels.Select(p => ContinentLayout.World(p, _source.origin, _source.AreaSize)).ToList();

        [Test]
        public void EveryRoadCanBeWalkedEndToEnd()
        {
            var problems = new List<string>();
            foreach (var road in ContinentLayout.Roads)
            {
                var steps = ContinentSetup.Walk(World(road.Pixels), 2f);
                for (var i = 0; i < steps.Count; i++)
                {
                    var problem = Problem(steps[i]);
                    if (problem == null) continue;
                    problems.Add($"{road.Name} at {i * 2} m ({steps[i].x:0}, {steps[i].y:0}): {problem}");
                    break; // one per road is enough to find it
                }
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void BridgesCarryRoadsOverTheSeaAndRivers()
        {
            var bridges = _source.heights.stamps.Where(s => s.kind == HeightStampKind.Bridge).ToList();
            Assert.That(bridges.Count(b => b.label.Contains("West bridge")), Is.GreaterThanOrEqualTo(1), "the bridge from the west island");
            Assert.That(bridges.Count(b => b.label.Contains("South bridge")), Is.GreaterThanOrEqualTo(1), "the bridge to the south island");
            Assert.That(bridges.Count, Is.GreaterThanOrEqualTo(4), "and over rivers where roads cross them");
        }

        [Test]
        public void TownsAndGateSpotsAreOnLand()
        {
            var places = new[] { ContinentLayout.GuildTown }.Concat(ContinentLayout.Settlements).Select(p => (p.Name, p.Pixel))
                .Concat(ContinentLayout.Gates.Select(g => (g.Name, g.Pixel)));
            // The bare land, without stamps: a bridge or a town square must not be what holds a place above the sea.
            var bare = new HeightSource
            {
                baseHeight = _source.heights.baseHeight, noiseAmplitude = 0f,
                paintedHeights = _source.heights.paintedHeights, paintedHeightScale = _source.heights.paintedHeightScale,
            };
            var wet = places.Where(p =>
            {
                var w = ContinentLayout.World(p.Pixel, _source.origin, _source.AreaSize);
                return bare.Sample(w.x, w.y, _source.origin, _source.AreaSize) < _source.rules.seaLevel ||
                       _source.heights.IsPaintedWater(w.x, w.y, _source.origin, _source.AreaSize);
            }).Select(p => p.Name).ToList();
            Assert.That(wet, Is.Empty, "in the sea or a lake: " + string.Join(", ", wet));
        }

        [Test]
        public void TheCrossingIsAboutFortyMinutesOnFoot()
        {
            var path = ContinentLayout.CrossingPath(_source.origin, _source.AreaSize);
            var metres = 0f;
            for (var i = 1; i < path.Count; i++) metres += Vector2.Distance(path[i - 1], path[i]);
            var walking = metres / 5f / 60f;   // PlayerTuning: 5 m/s
            var mounted = metres / 16f / 60f;  // ticket 36: 16 m/s
            TestContext.WriteLine($"Guild town to Saltreach by road: {metres / 1000f:0.0} km, {walking:0} min walking, {mounted:0} min mounted.");
            Assert.That(walking, Is.InRange(30f, 55f), $"{metres / 1000f:0.0} km is {walking:0} minutes walking; ticket 36 aims for about 40");
        }
    }
}
