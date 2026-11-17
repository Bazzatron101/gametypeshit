using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    /// The first raid with ten bots (ticket 29): a host bot and nine bot clients over the simulated network
    /// (100 ms each way) gather in the hub, ready up and go into the Goblin King's raid with the real content.
    /// They split into groups at the four crystals and break them together, shelter from the blasts, beat the
    /// King and come home. Host tick time and upload are measured from the raid's start to the clear and checked
    /// against the guards ticket 14 and ticket 31 recorded; the numbers go to TestResults/raid-bots.md.
    /// </summary>
    public class RaidBotTest
    {
        private const int Latency = 6;
        private const string RaidId = "goblin-king-raid";

        private static SimulationConfig ContentConfig()
        {
            var content = AssetDatabase.FindAssets("t:GameContentAsset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameContentAsset>(AssetDatabase.GUIDToAssetPath(guid))).Single();
            var config = new SimulationConfig();
            content.ApplyTo(config);
            return config;
        }

        [Test]
        public void TenBotsSplitForTheCrystalsShelterTogetherAndBeatTheGoblinKing()
        {
            var config = ContentConfig();
            Assert.That(config.Gates.Any(g => g.Id == RaidId && g.Raid != null), "the content has the raid");
            var network = new SimulatedNetwork(Latency);
            HostSession host = null;
            host = new HostSession(config, network.HostEndpoint, id => JoinPoint.For(host.World, id, Vec3.Zero));
            var hostId = host.AddLocalPlayer();
            var hostBot = new BotBrain(seed: hostId.Value);
            var clients = Enumerable.Range(0, HostSession.MaxPlayers - 1)
                .Select(i => (session: new ClientSession(ContentConfig(), network.Connect(), $"bot-{i}"), bot: new BotBrain(seed: i + 2)))
                .ToList();

            var clock = new Stopwatch();
            var hostTicks = new List<double>();
            var started = -1;
            var bytesAtStart = 0L;
            var bytesAtClear = -1L;
            var clearedAt = -1;
            ReturnedToHub home = null;
            RaidShieldRaised shield = null;
            var shieldBrokenAt = -1;
            var regrows = 0;
            var finalAt = -1;
            var shelters = 0;
            var exposures = 0;
            var caught = 0;
            var playerDefeats = 0;
            var crystalHitters = new Dictionary<EnemyId, HashSet<PlayerId>>();
            var crystalBrokenAt = new Dictionary<EnemyId, int>();
            var fightCorrection = 0f; // after everyone has been put at the entrance (a host placement, not a misprediction) until the clear

            for (var tick = 0; tick < 60 * 60 * 8 && home == null; tick++)
            {
                network.Advance();
                var everyoneHere = host.World.PlayerIds.Count == HostSession.MaxPlayers && clients.All(c => c.session.IsJoined);
                if (started < 0 && everyoneHere && host.World.TryStartGateFromHub(RaidId, out _))
                {
                    started = tick;
                    bytesAtStart = host.BytesSent;
                }

                var command = hostBot.Next(host.World, hostId, host.World.Events);
                clock.Restart();
                host.Tick(command);
                if (started >= 0 && clearedAt < 0) hostTicks.Add(clock.Elapsed.TotalMilliseconds);

                var raid = host.World.HasGate ? host.World.Gate.Raid : null;
                foreach (var e in host.World.Events)
                {
                    switch (e)
                    {
                        case RaidShieldRaised r: shield = r; break;
                        case RaidCrystalsRegrew _: regrows++; break;
                        case RaidShieldBroken _: shieldBrokenAt = tick; break;
                        case RaidFinalPhase _: finalAt = tick; break;
                        case RaidShelterMarked _: shelters++; break;
                        case RaidBlastLanded b:
                            caught += b.Caught;
                            exposures += host.World.PlayerIds.Count(id => !host.World.GetPlayer(id).IsAway);
                            break;
                        case PlayerDefeated _ when started >= 0: playerDefeats++; break;
                        case DamageDealt d when raid != null && raid.IsCrystal(d.Target):
                            if (!crystalHitters.TryGetValue(d.Target, out var hitters)) crystalHitters[d.Target] = hitters = new HashSet<PlayerId>();
                            hitters.Add(d.Attacker);
                            if (d.RemainingHealth == 0) crystalBrokenAt[d.Target] = tick;
                            break;
                        case GateCleared _:
                            clearedAt = tick;
                            bytesAtClear = host.BytesSent;
                            break;
                        case ReturnedToHub h: home = h; break;
                    }
                }

                foreach (var (session, bot) in clients)
                {
                    session.Tick(session.IsJoined ? bot.Next(session.World, session.PlayerId.Value, session.Events) : PlayerCommand.None);
                    if (started >= 0 && tick > started + 60 && clearedAt < 0) fightCorrection = Math.Max(fightCorrection, session.LastCorrection);
                }
            }

            var raidSeconds = (clearedAt - started) / 60.0;
            var perClient = clearedAt > started ? (bytesAtClear - bytesAtStart) / raidSeconds / (HostSession.MaxPlayers - 1) : 0.0;
            var report = Report(started, clearedAt, shield, shieldBrokenAt, regrows, finalAt, shelters, caught, exposures, playerDefeats,
                crystalHitters, hostTicks, perClient, fightCorrection, host);
            TestContext.WriteLine(report);
            var path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "TestResults", "raid-bots.md"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report);

            // Through the existing session flow: all ten gathered in the hub and went in together.
            Assert.That(started, Is.GreaterThanOrEqualTo(0), "all ten readied up in the hub and the host took them into the raid");
            Assert.That(clients.All(c => c.session.IsJoined), Is.True);

            // Split objectives: four crystals for ten, each broken by its own group, all inside the window.
            Assert.That(shield, Is.Not.Null, "the King was shielded");
            Assert.That(shield.Crystals, Is.EqualTo(4), "ten players: four crystals");
            Assert.That(shieldBrokenAt, Is.GreaterThan(0), "they broke the shield");
            Assert.That(crystalHitters.Count, Is.EqualTo(4), "every crystal was worked on");
            Assert.That(crystalHitters.Values.All(h => h.Count >= 2), Is.True, "by a group, not one player");
            Assert.That(crystalHitters.Values.SelectMany(h => h).Distinct().Count(), Is.GreaterThanOrEqualTo(9), "the party split up across them");
            var lastBreaks = crystalBrokenAt.Values.ToList();
            Assert.That(lastBreaks.Max() - lastBreaks.Min(), Is.LessThanOrEqualTo(8 * 60), "the final breaks came inside the regrow window");

            // The final phase: everyone handles the shelter together.
            Assert.That(finalAt, Is.GreaterThan(shieldBrokenAt), "the phase changed");
            Assert.That(shelters, Is.GreaterThanOrEqualTo(1), "the King wound up its blast");
            Assert.That(exposures, Is.GreaterThan(0));
            Assert.That(caught / (double)exposures, Is.LessThan(0.2), "most of the party was inside the shelter when it landed");

            Assert.That(clearedAt, Is.GreaterThan(0), "they beat the Goblin King");
            Assert.That(home, Is.Not.Null, "and came home");
            Assert.That((home.GateId, home.Cleared), Is.EqualTo((RaidId, true)));

            // The guards recorded earlier (ticket 14: host tick; ticket 31 / ADR 0021: upload per client with the party together).
            Assert.That(hostTicks.Average(), Is.LessThan(4.0), "average host tick (ms)");
            Assert.That(perClient, Is.LessThan(16 * 1024), "host upload per client during the raid (bytes/s)");
            Assert.That(fightCorrection, Is.LessThan(0.5f), "predictions stayed close to the host through the fight");
            Assert.That(host.MalformedMessages, Is.Zero);
            Assert.That(clients.Sum(c => c.session.UnreadableEventLists), Is.Zero);
        }

        private static double Percentile(List<double> values, double p)
        {
            var sorted = values.OrderBy(v => v).ToList();
            return sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, Math.Max(0, (int)Math.Ceiling(p * sorted.Count) - 1))];
        }

        private static string Report(int started, int clearedAt, RaidShieldRaised shield, int shieldBrokenAt, int regrows, int finalAt, int shelters,
            int caught, int exposures, int playerDefeats, Dictionary<EnemyId, HashSet<PlayerId>> crystalHitters, List<double> hostTicks,
            double perClient, float largestCorrection, HostSession host)
        {
            string At(int tick) => tick < 0 ? "never" : FormattableString.Invariant($"{(tick - started) / 60.0:0.0} s");
            var b = new StringBuilder();
            b.AppendLine($"# Ten-bot raid (headless, {DateTime.Now:yyyy-MM-dd HH:mm})");
            b.AppendLine();
            b.AppendLine($"Host bot + 9 bot clients, {Latency * 1000 / 60} ms each way, real content, '{RaidId}' from the hub. Times from the raid's start.");
            b.AppendLine();
            b.AppendLine("| Measure | Value |");
            b.AppendLine("|---|---|");
            b.AppendLine($"| Crystals | {shield?.Crystals ?? 0} |");
            b.AppendLine($"| Shield broken | {At(shieldBrokenAt)} ({regrows} regrow(s) first) |");
            b.AppendLine("| Groups at the crystals | " + string.Join(", ", crystalHitters.OrderBy(p => p.Key.Value).Select(p => p.Value.Count)) + " players |");
            b.AppendLine($"| Final phase | {At(finalAt)} |");
            b.AppendLine($"| Blasts | {shelters} wound up; {caught} of {exposures} player-blasts caught outside |");
            b.AppendLine($"| Player defeats | {playerDefeats} |");
            b.AppendLine($"| Cleared | {At(clearedAt)} |");
            if (hostTicks.Count > 0)
                b.AppendLine(FormattableString.Invariant($"| Host tick, mean / p99 / max | {hostTicks.Average():0.000} / {Percentile(hostTicks, 0.99):0.000} / {hostTicks.Max():0.000} ms |"));
            b.AppendLine(FormattableString.Invariant($"| Host upload per client, raid start to clear | {perClient / 1024:0.0} KB/s |"));
            b.AppendLine(FormattableString.Invariant($"| Largest client correction, entrance to clear | {largestCorrection:0.000} m |"));
            b.AppendLine($"| Last snapshot | {host.LastSnapshotSize} bytes |");
            return b.ToString();
        }
    }
}
