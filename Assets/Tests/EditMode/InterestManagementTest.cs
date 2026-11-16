using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Interest management (ticket 41): out in the overworld each client is sent only the players, enemies,
    /// projectiles and events near its own player, plus far party members' positions now and then for a
    /// map. Whatever comes into range arrives whole; whatever leaves is gone. Prediction, late joins and
    /// the hub and gates (where everyone is together) work as before.
    /// </summary>
    public class InterestManagementTest
    {
        private const int Latency = 3;

        private static EnemyDefinition Dummy => new EnemyDefinition
        {
            Name = "Dummy", MaxHealth = 100, MoveSpeed = 0f, AttackDamage = 0, AttackRange = 0f, XpReward = 10,
        };

        private static SimulationConfig Config(bool overworld = true) => new SimulationConfig
        {
            TickRate = 60, PlayerMoveSpeed = 8f,
            Hub = new HubDefinition { Centre = new Vec3(0f, 0f, 0f) },
            Overworld = overworld ? new OverworldDefinition() : null,
            Progression = new ProgressionDefinition
            {
                LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 }, FreePointsPerLevel = 3, StartingStatValue = 10,
            },
        };

        private sealed class Rig
        {
            public readonly SimulatedNetwork Network = new SimulatedNetwork(Latency);
            public readonly HostSession Host;
            public readonly List<ClientSession> Clients = new List<ClientSession>();
            public readonly Dictionary<ClientSession, PlayerCommand> Input = new Dictionary<ClientSession, PlayerCommand>();
            public PlayerCommand HostInput;

            /// <param name="spawn">Where each player id appears.</param>
            public Rig(Func<PlayerId, Vec3> spawn, bool overworld = true, float radius = 200f)
            {
                Host = new HostSession(Config(overworld), Network.HostEndpoint, spawn, 3) { InterestRadius = radius };
                Host.AddLocalPlayer();
            }

            public ClientSession Join(string key)
            {
                var client = new ClientSession(Config(Host.World.Overworld != null), Network.Connect(), key);
                Clients.Add(client);
                Input[client] = PlayerCommand.None;
                Tick(Latency * 2 + 2);
                Assert.That(client.IsJoined, Is.True, $"test setup: {key} joined");
                return client;
            }

            public List<SimulationEvent> Tick(int times = 1, ClientSession watch = null)
            {
                var seen = new List<SimulationEvent>();
                for (var i = 0; i < times; i++)
                {
                    Network.Advance();
                    Host.Tick(HostInput);
                    foreach (var c in Clients)
                    {
                        c.Tick(Input[c]);
                        if (c == watch) seen.AddRange(c.Events);
                    }
                }
                return seen;
            }

            public Vec3 HostPosition(PlayerId id) => Host.World.GetPlayer(id).Position;
        }

        /// <summary>The host at the origin; each joiner 500 m further east.</summary>
        private static Vec3 Spread(PlayerId id) => new Vec3((id.Value - 1) * 500f, 0f, 0f);

        private static PlayerId Me(ClientSession client) => client.PlayerId.Value;

        // --- What a client is sent ----------------------------------------------------------------------

        [Test]
        public void AClientGetsOnlyThePlayersNearItAndTheFarOnesForTheMap()
        {
            var rig = new Rig(Spread);
            var near = rig.Join("near"); // 500 m east of the host
            var alsoNear = rig.Join("also"); // 1000 m east
            rig.Host.World.PlacePlayer(Me(alsoNear), new Vec3(550f, 0f, 0f)); // beside "near"
            rig.Tick(30);

            Assert.That(near.World.HasPlayer(Me(alsoNear)), Is.True, "the player beside me");
            Assert.That(near.World.HasPlayer(rig.Host.LocalPlayerId), Is.False, "not the host, 500 m away");
            rig.Tick(60);
            Assert.That(near.FarPlayers.ContainsKey(rig.Host.LocalPlayerId), Is.True, "but where the host is, for the map");
            Assert.That(near.FarPlayers[rig.Host.LocalPlayerId].X, Is.EqualTo(0f).Within(0.5f));
            Assert.That(near.FarPlayers.ContainsKey(Me(alsoNear)), Is.False, "near players are in the world, not the map list");
        }

        [Test]
        public void InTheHubOrAGateEveryoneIsSentEverything()
        {
            var rig = new Rig(Spread, overworld: false);
            var far = rig.Join("far");
            rig.Tick(10);

            Assert.That(far.World.HasPlayer(rig.Host.LocalPlayerId), Is.True);
        }

        // --- Coming into range and leaving it -----------------------------------------------------------

        [Test]
        public void AnEnemyComingIntoRangeArrivesWholeAndLeavingIsGone()
        {
            var rig = new Rig(Spread);
            var client = rig.Join("rider");
            var enemy = rig.Host.World.SpawnEnemy(Dummy, new Vec3(0f, 0f, 20f)); // by the host, 500 m from the rider
            rig.Host.World.DamageEnemy(enemy, rig.Host.LocalPlayerId, 37);
            rig.Tick(10);
            Assert.That(client.World.HasEnemy(enemy), Is.False, "out of range");

            rig.Host.World.PlacePlayer(Me(client), new Vec3(40f, 0f, 20f)); // the rider comes over
            rig.Tick(Latency * 2 + 6);
            Assert.That(client.World.HasEnemy(enemy), Is.True, "in range now");
            Assert.That(client.World.GetEnemy(enemy).Health, Is.EqualTo(63), "whole: its damage taken far away is there");
            Assert.That(client.World.GetEnemy(enemy).Position.Z, Is.EqualTo(20f).Within(1e-3f));

            rig.Host.World.PlacePlayer(Me(client), new Vec3(900f, 0f, 0f)); // and leaves again
            rig.Tick(Latency * 2 + 6);
            Assert.That(client.World.HasEnemy(enemy), Is.False, "gone from the client's world");
        }

        [Test]
        public void EventsAboutFarAwayThingsAreNotSentButOnesAboutMeAre()
        {
            var rig = new Rig(Spread);
            var client = rig.Join("away");
            var enemy = rig.Host.World.SpawnEnemy(Dummy, new Vec3(0f, 0f, 2f)); // in front of the host, far from the client
            rig.Tick(10);

            rig.Host.World.DamageEnemy(enemy, rig.Host.LocalPlayerId, 1000); // the host kills it: XP for everyone (ADR: no splitting)
            var seen = rig.Tick(Latency * 2 + 6, watch: client);

            Assert.That(seen.OfType<DamageDealt>(), Is.Empty, "a fight 500 m away is not news here");
            Assert.That(seen.OfType<EnemyDefeated>(), Is.Empty);
            Assert.That(seen.OfType<XpGained>().Any(x => x.Player == Me(client)), Is.True, "but my own XP is");
            Assert.That(seen.OfType<XpGained>().Any(x => x.Player == rig.Host.LocalPlayerId), Is.False, "and the host's is not");
        }

        // --- Prediction and joining -----------------------------------------------------------------------

        [Test]
        public void PredictionHoldsWhileOthersComeAndGoAtTheEdgeOfRange()
        {
            var rig = new Rig(id => id.Value == 1 ? new Vec3(0f, 0f, 0f) : new Vec3(150f, 0f, 0f), radius: 200f);
            var client = rig.Join("edge");
            rig.HostInput = PlayerCommand.None;

            // The client rides back and forth across the 200 m line from the host.
            for (var lap = 0; lap < 3; lap++)
            {
                rig.Input[client] = new PlayerCommand(1f, 0f);
                rig.Tick(60 * 15); // out to 270 m
                Assert.That(client.World.HasPlayer(rig.Host.LocalPlayerId), Is.False, "the host is out of range");
                rig.Input[client] = new PlayerCommand(-1f, 0f);
                rig.Tick(60 * 15); // back to 150 m
                Assert.That(client.World.HasPlayer(rig.Host.LocalPlayerId), Is.True, "and back in range");
            }
            rig.Input[client] = PlayerCommand.None;
            rig.Tick(Latency * 3);

            Assert.That(client.LargestCorrection, Is.LessThan(1e-3f), "the prediction never had to be pulled back");
            Assert.That(client.World.GetPlayer(Me(client)).Position.X, Is.EqualTo(rig.HostPosition(Me(client)).X).Within(1e-3f));
        }

        [Test]
        public void ALateJoinerFarAwayIsWelcomedWithOnlyWhatIsNearIt()
        {
            var rig = new Rig(Spread);
            for (var i = 0; i < 5; i++) rig.Host.World.SpawnEnemy(Dummy, new Vec3(0f, 0f, 10f + i)); // round the host
            rig.Tick(5);

            var joiner = rig.Join("late");

            Assert.That(joiner.World.PlayerIds, Is.EqualTo(new[] { Me(joiner) }), "only itself, 500 m from the host");
            Assert.That(joiner.World.EnemyIds, Is.Empty, "none of the enemies by the host");
            Assert.That(joiner.World.GetPlayer(Me(joiner)).Position.X, Is.EqualTo(500f).Within(1e-3f));
        }

        // --- Bandwidth --------------------------------------------------------------------------------------

        /// <summary>Ten players 500 m apart on a line, each with five enemies round them; the host's upload over ten seconds.</summary>
        private static double UploadPerSecond(float radius)
        {
            var rig = new Rig(Spread, radius: radius);
            for (var i = 0; i < 9; i++) rig.Join($"bot{i}");
            foreach (var id in rig.Host.World.PlayerIds.ToList())
            for (var e = 0; e < 5; e++)
                rig.Host.World.SpawnEnemy(Dummy, rig.HostPosition(id) + new Vec3(30f + e * 3f, 0f, 30f));
            foreach (var c in rig.Clients) rig.Input[c] = new PlayerCommand(0f, 1f); // everyone walks north
            rig.Tick(30);
            var before = rig.Host.BytesSent;
            rig.Tick(600);
            return (rig.Host.BytesSent - before) / 10.0;
        }

        [Test]
        public void MeasureTheHostsUploadWithTenPlayersSpreadAcrossTheOverworld()
        {
            var everything = UploadPerSecond(float.MaxValue);
            var near = UploadPerSecond(200f);

            TestContext.WriteLine($"Ten players 500 m apart, five enemies each: everything {everything / 1024:0} KB/s; " +
                                  $"within 200 m {near / 1024:0} KB/s ({near / everything * 100:0}% of everything)");
            Assert.That(near, Is.LessThan(everything * 0.25), "spread out, each client is sent a small share of the world");
        }
    }
}
