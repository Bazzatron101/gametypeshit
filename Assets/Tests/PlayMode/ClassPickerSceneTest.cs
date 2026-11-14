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
    /// The class picker in the real greybox scene: a character with no class is asked for one
    /// before play starts, and the pick gives that class's skills and is saved at once.
    /// </summary>
    public class ClassPickerSceneTest
    {
        private static readonly PlayerId LocalPlayer = new PlayerId(1);

        private SimulationRunner _runner;
        private string _saves;

        [UnitySetUp]
        public IEnumerator LoadGreyboxWithANewCharacter()
        {
            _saves = TestSaves.UseFreshFolder(classId: null);
            yield return SceneManager.LoadSceneAsync("Greybox", LoadSceneMode.Single);
            _runner = Object.FindAnyObjectByType<SimulationRunner>();
        }

        [UnityTest]
        public IEnumerator ANewCharacterPicksAClassBeforePlaying()
        {
            var picker = Object.FindAnyObjectByType<ClassPicker>();
            Assert.That(picker, Is.Not.Null, "the scene has the class picker");
            Assert.That(picker.IsOpen, Is.True);
            var tick = _runner.World.Tick;
            yield return null;
            yield return null;
            Assert.That(_runner.World.Tick, Is.EqualTo(tick), "the world waits for the pick");

            picker.Choose("wizard");
            yield return new WaitForSeconds(0.2f);

            var player = _runner.World.GetPlayer(LocalPlayer);
            Assert.That(picker.IsOpen, Is.False);
            Assert.That(player.Class.Id, Is.EqualTo("wizard"));
            Assert.That(player.KnownSkills.Select(s => s.Name), Is.EqualTo(new[] { "Fireball" }), "a wizard starts with just its Fireball");
            Assert.That(new CharacterSaves(_saves).LoadOrNew().ClassId, Is.EqualTo("wizard"), "saved at once");
            Assert.That(_runner.World.Tick, Is.GreaterThan(tick), "and play starts");
        }
    }
}
