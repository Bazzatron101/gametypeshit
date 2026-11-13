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
    /// The first story scene in the real co-op scene (ticket 20): hosting a new world brings the
    /// party to the hub, the arrival scene plays as text, and reading through it ends it. Hosting
    /// that world again does not play it a second time.
    /// </summary>
    public class StorySceneSceneTest
    {

        private static IEnumerator HostInCoop(string saveFolder)
        {
            GameSaves.Root = saveFolder;
            LogAssert.ignoreFailingMessages = true; // Fish-Net's inactive network managers complain in the editor; see GateSceneTest
            yield return SceneManager.LoadSceneAsync("Coop", LoadSceneMode.Single);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
            var runner = Object.FindAnyObjectByType<SimulationRunner>();
            runner.StartHost(new SimulatedNetwork(0).HostEndpoint, id => JoinPoint.For(runner.World, id, Vec3.Zero));
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTest]
        public IEnumerator TheArrivalScenePlaysOnceForANewWorldAndReadsThrough()
        {
            var folder = TestSaves.UseFreshFolder();
            yield return HostInCoop(folder);
            var player = Object.FindAnyObjectByType<TextScenePlayer>();
            Assert.That(player, Is.Not.Null, "the co-op scene needs the text scene player");
            Assert.That(player.Playing, Is.Not.Null, "the arrival scene is playing");
            Assert.That(player.Playing.Id, Is.EqualTo("arrival"));

            var lines = player.Playing.Lines.Count;
            for (var i = 0; i < lines * 2 + 2 && player.Playing != null; i++)
            {
                player.Advance(); // reveals the rest of the line, then moves on
                yield return null;
            }
            Assert.That(player.Playing, Is.Null, "read to the end");

            // Leave, and host the same world again: the host's world remembers it has played.
            Object.FindAnyObjectByType<SimulationRunner>().StopOnline();
            yield return HostInCoop(folder);
            Assert.That(Object.FindAnyObjectByType<TextScenePlayer>().Playing, Is.Null, "once per world");
        }
    }
}
