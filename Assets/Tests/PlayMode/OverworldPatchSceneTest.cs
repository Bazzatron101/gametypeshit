using System.Collections;
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
    /// The overworld test patch in the real scene (tickets 39 and 40): the simulation walks on the streamed
    /// ground, every tile shows a far version and the tiles near players their full one, the host loads the
    /// tiles around every player and drops the rest, the Unity Terrain editing surface is hidden, and
    /// walking near the hidden gate finds it.
    /// </summary>
    public class OverworldPatchSceneTest
    {
        private SimulationRunner _runner;

        [UnitySetUp]
        public IEnumerator HostOnThePatch()
        {
            TestSaves.UseFreshFolder();
            yield return SceneManager.LoadSceneAsync("OverworldPatch", LoadSceneMode.Single);
            yield return null;
            _runner = Object.FindAnyObjectByType<SimulationRunner>();
            _runner.Autosave.Enabled = false;
            _runner.StartHost(new SimulatedNetwork(0).HostEndpoint, id => JoinPoint.For(_runner.World, id, Vec3.Zero));
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheSimulationWalksOnTheStreamedGroundThatPlayersSee()
        {
            var world = _runner.World;
            Assert.That(world.InOverworld, Is.True);
            Assert.That(world.Ground, Is.InstanceOf<TiledGround>());

            var scene = Object.FindAnyObjectByType<OverworldScene>();
            Assert.That(scene.Shown, Is.Not.Null, "the shown ground is built when the scene loads");
            Assert.That(scene.Shown.GetComponentsInChildren<MeshRenderer>(true).Count(r => r.name == "Far"), Is.EqualTo(16), "every tile has a far version");
            Assert.That(Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None), Is.Empty, "the editing surface is hidden");

            var me = _runner.LocalPlayerId.Value;
            var ground = scene.Ground;
            Assert.That(scene.ShownNear, Has.Member(ground.TileAt(world.GetPlayer(me).Position)), "the tile underfoot is shown in full");

            world.PlacePlayer(me, new Vec3(-180f, 0f, 160f)); // the hilltop
            yield return new WaitForSeconds(0.5f); // its tile streams in
            var at = world.GetPlayer(me).Position;
            Assert.That(ground.IsLoaded(ground.TileAt(at)), Is.True);
            Assert.That(at.Y, Is.GreaterThan(40f));
            Assert.That(at.Y, Is.EqualTo(world.Ground.HeightAt(at)).Within(1e-3f), "standing on the ground");
        }

        [UnityTest]
        public IEnumerator TheHostLoadsTheTilesAroundEveryPlayerAndDropsTheRest()
        {
            var world = _runner.World;
            var scene = Object.FindAnyObjectByType<OverworldScene>();
            var ground = scene.Ground;
            scene.SetRadii(50f, 100f);
            var me = _runner.LocalPlayerId.Value;
            var far = new PlayerId(99); // another player, far across the patch
            world.PlacePlayer(me, new Vec3(-440f, 0f, -100f));
            world.AddPlayer(far, new Vec3(440f, 0f, 440f));
            yield return new WaitForSeconds(1f);

            var mine = ground.TileAt(world.GetPlayer(me).Position);
            var theirs = ground.TileAt(world.GetPlayer(far).Position);
            Assert.That(ground.IsLoaded(mine), Is.True, "my tile");
            Assert.That(ground.IsLoaded(theirs), Is.True, "and theirs, across the map, on the host");
            Assert.That(ground.IsLoaded(new TileCoord(2, 1)), Is.False, "nothing in between");
            Assert.That(scene.ShownNear, Has.Member(theirs));

            world.PlacePlayer(far, new Vec3(-440f, 0f, -60f)); // they come over to me
            yield return new WaitForSeconds(1f);
            Assert.That(ground.IsLoaded(theirs), Is.False, "their old tile is dropped");
            Assert.That(scene.ShownNear, Has.No.Member(theirs));
        }

        [UnityTest]
        public IEnumerator WalkingNearTheHiddenGateFindsIt()
        {
            var world = _runner.World;
            var me = _runner.LocalPlayerId.Value;
            Assert.That(world.IsGateFound("goblin-warren"), Is.True, "the warren is known from the start");
            Assert.That(world.IsGateFound("thornwood-hollow"), Is.False, "hidden at first");

            var site = world.Overworld.SiteOf("thornwood-hollow");
            world.PlacePlayer(me, site.Entrance + new Vec3(-5f, 0f, 0f));
            yield return new WaitForSeconds(0.25f); // a few simulation ticks

            Assert.That(world.GetPlayer(me).Position.X, Is.EqualTo(site.Entrance.X - 5f).Within(0.5f), "still standing by the gate");
            Assert.That(world.IsGateFound("thornwood-hollow"), Is.True, "found by standing near it");
            Assert.That(world.GateEntranceNear(me)?.GateId, Is.Null.Or.EqualTo("thornwood-hollow"));
        }
    }
}
