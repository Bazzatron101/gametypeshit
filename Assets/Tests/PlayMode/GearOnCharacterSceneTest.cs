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
    /// Gear shows on the character in the real scenes (ticket 34): putting a piece on draws its look on
    /// the body, swapping or taking it off changes it, and in the co-op scene the host and each bot
    /// teammate are drawn with their own gear. What is worn, and that it reaches guests, is covered
    /// headlessly (GearOnCharacterTest).
    /// </summary>
    public class GearOnCharacterSceneTest
    {
        private static void AssertDrawn(PlayerView view, GearSlot slot, string gearId)
        {
            var look = view.WornLook(slot);
            Assert.That(look, Is.Not.Null, $"{view.name} shows its {slot}");
            Assert.That(look.name, Does.EndWith(": " + gearId));
            Assert.That(look.activeInHierarchy, Is.True);
            Assert.That(look.GetComponentsInChildren<Renderer>(), Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator PuttingGearOnSwappingItAndTakingItOffChangesTheCharacter()
        {
            TestSaves.UseFreshFolder();
            yield return SceneManager.LoadSceneAsync("Greybox", LoadSceneMode.Single);
            var runner = Object.FindAnyObjectByType<SimulationRunner>();
            var view = Object.FindAnyObjectByType<PlayerView>();
            var me = view.PlayerId;
            var world = runner.World;

            world.GiveGear(me, "cinder-hood", equip: true);
            world.GiveGear(me, "iron-sword", equip: true);
            world.GiveGear(me, "cinder-boots", equip: true);
            yield return new WaitForSeconds(0.1f);

            AssertDrawn(view, GearSlot.Head, "cinder-hood");
            AssertDrawn(view, GearSlot.Weapon, "iron-sword");
            AssertDrawn(view, GearSlot.Feet, "cinder-boots");
            var head = view.WornLook(GearSlot.Head);
            Assert.That(head.transform.IsChildOf(view.transform), Is.True, "drawn on this character");
            Assert.That(head.transform.position.y, Is.GreaterThan(view.WornLook(GearSlot.Feet).transform.position.y), "the hood above the boots");

            world.GiveGear(me, "goblin-cleaver", equip: true);
            runner.RequestUnequip(me, GearSlot.Head);
            yield return new WaitForSeconds(0.1f);

            AssertDrawn(view, GearSlot.Weapon, "goblin-cleaver");
            Assert.That(view.WornLook(GearSlot.Head), Is.Null, "the hood came off");
            Assert.That(view.GetComponentsInChildren<Transform>().Count(t => t.name.Contains("iron-sword") || t.name.Contains("cinder-hood")), Is.Zero,
                "nothing left behind of what came off");
        }

        [UnityTest]
        public IEnumerator InTheCoopSceneTheHostAndEachBotTeammateWearTheirOwnGear()
        {
            TestSaves.UseFreshFolder();
            LogAssert.ignoreFailingMessages = true; // Fish-Net's inactive network managers complain in the editor; see GateSceneTest
            yield return SceneManager.LoadSceneAsync("Coop", LoadSceneMode.Single);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            var runner = Object.FindAnyObjectByType<SimulationRunner>();
            runner.Autosave.Enabled = false;
            runner.StartHost(new SimulatedNetwork(0).HostEndpoint, id => JoinPoint.For(runner.World, id, Vec3.Zero), companions: 3);
            var world = runner.World;
            var bot = world.PlayerIds.First(id => id != runner.LocalPlayerId);
            world.GiveGear(bot, "cinder-hood"); // into the bag only: the teammate puts it on itself
            yield return new WaitForSeconds(0.5f);

            var views = Object.FindObjectsByType<PlayerView>(FindObjectsSortMode.None);
            Assert.That(views.Select(v => v.PlayerId), Is.EquivalentTo(world.PlayerIds), "one view per player and teammate");
            foreach (var view in views)
            {
                var worn = world.GetPlayer(view.PlayerId).Worn;
                for (var slot = 0; slot < GearDefinition.SlotCount; slot++)
                {
                    if (worn[slot] == null) Assert.That(view.WornLook((GearSlot)slot), Is.Null, $"{view.name} shows nothing in its {(GearSlot)slot}");
                    else AssertDrawn(view, (GearSlot)slot, worn[slot].Id);
                }
            }

            Assert.That(world.GetPlayer(bot).Worn[(int)GearSlot.Head]?.Id, Is.EqualTo("cinder-hood"), "the teammate wore what it was given");
            var weapons = views.Where(v => v.PlayerId != runner.LocalPlayerId).Select(v => world.GetPlayer(v.PlayerId).Worn[(int)GearSlot.Weapon]?.Id).ToArray();
            Assert.That(weapons.Distinct().Count(), Is.GreaterThan(1), "teammates of different classes carry different weapons, so they look different");
        }
    }
}
