using System.Collections;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Checks the defensive wiring in the real greybox scene: the Goblin's attacks land on the
    /// player, and a dodge command reaches the simulation and dashes the capsule. Window timing
    /// is covered headlessly in SimulationDefenseTest.
    /// </summary>
    public class GreyboxDefenseTest
    {
        private static readonly PlayerId LocalPlayer = new PlayerId(1);

        private SimulationRunner _runner;
        private PlayerView _player;

        [UnitySetUp]
        public IEnumerator LoadGreybox()
        {
            TestSaves.UseFreshFolder();
            yield return SceneManager.LoadSceneAsync("Greybox", LoadSceneMode.Single);
            _runner = Object.FindAnyObjectByType<SimulationRunner>();
            _player = Object.FindAnyObjectByType<PlayerView>();
        }

        [UnityTest]
        public IEnumerator GoblinAttacksDamageAPlayerWhoStandsStill()
        {
            _runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(PlayerCommand.None));

            var deadline = Time.time + 8f;
            while (_runner.World.GetPlayer(LocalPlayer).Health == 100 && Time.time < deadline) yield return null;

            Assert.That(_runner.World.GetPlayer(LocalPlayer).Health, Is.LessThan(100));
        }

        [UnityTest]
        public IEnumerator DodgeCommandDashesTheCapsule()
        {
            _runner.SetCommandSource(LocalPlayer, new ConstantCommandSource(new PlayerCommand(1f, 0f, dodge: true)));
            var start = _player.transform.position;

            yield return new WaitForSeconds(0.3f); // longer than the 0.25 s dash

            // One dash (3 m) plus a little walking once it ends; held dodge is limited by the cooldown.
            Assert.That(_player.transform.position.x - start.x, Is.GreaterThan(2.9f));
            Assert.That(_player.transform.position.x - start.x, Is.LessThan(4f));
        }
        [UnityTest]
        public IEnumerator APerfectDodgeIsShown()
        {
            var view = Object.FindAnyObjectByType<PerfectDodgeView>();
            Assert.That(view, Is.Not.Null, "the scene needs the perfect dodge view");

            // Dodge the moment a goblin's swing is about to land: the hit falls inside the dodge's invulnerability.
            var dodger = new DodgeOnTelegraph(_runner, LocalPlayer);
            _runner.SetCommandSource(LocalPlayer, dodger);
            var deadline = Time.time + 10f;
            while (!view.BannerShown && Time.time < deadline) yield return null;

            Assert.That(view.BannerShown, Is.True, "the banner shows");
            Assert.That(GameObject.Find("Perfect dodge"), Is.Not.Null, "and a ring bursts around the player");
        }

        /// <summary>Stands still, then dodges just before any goblin's swing at this player lands.</summary>
        private sealed class DodgeOnTelegraph : IPlayerCommandSource
        {
            private readonly SimulationRunner _runner;
            private readonly PlayerId _me;
            private int _landsAt;

            public DodgeOnTelegraph(SimulationRunner runner, PlayerId me)
            {
                _runner = runner;
                _me = me;
            }

            public PlayerCommand Sample()
            {
                // A goblin close by whose swing lands within a few ticks (input is read before this frame's events exist).
                var world = _runner.World;
                var me = world.GetPlayer(_me).Position;
                foreach (var id in world.EnemyIds)
                {
                    var enemy = world.GetEnemy(id);
                    var due = enemy.AttackLandsAtTick - world.Tick;
                    if (enemy.IsDefeated || enemy.AttackLandsAtTick == 0 || due < 1 || due > 5 || enemy.AttackLandsAtTick == _landsAt) continue;
                    if (new Vec3(enemy.Position.X - me.X, 0f, enemy.Position.Z - me.Z).Length > 3f) continue;
                    _landsAt = enemy.AttackLandsAtTick; // dodge each swing once
                    return new PlayerCommand(0f, 0f, dodge: true);
                }
                return PlayerCommand.None;
            }
        }
    }
}
