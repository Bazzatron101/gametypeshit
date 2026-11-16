using System.Collections;
using System.IO;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The whole continent in the real scene (ticket 45): the player starts in the guild town on the
    /// streamed ground, every tile has a far version, and across the map the road reaches Saltreach on
    /// the east coast. The tiles are not in git: rebuild them with Hunter System > Overworld > Rebuild
    /// Continent Scene; without them these tests are skipped.
    /// </summary>
    public class ContinentSceneTest
    {
        private SimulationRunner _runner;

        [UnitySetUp]
        public IEnumerator HostOnTheContinent()
        {
            if (!File.Exists(Path.Combine(Application.streamingAssetsPath, "Overworld", "Continent", "tile_0_0.bytes")))
                Assert.Ignore("The continent's tiles are not baked: Hunter System > Overworld > Rebuild Continent Scene.");
            TestSaves.UseFreshFolder();
            yield return SceneManager.LoadSceneAsync("Continent", LoadSceneMode.Single);
            yield return null;
            _runner = Object.FindAnyObjectByType<SimulationRunner>();
            _runner.Autosave.Enabled = false;
            _runner.StartHost(new SimulatedNetwork(0).HostEndpoint, id => JoinPoint.For(_runner.World, id, Vec3.Zero));
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThePlayerStartsInTheGuildTownOnTheContinent()
        {
            var world = _runner.World;
            var scene = Object.FindAnyObjectByType<OverworldScene>();
            Assert.That(world.InOverworld, Is.True);
            Assert.That(scene.Shown.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.name == "Far"), Is.EqualTo(42 * 31), "every tile has a far version");

            var me = _runner.LocalPlayerId.Value;
            yield return new WaitForSeconds(0.5f);
            var at = world.GetPlayer(me).Position;
            Assert.That(scene.Ground.IsLoaded(scene.Ground.TileAt(at)), Is.True, "the guild town's tile is loaded");
            Assert.That(world.Ground.KindAt(at), Is.EqualTo(GroundKind.Walkable), $"standing on land in town (at {at.X:0}, {at.Y:0.0}, {at.Z:0})");
            Assert.That(at.X, Is.LessThan(-3500f), "the guild town is on the west island");
        }

        [UnityTest]
        public IEnumerator TheRoadReachesSaltreachOnTheEastCoast()
        {
            var world = _runner.World;
            var scene = Object.FindAnyObjectByType<OverworldScene>();
            var asset = scene.Overworld;
            var main = asset.Roads.OrderByDescending(r => r.points.Max(p => p.x)).First();
            var end = main.points[main.points.Count - 2]; // just before the castle square
            var me = _runner.LocalPlayerId.Value;

            world.PlacePlayer(me, new Vec3(end.x, 0f, end.z));
            yield return new WaitForSeconds(1f); // its tile streams in
            var at = world.GetPlayer(me).Position;
            Assert.That(at.X, Is.GreaterThan(3000f), "far east");
            Assert.That(scene.Ground.IsLoaded(scene.Ground.TileAt(at)), Is.True);
            Assert.That(world.Ground.KindAt(at), Is.EqualTo(GroundKind.Walkable), "the road is walkable ground");
            Assert.That(at.Y, Is.EqualTo(world.Ground.HeightAt(at)).Within(1e-3f), "standing on it");
        }
    }
}
