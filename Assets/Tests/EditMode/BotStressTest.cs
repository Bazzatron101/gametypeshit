using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using HunterSystem.Game;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEditor;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Ten players, all bots, in one session with the game's real content (the M1 slice's Goblins and
    /// Hobgoblin): the host's own player plus nine bot clients over a simulated network with 100 ms
    /// each way. It checks the session holds together and records host and client tick times and
    /// bandwidth to TestResults/bot-stress.md for ticket 14's write-up.
    /// </summary>
    public class BotStressTest
    {
        private const int Latency = 6;          // ticks each way: 100 ms at 60 Hz
        private const int Seconds = 30;
        private const int BotClients = HostSession.MaxPlayers - 1;

        /// <summary>
        /// Bots are asked once a frame in the game, and frames outnumber ticks; they go through the
        /// game's own per-frame buffer here too, as at 120 fps, so frame-versus-tick mistakes show.
        /// </summary>
        private const int FramesPerTick = 2;

        private static SimulationConfig ContentConfig(out GameContentAsset content)
        {
            var assets = AssetDatabase.FindAssets("t:GameContentAsset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameContentAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();
            Assert.That(assets.Length, Is.EqualTo(1), "exactly one GameContent asset");
            content = assets[0];
            var config = new SimulationConfig();
            content.ApplyTo(config);
            return config;
        }

        [Test]
        public void TenBotsFightTogetherInOneSession()
        {
            var config = ContentConfig(out var content);
            var goblin = content.Enemies.First(e => e != null && e.name == "Goblin").ToDefinition();
            var hobgoblin = content.Enemies.First(e => e != null && e.name == "Hobgoblin").ToDefinition();

            var network = new SimulatedNetwork(Latency);
            HostSession host = null;
            host = new HostSession(config, network.HostEndpoint, id => JoinPoint.BesideParty(host.World, id, Vec3.Zero));
            var hostId = host.AddLocalPlayer();
            var hostBot = new BotBrain(seed: hostId.Value);
            var hostBuffer = new PlayerCommandBuffer();

            var clients = new List<(ClientSession session, BotBrain bot, PlayerCommandBuffer buffer)>();
            for (var i = 0; i < BotClients; i++)
                clients.Add((new ClientSession(ContentConfig(out _), network.Connect(), $"bot-{i}"), new BotBrain(seed: i + 2), new PlayerCommandBuffer()));

            var hostTicks = new List<double>();
            var clientTicks = new List<double>();
            var snapshotSizes = new List<int>();
            var damageBy = new Dictionary<PlayerId, int>();
            var leveledUp = new HashSet<PlayerId>();
            var spentPoints = new HashSet<PlayerId>();
            var defeatedEnemies = 0;
            var playerDefeats = 0;
            var topUp = new EnemyTopUp(goblin, hobgoblin);
            long bytesSentWhenFull = -1;
            const int FullFromTick = 120; // every client has joined well before 2 s
            var clock = new Stopwatch();

            var ticks = Seconds * config.TickRate;
            for (var tick = 0; tick < ticks; tick++)
            {
                network.Advance();

                if (tick % 30 == 0) topUp.Apply(host.World, host.World.GetPlayer(hostId).Position);
                if (tick == FullFromTick) bytesSentWhenFull = host.BytesSent;

                for (var f = 0; f < FramesPerTick; f++) hostBuffer.Add(hostBot.Next(host.World, hostId, host.World.Events));
                var hostCommand = hostBuffer.TakeForTick();
                clock.Restart();
                host.Tick(hostCommand);
                hostTicks.Add(clock.Elapsed.TotalMilliseconds);
                if (tick % 3 == 2) snapshotSizes.Add(host.LastSnapshotSize);

                foreach (var e in host.World.Events)
                {
                    if (e is DamageDealt d) damageBy[d.Attacker] = (damageBy.TryGetValue(d.Attacker, out var sum) ? sum : 0) + d.Amount;
                    if (e is EnemyDefeated) defeatedEnemies++;
                    if (e is PlayerDefeated) playerDefeats++;
                    if (e is PlayerLeveledUp l) leveledUp.Add(l.Player);
                    if (e is StatPointAllocated a) spentPoints.Add(a.Player);
                }

                foreach (var (session, bot, buffer) in clients)
                {
                    for (var f = 0; f < FramesPerTick && session.IsJoined; f++) buffer.Add(bot.Next(session.World, session.PlayerId.Value, session.Events));
                    var command = buffer.TakeForTick();
                    clock.Restart();
                    session.Tick(command);
                    clientTicks.Add(clock.Elapsed.TotalMilliseconds);
                }
            }

            var report = Report(host, clients.Select(c => c.session).ToList(), hostTicks, clientTicks, snapshotSizes, damageBy,
                defeatedEnemies, playerDefeats, topUp.Spawned, (host.BytesSent - bytesSentWhenFull) / (double)(Seconds - FullFromTick / 60));
            TestContext.WriteLine(report);
            var path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "TestResults", "bot-stress.md"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report);

            Assert.That(host.World.PlayerIds.Count, Is.EqualTo(HostSession.MaxPlayers), "a full session");
            Assert.That(clients.All(c => c.session.IsJoined), Is.True, "every bot joined through the normal path");
            Assert.That(host.World.PlayerIds.All(id => damageBy.ContainsKey(id)), Is.True, "every one of the ten fought");
            Assert.That(defeatedEnemies, Is.GreaterThanOrEqualTo(20), "and they got through a lot of enemies");
            Assert.That(leveledUp, Is.Not.Empty, "the fight was long enough to level up");
            Assert.That(spentPoints, Is.SupersetOf(leveledUp), "every bot that levelled up spent its points");
            Assert.That(clients.Max(c => c.session.LargestCorrection), Is.LessThan(0.5f), "reconciliation: predictions stayed close to the host");
            Assert.That(host.MalformedMessages, Is.Zero);
            Assert.That(clients.Sum(c => c.session.UnreadableEventLists), Is.Zero);

            // Guards against getting worse, not targets: a 60 Hz frame is 16.7 ms (and the host also renders),
            // and full snapshots measured about 92 KB/s per client when this was written (ticket 14's write-up).
            Assert.That(hostTicks.Average(), Is.LessThan(4.0), "average host tick (ms)");
            var perClientBytesPerSecond = (host.BytesSent - bytesSentWhenFull) / (double)(Seconds - FullFromTick / 60) / BotClients;
            Assert.That(perClientBytesPerSecond, Is.LessThan(128 * 1024), "host upload per client (bytes/s)");
        }

        /// <summary>Snapshot size for an empty world, per player and per enemy, and what Deflate would make of the session's own snapshot.</summary>
        private static string SnapshotBreakdown(HostSession host)
        {
            var config = ContentConfig(out var content);
            var goblin = content.Enemies.First(e => e != null && e.name == "Goblin").ToDefinition();
            int Size(int players, int enemies)
            {
                var world = new SimulationWorld(config);
                for (var i = 1; i <= players; i++) world.AddPlayer(new PlayerId(i), new Vec3(i, 0f, 0f));
                for (var i = 0; i < enemies; i++) world.SpawnEnemy(goblin, new Vec3(i, 0f, 5f));
                return world.CaptureSnapshot().Length;
            }

            var empty = Size(0, 0);
            var perPlayer = (Size(10, 0) - empty) / 10.0;
            var perEnemy = (Size(0, 10) - empty) / 10.0;
            var live = host.World.CaptureSnapshot();
            var compressed = Deflate(live);
            return FormattableString.Invariant(
                $"| Part | Bytes |\n|---|---|\n| Empty world | {empty} |\n| Each player (fresh) | {perPlayer:0} |\n| Each enemy | {perEnemy:0} |\n") +
                FormattableString.Invariant($"| This session's last world snapshot | {live.Length} |\n| ...compressed with Deflate | {compressed} ({100.0 * compressed / live.Length:0}%) |");
        }

        private static int Deflate(byte[] bytes)
        {
            using var output = new MemoryStream();
            using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true)) deflate.Write(bytes, 0, bytes.Length);
            return (int)output.Length;
        }

        private static double Percentile(List<double> values, double p)
        {
            var sorted = values.OrderBy(v => v).ToList();
            return sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(p * sorted.Count) - 1)];
        }

        private static string Report(HostSession host, List<ClientSession> clients, List<double> hostTicks, List<double> clientTicks,
            List<int> snapshotSizes, Dictionary<PlayerId, int> damageBy, int defeatedEnemies, int playerDefeats, int spawned, double uploadBytesPerSecond)
        {
            var b = new StringBuilder();
            b.AppendLine($"# Bot stress test (headless, {DateTime.Now:yyyy-MM-dd HH:mm})");
            b.AppendLine();
            b.AppendLine($"{host.World.PlayerIds.Count} players (host bot + {clients.Count} bot clients), {Seconds} s at 60 Hz, " +
                         $"{Latency * 1000 / 60} ms each way, real content. {spawned} enemies spawned, {defeatedEnemies} defeated, {playerDefeats} player defeats.");
            b.AppendLine();
            b.AppendLine("| Measure | Value |");
            b.AppendLine("|---|---|");
            var steadyHost = hostTicks.Skip(60).ToList();
            var steadyClient = clientTicks.Skip(60 * clients.Count).ToList();
            b.AppendLine(FormattableString.Invariant($"| Host tick, mean / p99 / max | {hostTicks.Average():0.000} / {Percentile(hostTicks, 0.99):0.000} / {hostTicks.Max():0.000} ms |"));
            b.AppendLine(FormattableString.Invariant($"| Host tick after the first second, p99 / max | {Percentile(steadyHost, 0.99):0.000} / {steadyHost.Max():0.000} ms |"));
            b.AppendLine(FormattableString.Invariant($"| Client tick (predict + replay), mean / p99 / max | {clientTicks.Average():0.000} / {Percentile(clientTicks, 0.99):0.000} / {clientTicks.Max():0.000} ms |"));
            b.AppendLine(FormattableString.Invariant($"| Client tick after the first second, p99 / max | {Percentile(steadyClient, 0.99):0.000} / {steadyClient.Max():0.000} ms |"));
            b.AppendLine(FormattableString.Invariant($"| Snapshot size, mean / max | {snapshotSizes.Average():0} / {snapshotSizes.Max()} bytes |"));
            b.AppendLine(FormattableString.Invariant($"| Host upload with all ten in, total | {uploadBytesPerSecond / 1024:0.0} KB/s |"));
            b.AppendLine(FormattableString.Invariant($"| Host upload with all ten in, per client | {uploadBytesPerSecond / clients.Count / 1024:0.0} KB/s |"));
            b.AppendLine(FormattableString.Invariant($"| Host download, total | {host.BytesReceived / (double)Seconds / 1024:0.0} KB/s |"));
            b.AppendLine(FormattableString.Invariant($"| Largest client correction | {clients.Max(c => c.LargestCorrection):0.000} m |"));
            b.AppendLine(FormattableString.Invariant($"| Unconfirmed commands (round trip), max at end | {clients.Max(c => c.UnconfirmedCommands)} |"));
            b.AppendLine();
            b.AppendLine("Where the snapshot bytes go (same content, worlds built for the measurement):");
            b.AppendLine();
            b.AppendLine(SnapshotBreakdown(host));
            b.AppendLine();
            b.AppendLine("Damage dealt per player: " + string.Join(", ", damageBy.OrderBy(p => p.Key.Value).Select(p => $"{p.Key.Value}: {p.Value}")));
            return b.ToString();
        }
    }
}
