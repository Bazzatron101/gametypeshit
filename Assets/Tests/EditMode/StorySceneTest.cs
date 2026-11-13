using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Text-only story scenes (ticket 20). The simulation decides when a scene plays, from story
    /// triggers in the content (the first arrival in the hub, the first clear of a gate), and the
    /// host's world remembers which have played, so each plays once per world. What the scene says
    /// is presentation, written by the developer.
    /// </summary>
    public class StorySceneTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);
        private static readonly PlayerId P2 = new PlayerId(2);

        private static EnemyDefinition Dummy => new EnemyDefinition { Name = "Dummy", MaxHealth = 10, MoveSpeed = 0f, AttackDamage = 0, AttackRange = 0f };

        private static GateDefinition SmallGate() => new GateDefinition
        {
            Id = "small", Name = "Small Gate",
            Rooms = { new RoomDefinition { Width = 10f, Length = 10f, Spawns = { new RoomSpawn(Dummy, new Vec3(3f, 0f, 3f)) } } },
        };

        private static SimulationConfig Config() => new SimulationConfig
        {
            TickRate = 60, Gates = new[] { SmallGate() }, Hub = new HubDefinition { Centre = new Vec3(0f, 0f, -40f) },
            StoryScenes = new[]
            {
                new StorySceneDefinition { Id = "arrival", Trigger = StoryTrigger.FirstHubArrival },
                new StorySceneDefinition { Id = "small-cleared", Trigger = StoryTrigger.GateCleared, GateId = "small" },
            },
        };

        private static List<SimulationEvent> Step(SimulationWorld world, int ticks = 1)
        {
            var all = new List<SimulationEvent>();
            for (var i = 0; i < ticks; i++)
            {
                world.Step(new Dictionary<PlayerId, PlayerCommand>());
                all.AddRange(world.Events);
            }
            return all;
        }

        private static SimulationWorld WorldInHub(int players = 1)
        {
            var world = new SimulationWorld(Config());
            for (var i = 1; i <= players; i++) world.AddPlayer(new PlayerId(i), world.HubJoinPosition(new PlayerId(i)));
            return world;
        }

        private static void ClearSmallGateAndComeHome(SimulationWorld world, List<SimulationEvent> events)
        {
            world.StartGate("small");
            world.PlacePlayer(P1, new Vec3(-3f, 0f, 2f));
            events.AddRange(Step(world));
            foreach (var id in world.EnemyIds.ToList()) world.DamageEnemy(id, P1, 1000);
            events.AddRange(Step(world));
            world.LeaveGate();
            events.AddRange(Step(world));
        }

        [Test]
        public void TheArrivalScenePlaysTheFirstTimeTheWorldReachesTheHubAndNeverAgain()
        {
            var world = WorldInHub();

            var first = Step(world, 5);
            var events = new List<SimulationEvent>();
            ClearSmallGateAndComeHome(world, events);
            events.AddRange(Step(world, 5));

            Assert.That(first.OfType<StorySceneStarted>().Select(s => s.SceneId), Is.EqualTo(new[] { "arrival" }), "once, at the first arrival");
            Assert.That(events.OfType<StorySceneStarted>().Count(s => s.SceneId == "arrival"), Is.Zero, "not on coming home");
            Assert.That(world.SeenStoryScenes, Has.Member("arrival"));
        }

        [Test]
        public void AGateClearSceneFollowsTheFirstClearOfThatGateOnly()
        {
            var world = WorldInHub();
            Step(world);
            var events = new List<SimulationEvent>();

            ClearSmallGateAndComeHome(world, events);
            ClearSmallGateAndComeHome(world, events);

            Assert.That(events.OfType<StorySceneStarted>().Count(s => s.SceneId == "small-cleared"), Is.EqualTo(1));
        }

        [Test]
        public void AGateClearSceneWaitsUntilThePartyIsHome()
        {
            var world = WorldInHub();
            Step(world);
            world.StartGate("small");
            world.PlacePlayer(P1, new Vec3(-3f, 0f, 2f));
            Step(world);
            foreach (var id in world.EnemyIds.ToList()) world.DamageEnemy(id, P1, 1000);
            var inGate = Step(world, 5);
            Assert.That(world.HasGate && world.Gate.IsCleared, Is.True);
            Assert.That(inGate.OfType<StorySceneStarted>(), Is.Empty, "not in the gate");

            world.LeaveGate();
            var home = Step(world, 2);

            Assert.That(home.OfType<StorySceneStarted>().Select(e => e.SceneId), Is.EqualTo(new[] { "small-cleared" }));
        }

        [Test]
        public void AWorldThatHasSeenASceneDoesNotPlayItAgain()
        {
            // The host's world loads what it has seen from its save before anyone arrives.
            var world = new SimulationWorld(Config());
            world.SetSeenStoryScenes(new[] { "arrival" });
            world.AddPlayer(P1, world.HubJoinPosition(P1));

            Assert.That(Step(world, 5).OfType<StorySceneStarted>(), Is.Empty);
        }

        [Test]
        public void ScenesWaitForSomeoneToBePresent()
        {
            var world = new SimulationWorld(Config());

            Assert.That(Step(world, 5).OfType<StorySceneStarted>(), Is.Empty, "an empty hub tells no story");
            world.AddPlayer(P1, world.HubJoinPosition(P1));
            Assert.That(Step(world).OfType<StorySceneStarted>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void WhatHasBeenSeenTravelsToGuests()
        {
            var world = WorldInHub(players: 2);
            Step(world);

            var guest = SimulationWorld.FromSnapshot(Config(), world.CaptureSnapshot());

            Assert.That(guest.SeenStoryScenes, Is.EquivalentTo(new[] { "arrival" }));
            Assert.That(Step(guest, 5).OfType<StorySceneStarted>(), Is.Empty, "a guest's prediction does not play it a second time");
        }

        [Test]
        public void WithoutScenesNothingHappens()
        {
            var world = new SimulationWorld(new SimulationConfig { Hub = new HubDefinition() });
            world.AddPlayer(P1, world.HubJoinPosition(P1));

            Assert.That(Step(world, 5).OfType<StorySceneStarted>(), Is.Empty);
        }

        // --- The host's world save ------------------------------------------------------------------

        [Test]
        public void TheHostsWorldSaveKeepsWhichScenesHavePlayed()
        {
            var root = Path.Combine(Path.GetTempPath(), "hunter-story-" + Guid.NewGuid().ToString("N"));
            try
            {
                var saves = new WorldSaves(root);
                var world = saves.LoadOrNew();
                world.ClearedGates.Add("small");
                world.SeenStoryScenes.Add("arrival");
                saves.Save(world);

                var loaded = new WorldSaves(root).LoadOrNew();

                Assert.That(loaded.ClearedGates, Is.EqualTo(new[] { "small" }));
                Assert.That(loaded.SeenStoryScenes, Is.EqualTo(new[] { "arrival" }));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void AnOlderWorldSaveWithoutScenesStillLoads()
        {
            var root = Path.Combine(Path.GetTempPath(), "hunter-story-" + Guid.NewGuid().ToString("N"));
            try
            {
                // Format 1, as ticket 15 wrote it: magic, version 1, cleared gates.
                var path = new WorldSaves(root).FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var w = new BinaryWriter(File.Create(path)))
                {
                    w.Write(System.Text.Encoding.ASCII.GetBytes("HSWD"));
                    w.Write((byte)1);
                    w.Write(1);
                    w.Write("small");
                }

                var loaded = new WorldSaves(root).LoadOrNew();

                Assert.That(loaded.ClearedGates, Is.EqualTo(new[] { "small" }));
                Assert.That(loaded.SeenStoryScenes, Is.Empty);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }
    }
}
