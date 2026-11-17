using System.Collections;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The test patch played the way the developer plays it: pressing Play, offline, with no co-op launcher
    /// (playtest 2026-11-17: the mount, the gear panel and the roaming monsters did nothing there). Offline,
    /// "you" is the scene's player, so the gear panel, F2's mount and R work, and the monsters that leak
    /// from gates are drawn.
    /// </summary>
    public class OverworldOfflineSceneTest
    {
        private SimulationRunner _runner;

        [UnitySetUp]
        public IEnumerator PlayThePatchOffline()
        {
            TestSaves.UseFreshFolder(speciesId: "wombat"); // a wombat rides a bicycle, drawn as "Mount" (ticket 90)
            yield return SceneManager.LoadSceneAsync("OverworldPatch", LoadSceneMode.Single);
            yield return null;
            _runner = Object.FindAnyObjectByType<SimulationRunner>();
            _runner.Autosave.Enabled = false;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator OfflineThePlayerIsYou()
        {
            Assert.That(_runner.IsOnline, Is.False, "played as the developer plays it: offline");
            Assert.That(_runner.LocalPlayerId.HasValue, Is.True, "the gear panel, F2 and the mount messages act for this player");
            Assert.That(_runner.World.HasPlayer(_runner.LocalPlayerId.Value), Is.True);
            yield break;
        }

        [UnityTest]
        public IEnumerator OfflineAHunterGetsOnAMountInTheTown()
        {
            var me = _runner.LocalPlayerId.Value;
            _runner.World.GrantMount(me); // what F2 does
            yield return new WaitForSeconds(0.1f);
            _runner.RequestToggleMount(me); // what R does
            yield return new WaitForSeconds(0.1f);

            Assert.That(_runner.World.GetPlayer(me).IsMounted, Is.True);
            var view = Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None).First(v => v.PlayerId.Equals(me));
            var mount = view.transform.Find("Mount");
            Assert.That(mount != null && mount.gameObject.activeSelf, Is.True, "drawn on the bicycle");
        }

        [UnityTest]
        public IEnumerator OfflineTheMonstersThatLeakFromAGateAreDrawn()
        {
            var world = _runner.World;
            var me = _runner.LocalPlayerId.Value;
            var site = world.Overworld.Gates.First(g => g.GateId == "goblin-warren");
            world.PlacePlayer(me, site.Entrance + new Vec3(0f, 0f, -30f));
            yield return new WaitForSeconds(1.5f); // its tiles stream in, and the gate leaks

            Assert.That(world.EnemyIds.Count(), Is.GreaterThan(0), "the gate has leaked monsters");
            var drawn = Object.FindObjectsByType<EnemyView>(FindObjectsSortMode.None).Where(v => v.IsBound).Select(v => v.Id).ToList();
            foreach (var id in world.EnemyIds)
                Assert.That(drawn, Has.Member(id), "every roaming monster is drawn");
        }
    }
}
