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
    /// Ten players, all bots, do the whole loop through the hub (ticket 19): they gather in the hub,
    /// mark themselves ready, the host takes them into the first gate, they clear it, and the portal
    /// brings them home. A host bot plus nine bot clients over the simulated network, with the real
    /// content: the ten-bot check that the hub holds a full party.
    /// </summary>
    public class HubRoundTripTest
    {
        private const int Latency = 6;

        private static SimulationConfig ContentConfig(out GameContentAsset content)
        {
            content = AssetDatabase.FindAssets("t:GameContentAsset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameContentAsset>(AssetDatabase.GUIDToAssetPath(guid))).Single();
            var config = new SimulationConfig();
            content.ApplyTo(config);
            return config;
        }

        [Test]
        public void TenBotsGatherReadyUpClearTheFirstGateAndComeHome()
        {
            var config = ContentConfig(out var content);
            Assert.That(config.Hub, Is.Not.Null, "the content has a hub");
            var gateId = content.Gates[0].Id;
            var network = new SimulatedNetwork(Latency);
            HostSession host = null;
            host = new HostSession(config, network.HostEndpoint, id => JoinPoint.For(host.World, id, Vec3.Zero));
            var hostId = host.AddLocalPlayer();
            var hostBot = new BotBrain(seed: hostId.Value);
            var clients = Enumerable.Range(0, HostSession.MaxPlayers - 1)
                .Select(i => (session: new ClientSession(ContentConfig(out _), network.Connect(), $"bot-{i}"), bot: new BotBrain(seed: i + 2)))
                .ToList();

            var started = -1;
            var home = (ReturnedToHub)null;
            var players = 0;
            var readyWhenStarted = 0;
            for (var tick = 0; tick < 60 * 60 * 5 && home == null; tick++)
            {
                network.Advance();
                // The host waits for the whole party (ten players) to gather and ready up.
                var everyoneHere = host.World.PlayerIds.Count == HostSession.MaxPlayers && clients.All(c => c.session.IsJoined);
                if (started < 0 && everyoneHere && host.World.TryStartGateFromHub(gateId, out _))
                {
                    started = tick;
                    readyWhenStarted = host.World.PlayerIds.Count;
                }
                host.Tick(hostBot.Next(host.World, hostId, host.World.Events));
                home = host.World.Events.OfType<ReturnedToHub>().FirstOrDefault();
                foreach (var (session, bot) in clients)
                    session.Tick(session.IsJoined ? bot.Next(session.World, session.PlayerId.Value, session.Events) : PlayerCommand.None);
                players = System.Math.Max(players, host.World.PlayerIds.Count);
            }
            for (var i = 0; i < Latency * 3; i++)
            {
                network.Advance();
                host.Tick(PlayerCommand.None);
                foreach (var (session, _) in clients) session.Tick(PlayerCommand.None);
            }
            TestContext.WriteLine($"all ten gathered and ready, gate started at {started / 60f:0.0} s, home at {host.World.Tick / 60f:0.0} s");

            Assert.That(players, Is.EqualTo(HostSession.MaxPlayers), "a full party gathered in the hub");
            Assert.That(started, Is.GreaterThanOrEqualTo(0), "everyone readied up and the host took them in");
            Assert.That(readyWhenStarted, Is.EqualTo(HostSession.MaxPlayers), "all ten, from the hub");
            Assert.That(home, Is.Not.Null, "they came home");
            Assert.That((home.GateId, home.Cleared), Is.EqualTo((gateId, true)));
            Assert.That(host.World.InHub, Is.True);
            foreach (var (session, _) in clients) Assert.That(session.World.InHub, Is.True, "every guest is home too");
            foreach (var id in host.World.PlayerIds)
                Assert.That(host.World.GetPlayer(id).ActiveModifiers, Is.Not.Empty, $"player {id.Value} took a reward home");
        }
    }
}
