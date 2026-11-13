using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEditor;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The first gate from the real content, played start to finish by bots (ticket 17): the room
    /// kit chains into one gate that can actually be cleared, and the death rules hold up over a
    /// whole run. Headless, no network.
    /// </summary>
    public class GatePlaythroughTest
    {
        private const int TickRate = 60;

        private static GameContentAsset Content()
        {
            var assets = AssetDatabase.FindAssets("t:GameContentAsset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameContentAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();
            Assert.That(assets.Length, Is.EqualTo(1), "exactly one GameContent asset");
            return assets[0];
        }

        private static SimulationWorld WorldInFirstGate(int players, out GateDefinitionAsset gate)
        {
            var content = Content();
            Assert.That(content.Gates, Is.Not.Empty, "the content has a gate");
            gate = content.Gates[0];
            var config = new SimulationConfig();
            content.ApplyTo(config);
            var world = new SimulationWorld(config);
            for (var i = 1; i <= players; i++) world.AddPlayer(new PlayerId(i), Vec3.Zero);
            world.StartGate(gate.Id);
            return world;
        }

        /// <summary>Runs bots through the gate until it is cleared or time runs out. Returns every event.</summary>
        private static List<SimulationEvent> Play(SimulationWorld world, int minutes)
        {
            var bots = world.PlayerIds.ToDictionary(id => id, id => new BotBrain(seed: id.Value));
            var commands = new Dictionary<PlayerId, PlayerCommand>();
            var events = new List<SimulationEvent>();
            for (var tick = 0; tick < minutes * 60 * TickRate && !world.Gate.IsCleared; tick++)
            {
                foreach (var pair in bots) commands[pair.Key] = pair.Value.Next(world, pair.Key, world.Events);
                world.Step(commands);
                events.AddRange(world.Events);
            }
            return events;
        }

        private static string Summary(SimulationWorld world, List<SimulationEvent> events) =>
            $"after {world.Tick / TickRate} s: rooms cleared {events.OfType<RoomCleared>().Count()}, checkpoints {events.OfType<CheckpointActivated>().Count()}, " +
            $"player defeats {events.OfType<PlayerDefeated>().Count()}, revives {events.OfType<PlayerRevived>().Count()}, " +
            $"wipes {events.OfType<GateWiped>().Count()}, restarts {events.OfType<GateRestarted>().Count()}, attempt {world.Gate.Attempt}";

        [Test]
        public void TheKitChainsIntoTheFirstGate()
        {
            var world = WorldInFirstGate(1, out var asset);
            var layout = world.Gate.Layout;

            Assert.That(layout.Rooms.Count, Is.EqualTo(asset.Rooms.Count).And.GreaterThanOrEqualTo(4));
            Assert.That(asset.Rooms.Distinct().Count(), Is.LessThan(asset.Rooms.Count), "at least one kit piece is used more than once");
            for (var i = 1; i < layout.Rooms.Count; i++)
                Assert.That(layout.Rooms[i].MinZ, Is.EqualTo(layout.Rooms[i - 1].MaxZ), $"room {i} starts where room {i - 1} ends");
            Assert.That(layout.Checkpoints.Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void APartyOfThreeBotsClearsTheFirstGate()
        {
            var world = WorldInFirstGate(3, out _);

            var events = Play(world, minutes: 10);

            TestContext.WriteLine(Summary(world, events));
            Assert.That(world.Gate.IsCleared, Is.True, Summary(world, events));
            Assert.That(events.OfType<GateCleared>().Count(), Is.EqualTo(1));
            Assert.That(events.OfType<CheckpointActivated>().Count(), Is.EqualTo(world.Gate.Layout.Checkpoints.Count), "every checkpoint was reached on the way");
            Assert.That(events.OfType<GateRestarted>(), Is.Empty, "the solo restart rule never applies to a party");
        }

        [Test]
        public void ASoloBotGetsThroughTheFirstGateByTheSoloRules()
        {
            var world = WorldInFirstGate(1, out _);

            var events = Play(world, minutes: 20);

            TestContext.WriteLine(Summary(world, events));
            Assert.That(world.Gate.IsCleared, Is.True, "the spec: every normal gate stays solo-completable. " + Summary(world, events));
            Assert.That(events.OfType<GateWiped>(), Is.Empty, "wipes are a co-op rule");
            // Whatever happened on the way, the solo arithmetic held: never more than three revives in one attempt.
            var revivesThisAttempt = 0;
            foreach (var e in events)
            {
                if (e is GateRestarted) revivesThisAttempt = 0;
                if (e is PlayerRevived) revivesThisAttempt++;
                Assert.That(revivesThisAttempt, Is.LessThanOrEqualTo(3));
            }
        }
    }
}
