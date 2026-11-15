using System.Collections;
using System.IO;
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
    /// Character saving in the real scenes (ticket 15): progress made in the greybox is there the
    /// next time the scene loads, and the test arena brings your character in but keeps nothing.
    /// The save rules themselves are covered headlessly (CharacterRecordTest, SaveFilesTest, CharacterAutosaveTest).
    /// </summary>
    public class CharacterSaveSceneTest
    {
        private static readonly PlayerId LocalPlayer = new PlayerId(1);

        private static IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
        }

        private static SimulationRunner Runner => Object.FindAnyObjectByType<SimulationRunner>();

        [UnityTest]
        public IEnumerator ProgressInTheGreyboxIsThereNextTime()
        {
            TestSaves.UseFreshFolder();
            yield return Load("Greybox");
            var runner = Runner;
            Assert.That(runner.World.GetPlayer(LocalPlayer).Level, Is.EqualTo(1), "no save: a new character");

            runner.World.GrantXp(LocalPlayer, 60);
            yield return new WaitForSeconds(0.1f);
            runner.RequestStatAllocation(LocalPlayer, Stat.Vitality);
            yield return new WaitForSeconds(1.5f); // longer than the autosave interval
            Assert.That(File.Exists(runner.Saves.FilePath), Is.True, "saved while playing, without being asked");
            var before = runner.World.GetPlayer(LocalPlayer);

            yield return Load("Greybox");

            var after = Runner.World.GetPlayer(LocalPlayer);
            Assert.That(after.Level, Is.EqualTo(before.Level).And.GreaterThan(1));
            Assert.That(after.Xp, Is.EqualTo(before.Xp));
            Assert.That(after.Stats[Stat.Vitality], Is.EqualTo(before.Stats[Stat.Vitality]));
            Assert.That(after.UnspentStatPoints, Is.EqualTo(before.UnspentStatPoints));
        }

        [UnityTest]
        public IEnumerator SkillsAndGearInTheGreyboxAreThereNextTime()
        {
            TestSaves.UseFreshFolder();
            yield return Load("Greybox");
            var runner = Runner;
            runner.World.RaiseToLevel(LocalPlayer, 5);
            runner.World.GiveGear(LocalPlayer, "cinder-staff");
            runner.World.GiveGear(LocalPlayer, "cinder-hood");
            yield return new WaitForSeconds(0.1f);
            var known = runner.World.GetPlayer(LocalPlayer).KnownSkills;
            Assert.That(known, Is.Not.Empty, "a level-5 character knows a skill");
            var lastSlot = SimulationWorld.SkillSlotCount - 1;
            runner.RequestSkillAssignment(LocalPlayer, new SkillAssignment(lastSlot, known[0].Id));
            runner.RequestEquip(LocalPlayer, "cinder-staff");
            yield return new WaitForSeconds(1.5f); // longer than the autosave interval
            var before = runner.World.GetPlayer(LocalPlayer);
            Assert.That(before.SkillSlots[lastSlot].Skill?.Id, Is.EqualTo(known[0].Id), "the skill went on the bar");
            Assert.That(before.Worn[(int)GearSlot.Weapon]?.Id, Is.EqualTo("cinder-staff"), "the staff is worn");

            yield return Load("Greybox");

            var after = Runner.World.GetPlayer(LocalPlayer);
            Assert.That(after.KnownSkills.Select(s => s.Id), Is.EqualTo(before.KnownSkills.Select(s => s.Id)));
            Assert.That(after.SkillSlots.Select(s => s.Skill?.Id), Is.EqualTo(before.SkillSlots.Select(s => s.Skill?.Id)));
            Assert.That(after.Bag.Select(g => g.Id), Is.EquivalentTo(new[] { "cinder-staff", "cinder-hood" }));
            Assert.That(after.Worn.Select(g => g?.Id), Is.EqualTo(before.Worn.Select(g => g?.Id)));
            Assert.That(after.Worn[(int)GearSlot.Weapon]?.Id, Is.EqualTo("cinder-staff"));
            Assert.That(after.Worn[(int)GearSlot.Head], Is.Null, "the hood stayed in the bag");
        }

        [UnityTest]
        public IEnumerator LeavingSavesWhatTheAutosaveHadNotYetWritten()
        {
            TestSaves.UseFreshFolder();
            yield return Load("Greybox");
            Runner.World.GrantXp(LocalPlayer, 60);
            yield return new WaitForSeconds(0.1f); // the level has landed; the autosave has just run
            Runner.World.RaiseToLevel(LocalPlayer, 3);
            yield return new WaitForSeconds(0.1f); // too soon for another autosave
            var level = Runner.World.GetPlayer(LocalPlayer).Level;

            yield return Load("Greybox"); // leaving the scene

            Assert.That(level, Is.EqualTo(3));
            Assert.That(Runner.World.GetPlayer(LocalPlayer).Level, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator TheTestArenaUsesYourCharacterButKeepsNothing()
        {
            var root = TestSaves.UseFreshFolder();
            new CharacterSaves(root).Save(new CharacterRecord { Level = 3, Xp = 10, ClassId = "warrior" });
            var saved = File.ReadAllBytes(new CharacterSaves(root).FilePath);

            yield return Load("Arena");
            var runner = Runner;
            Assert.That(runner.World.GetPlayer(LocalPlayer).Level, Is.EqualTo(3), "your character comes in");

            runner.World.GrantXp(LocalPlayer, 5000);
            yield return new WaitForSeconds(1.5f);
            Assert.That(runner.World.GetPlayer(LocalPlayer).Level, Is.GreaterThan(3));

            yield return Load("Arena");

            Assert.That(Runner.World.GetPlayer(LocalPlayer).Level, Is.EqualTo(3), "nothing from the arena was kept");
            Assert.That(File.ReadAllBytes(new CharacterSaves(root).FilePath), Is.EqualTo(saved));
        }
    }
}
