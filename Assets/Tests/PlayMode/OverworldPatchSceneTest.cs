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
    /// The overworld test patch in the real scene (ticket 39): the simulation walks on the baked ground,
    /// the ground players see is built from it as flat-shaded tiles with a far version each, the Unity
    /// Terrain editing surface is hidden, and walking near the hidden gate finds it.
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
        public IEnumerator TheSimulationWalksOnTheBakedGroundThatPlayersSee()
        {
            var world = _runner.World;
            Assert.That(world.InOverworld, Is.True);
            Assert.That(world.Ground, Is.InstanceOf<HeightmapGround>());

            var scene = Object.FindAnyObjectByType<OverworldScene>();
            Assert.That(scene.Shown, Is.Not.Null, "the shown ground is built when the scene loads");
            var tiles = scene.Shown.GetComponentsInChildren<LODGroup>();
            Assert.That(tiles.Length, Is.EqualTo(16), "four by four tiles");
            Assert.That(tiles.All(t => t.lodCount == 2), Is.True, "each with a near and a far version");
            Assert.That(Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None), Is.Empty, "the editing surface is hidden");

            var me = _runner.LocalPlayerId.Value;
            world.PlacePlayer(me, new Vec3(-180f, 0f, 160f)); // the hilltop
            yield return null;
            var at = world.GetPlayer(me).Position;
            Assert.That(at.Y, Is.GreaterThan(40f));
            Assert.That(at.Y, Is.EqualTo(world.Ground.HeightAt(at)).Within(1e-3f), "standing on the ground");
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
