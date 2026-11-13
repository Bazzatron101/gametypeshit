using System.Collections;
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
    /// The hub in the real co-op scene (ticket 19): hosting puts you in the hub, the gate board
    /// takes a ready party into the gate, the portal shows after the clear, and leaving brings you
    /// home. The rules themselves are covered headlessly (HubTest, HubRoundTripTest).
    /// </summary>
    public class HubSceneTest
    {
        private SimulationRunner _runner;

        [UnitySetUp]
        public IEnumerator HostInTheHub()
        {
            TestSaves.UseFreshFolder();
            LogAssert.ignoreFailingMessages = true; // Fish-Net's inactive network managers complain in the editor; see GateSceneTest
            yield return SceneManager.LoadSceneAsync("Coop", LoadSceneMode.Single);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            _runner = Object.FindAnyObjectByType<SimulationRunner>();
            _runner.Autosave.Enabled = false;
            _runner.StartHost(new SimulatedNetwork(0).HostEndpoint, id => JoinPoint.For(_runner.World, id, Vec3.Zero));
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator FromTheHubThroughTheBoardIntoTheGateAndHomeAgain()
        {
            var hubView = Object.FindAnyObjectByType<HubView>();
            var board = Object.FindAnyObjectByType<GateBoard>();
            var gateView = Object.FindAnyObjectByType<GateView>();
            var me = _runner.LocalPlayerId.Value;
            var world = _runner.World;
            Assert.That(world.InHub, Is.True, "hosting puts you in the hub");
            Assert.That(hubView.IsShown, Is.True);

            world.PlacePlayer(me, world.Hub.Board + new Vec3(0f, 0f, -1.5f));
            yield return null;
            Assert.That(board.InReach, Is.True, "standing at the board");
            board.SetOpen(true);
            var gateId = _runner.Content.Gates[0].Id;
            Assert.That(board.StartGate(gateId), Is.False, "not ready yet");

            board.SetReady(true);
            yield return new WaitForSeconds(0.1f);
            Assert.That(world.GetPlayer(me).IsReady, Is.True);
            Assert.That(board.StartGate(gateId), Is.True);
            yield return null;
            yield return null;

            Assert.That(world.HasGate, Is.True);
            Assert.That(hubView.IsShown, Is.False, "the hub is put away in a gate");
            Assert.That(board.IsOpen, Is.False);

            foreach (var id in world.EnemyIds) world.DamageEnemy(id, me, 100000);
            var chamber = world.Gate.Layout.Rooms[world.Gate.Layout.Rooms.Count - 1];
            world.PlacePlayer(me, new Vec3(chamber.MinX + 1f, 0f, chamber.MinZ + 1f)); // the boss room, off the portal's spot
            yield return new WaitForSeconds(0.2f);
            foreach (var id in world.EnemyIds) world.DamageEnemy(id, me, 100000);
            yield return new WaitForSeconds(0.2f);
            Assert.That(world.Gate.IsCleared, Is.True);
            Assert.That(gateView.PortalShown, Is.True, "the way home opens");
            Assert.That(_runner.HostWorld.ClearedGates, Has.Member(gateId), "and the host's world remembers the clear");

            world.LeaveGate();
            yield return new WaitForSeconds(0.1f);

            Assert.That(world.InHub, Is.True);
            yield return null;
            Assert.That(hubView.IsShown, Is.True);
        }
    }
}
