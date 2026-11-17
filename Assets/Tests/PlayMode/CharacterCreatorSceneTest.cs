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
    /// The character creator in the real scenes (ticket 28): a new hunter is named and dressed
    /// before the class is picked and before play starts; the choice is saved; looks change in the
    /// hub; and other hunters' names float above them. The rules are covered headlessly in CharacterCreatorTest.
    /// </summary>
    public class CharacterCreatorSceneTest
    {
        private static readonly PlayerId LocalPlayer = new PlayerId(1);
        private static readonly CharacterLook RedBroad = new CharacterLook(1, Build.Broad);

        [UnityTest]
        public IEnumerator ANewHunterIsNamedAndDressedThenPicksAClass()
        {
            var saves = TestSaves.UseFreshFolder(classId: null, name: null);
            yield return SceneManager.LoadSceneAsync("Greybox", LoadSceneMode.Single);
            var runner = Object.FindAnyObjectByType<SimulationRunner>();
            var creator = Object.FindAnyObjectByType<CharacterCreator>();
            var picker = Object.FindAnyObjectByType<ClassPicker>();
            Assert.That(creator, Is.Not.Null, "every scene with a runner has the creator");
            Assert.That(creator.Showing, Is.EqualTo(CharacterCreator.Mode.Create));
            Assert.That(picker.IsOpen, Is.False, "the class comes after the name and looks");
            var tick = runner.World.Tick;
            yield return null;
            yield return null;
            Assert.That(runner.World.Tick, Is.EqualTo(tick), "the world waits for the hunter to be made");

            creator.Create("  Ayla  ", RedBroad);
            yield return null;

            Assert.That(creator.IsOpen, Is.False);
            var species = Object.FindAnyObjectByType<SpeciesPicker>();
            Assert.That(species.IsOpen, Is.True, "then the species (ticket 81)");
            Assert.That(picker.IsOpen, Is.False, "the class comes after the species");
            Assert.That(new CharacterSaves(saves).LoadOrNew().Name, Is.EqualTo("Ayla"), "saved at once");

            species.Choose("wombat");
            yield return null;
            Assert.That(species.IsOpen, Is.False);
            Assert.That(picker.IsOpen, Is.True, "then the class");
            Assert.That(new CharacterSaves(saves).LoadOrNew().SpeciesId, Is.EqualTo("wombat"), "saved at once");
            Assert.That(runner.World.Tick, Is.EqualTo(tick), "still waiting, for the class");

            picker.Choose("warrior");
            yield return new WaitForSeconds(0.2f);

            var me = runner.World.GetPlayer(LocalPlayer);
            Assert.That(me.Name, Is.EqualTo("Ayla"));
            Assert.That(me.Look, Is.EqualTo(RedBroad));
            Assert.That(me.Class.Id, Is.EqualTo("warrior"));
            Assert.That(me.Species.Id, Is.EqualTo("wombat"));
            Assert.That(runner.World.Tick, Is.GreaterThan(tick), "and play starts");

            yield return SceneManager.LoadSceneAsync("Greybox", LoadSceneMode.Single);

            var again = Object.FindAnyObjectByType<SimulationRunner>().World.GetPlayer(LocalPlayer);
            Assert.That(Object.FindAnyObjectByType<CharacterCreator>().IsOpen, Is.False, "a made hunter is not asked again");
            Assert.That(again.Name, Is.EqualTo("Ayla"));
            Assert.That(again.Look, Is.EqualTo(RedBroad));
        }

        [UnityTest]
        public IEnumerator AHunterMadeBeforeNamesIsAskedForOneAndKeepsTheirProgress()
        {
            var root = TestSaves.UseFreshFolder(classId: null, name: null);
            new CharacterSaves(root).Save(new CharacterRecord { Level = 4, ClassId = "archer" });
            yield return SceneManager.LoadSceneAsync("Greybox", LoadSceneMode.Single);
            var creator = Object.FindAnyObjectByType<CharacterCreator>();
            Assert.That(creator.Showing, Is.EqualTo(CharacterCreator.Mode.Create));

            creator.Create("Brann", default);
            yield return null;
            var species = Object.FindAnyObjectByType<SpeciesPicker>();
            Assert.That(species.IsOpen, Is.True, "a hunter from before species is asked for one once (ticket 81)");
            species.Choose("platypus");
            yield return new WaitForSeconds(0.2f);

            var me = Object.FindAnyObjectByType<SimulationRunner>().World.GetPlayer(LocalPlayer);
            Assert.That(me.Species.Id, Is.EqualTo("platypus"));
            Assert.That(Object.FindAnyObjectByType<ClassPicker>().IsOpen, Is.False, "the class it had stays");
            Assert.That(me.Name, Is.EqualTo("Brann"));
            Assert.That(me.Level, Is.EqualTo(4));
            Assert.That(me.Class.Id, Is.EqualTo("archer"));
        }

        [UnityTest]
        public IEnumerator InTheHubLooksChangeAndTeammatesNamesShow()
        {
            TestSaves.UseFreshFolder(name: "Ayla");
            LogAssert.ignoreFailingMessages = true; // Fish-Net's inactive network managers complain in the editor; see GateSceneTest
            yield return SceneManager.LoadSceneAsync("Coop", LoadSceneMode.Single);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            var runner = Object.FindAnyObjectByType<SimulationRunner>();
            runner.Autosave.Enabled = false;
            runner.StartHost(new SimulatedNetwork(0).HostEndpoint, id => JoinPoint.For(runner.World, id, Vec3.Zero), companions: 2);
            yield return null;
            yield return null;
            var me = runner.LocalPlayerId.Value;
            var creator = Object.FindAnyObjectByType<CharacterCreator>();
            Assert.That(runner.World.InHub, Is.True);
            Assert.That(creator.CanRestyle(), Is.True);

            creator.OpenRestyle();
            Assert.That(creator.Showing, Is.EqualTo(CharacterCreator.Mode.Restyle));
            creator.Restyle(RedBroad);
            yield return new WaitForSeconds(0.2f);

            Assert.That(creator.IsOpen, Is.False);
            Assert.That(runner.World.GetPlayer(me).Look, Is.EqualTo(RedBroad));
            Assert.That(runner.World.GetPlayer(me).Name, Is.EqualTo("Ayla"), "the name stays");

            var plates = Object.FindAnyObjectByType<Nameplates>();
            var teammates = runner.World.PlayerIds.Where(id => id != me).ToList();
            Assert.That(teammates.Count, Is.EqualTo(2));
            Assert.That(plates.Shown.Select(s => s.id), Is.EquivalentTo(teammates), "the bot teammates' names show; yours does not");
            Assert.That(plates.Shown.Select(s => s.name), Has.None.Null.And.None.Empty);
        }
    }
}
