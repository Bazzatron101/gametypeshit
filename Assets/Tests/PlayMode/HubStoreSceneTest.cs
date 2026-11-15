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
    /// The guild's store in the real co-op scene (ticket 22): it stands in the hub, its menu opens
    /// in reach, and a sale through the menu pays the store's share in gold. The rules themselves
    /// are covered headlessly in LootTest.
    /// </summary>
    public class HubStoreSceneTest
    {
        private SimulationRunner _runner;
        private string _item;

        [UnitySetUp]
        public IEnumerator HostInTheHubCarryingADuplicate()
        {
            var root = TestSaves.UseFreshFolder();
            var content = Resources.FindObjectsOfTypeAll<GameContentAsset>().Single();
            _item = content.Gates[0].ToDefinition(new System.Collections.Generic.Dictionary<ScriptableObject, EnemyDefinition>()).RewardPool[0].Id;
            var character = new CharacterRecord { ClassId = "warrior", Name = "Tester" };
            character.Duplicates.Add(_item);
            new CharacterSaves(root).Save(character);

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
        public IEnumerator SellingADuplicateAtTheGuildStorePaysItsShare()
        {
            var panel = Object.FindAnyObjectByType<ShopPanel>();
            Assert.That(panel, Is.Not.Null, "the scene has the store menu");
            var world = _runner.World;
            var me = _runner.LocalPlayerId.Value;
            var store = world.Shops.Single(s => s.Id == "guild-exchange");
            Assert.That(world.Hub.Floor.Contains(store.Position), Is.True, "the guild's store is in the hub");
            Assert.That(panel.InReach, Is.Null, "not at the store yet");

            world.PlacePlayer(me, store.Position);
            yield return null;
            Assert.That(panel.InReach?.Id, Is.EqualTo("guild-exchange"));
            var price = world.SellPrice(store, _item);
            panel.SetOpen(true);
            panel.Sell(_item);
            yield return new WaitForSeconds(0.2f);

            Assert.That(world.GetPlayer(me).Gold, Is.EqualTo(price));
            Assert.That(world.GetPlayer(me).Duplicates, Is.Empty);
        }
    }
}
