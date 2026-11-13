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
    /// The gate in the real co-op scene (ticket 17): hosting and entering the first gate draws its
    /// rooms and checkpoints, and a fallen solo player gets the death panel and can revive from it.
    /// The rules themselves are covered headlessly (GateRulesTest). The host here has no real
    /// network: a simulated one with nobody else on it, which is what playing solo is.
    /// </summary>
    public class GateSceneTest
    {
        private SimulationRunner _runner;

        [UnitySetUp]
        public IEnumerator EnterTheFirstGate()
        {
            TestSaves.UseFreshFolder();
            // Fish-Net's (inactive) network managers log an error about their prefab list when this scene loads in
            // the editor. The game sends its own messages and spawns no network objects, so it is noise here.
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Coop", LoadSceneMode.Single);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            _runner = Object.FindAnyObjectByType<SimulationRunner>();
            _runner.Autosave.Enabled = false;
            _runner.StartHost(new SimulatedNetwork(0).HostEndpoint, id => JoinPoint.For(_runner.World, id, Vec3.Zero));
            Assert.That(_runner.Content.Gates, Is.Not.Empty, "the content has a gate");
            _runner.World.StartGate(_runner.Content.Gates[0].Id);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheGatesRoomsAndCheckpointsAreDrawn()
        {
            var view = Object.FindAnyObjectByType<GateView>();
            Assert.That(view, Is.Not.Null, "the co-op scene needs the gate view");
            var gate = _runner.World.Gate;

            Assert.That(view.IsBuilt, Is.True);
            Assert.That(view.CheckpointCount, Is.EqualTo(gate.Layout.Checkpoints.Count));
            Assert.That(GameObject.Find($"Room {gate.Layout.Rooms.Count - 1} floor"), Is.Not.Null, "a floor for every room");
            Assert.That(GameObject.Find("Room 0 near wall"), Is.Not.Null, "the entrance is walled off behind");
            Assert.That(GameObject.Find("Room 0 far wall"), Is.Null, "the way on from a wide room into a narrow one is partly open...");
            Assert.That(GameObject.Find("Room 0 far wall (west part)"), Is.Not.Null, "...with wall either side of the opening");
            yield break;
        }

        [UnityTest]
        public IEnumerator AFallenSoloPlayerGetsThePanelAndCanReviveFromIt()
        {
            var hud = Object.FindAnyObjectByType<GateHud>();
            Assert.That(hud, Is.Not.Null, "the co-op scene needs the gate HUD");
            var me = _runner.LocalPlayerId.Value;
            Assert.That(hud.DeathPanelOpen, Is.False);

            var killer = _runner.World.SpawnEnemy(new EnemyDefinition
            {
                Name = "Killer", MaxHealth = 1, MoveSpeed = 0f, AttackDamage = 100000, AttackRange = 1f, AttackWindupSeconds = 0.05f,
            }, _runner.World.GetPlayer(me).Position);
            var deadline = Time.time + 3f;
            while (!_runner.World.GetPlayer(me).IsDefeated && Time.time < deadline) yield return null;
            _runner.World.DespawnEnemy(killer);
            yield return null;
            Assert.That(_runner.World.GetPlayer(me).IsDefeated, Is.True);
            Assert.That(hud.DeathPanelOpen, Is.True);

            hud.Respawn(); // what the panel's button and Enter do
            yield return new WaitForSeconds(0.2f);

            Assert.That(_runner.World.GetPlayer(me).IsDefeated, Is.False);
            Assert.That(_runner.World.Gate.RevivesLeft(me), Is.EqualTo(2));
            Assert.That(hud.DeathPanelOpen, Is.False);
            Assert.That(hud.CurrentNotice, Does.Contain("2 of 3"));
        }

        [UnityTest]
        public IEnumerator TheBossShowsItsHealthAndMarksEachAttackOnTheFloor()
        {
            var hud = Object.FindAnyObjectByType<GateHud>();
            var telegraphs = Object.FindAnyObjectByType<BossTelegraphView>();
            Assert.That(telegraphs, Is.Not.Null, "the co-op scene needs the boss telegraph view");
            var me = _runner.LocalPlayerId.Value;
            var gate = _runner.World.Gate;
            var chamber = gate.Layout.Rooms[gate.Layout.Rooms.Count - 1];
            _runner.World.PlacePlayer(me, new Vec3(0f, 0f, chamber.MinZ + 2f)); // into the boss chamber

            var sawTelegraph = false;
            var deadline = Time.time + 6f;
            while (!sawTelegraph && Time.time < deadline)
            {
                yield return null;
                sawTelegraph = telegraphs.ShownCount > 0;
            }

            Assert.That(hud.BossShown, Is.True, "its health bar is up");
            Assert.That(sawTelegraph, Is.True, "an attack was marked on the floor");

            var boss = default(EnemyId);
            foreach (var id in _runner.World.EnemyIds) if (_runner.World.GetEnemy(id).IsBoss) boss = id;
            _runner.World.DamageEnemy(boss, me, 100000);
            yield return new WaitForSeconds(0.2f);

            Assert.That(_runner.World.Gate.IsCleared, Is.True);
            Assert.That(hud.BossShown, Is.False);
            Assert.That(hud.CurrentNotice, Does.Contain("Reward"));
        }
    }
}
