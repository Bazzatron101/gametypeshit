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
    /// The overworld's worst case (ticket 44, ADR 0015): ten players far apart on the real continent, each
    /// riding its own road at mount speed, the host simulating and streaming the tiles round all ten and
    /// each client streaming its own, over a simulated network with 100 ms each way. It records the host's
    /// tick and streaming time, its loaded tiles and memory, its upload, and how long tiles take to load on
    /// the clients and whether a rider ever waits for one, to TestResults/spread-stress.md, and guards each.
    /// <para>The continent's tiles are made by Hunter System > Overworld > Rebuild Continent Scene and are not in git;
    /// without them the test is skipped.</para>
    /// </summary>
    public class SpreadOutStressTest
    {
        private const int Latency = 6; // ticks each way: 100 ms at 60 Hz
        private const int Seconds = 60;
        private const int Players = HostSession.MaxPlayers;
        private const string ContinentPath = "Assets/Data/Overworld/Continent.asset";

        private static SimulationConfig ContentConfig()
        {
            var content = AssetDatabase.FindAssets("t:GameContentAsset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameContentAsset>(AssetDatabase.GUIDToAssetPath(guid))).Single();
            var config = new SimulationConfig();
            content.ApplyTo(config);
            return config;
        }

        /// <summary>One rider: the road it rides, back and forth, and how it is getting on.</summary>
        private sealed class Rider
        {
            public List<Vec3> Route;
            public int Next;
            public int Step = 1;
            public Vec3 LastProgressAt;
            public int LastProgressTick;
            public int LastToggleTick = -1000;
            public int HeldUpTicks;
            public Vec3 Detour;
            public int DetourTick = -1000;

            /// <summary>The next command: get on the mount, then ride the road, round anything in the way.</summary>
            public PlayerCommand Drive(SimulationWorld world, PlayerId me, int tick)
            {
                if (!world.HasPlayer(me)) return PlayerCommand.None;
                var self = world.GetPlayer(me);
                if (!self.IsMounted && world.CanMount(me) && tick - LastToggleTick > 30)
                {
                    LastToggleTick = tick;
                    return new PlayerCommand(0f, 0f, toggleMount: true);
                }
                var at = self.Position;
                if ((at - LastProgressAt).Length > 2f) { LastProgressAt = at; LastProgressTick = tick; }
                if (tick - LastProgressTick > 300) { Advance(); LastProgressTick = tick; } // stuck for 5 s: on to the next point
                var goal = Route[Next];
                if (new Vec3(goal.X - at.X, 0f, goal.Z - at.Z).Length < 4f) { Advance(); goal = Route[Next]; }

                // Straight down the road while the next few metres are clear; round what is in the way when not,
                // searching only the next stretch and keeping the answer for half a second (a road point can be a kilometre off).
                var d = new Vec3(goal.X - at.X, 0f, goal.Z - at.Z);
                var distance = d.Length;
                if (distance < 1e-3f) return PlayerCommand.None;
                d = d * (1f / distance);
                if (world.Ground.ReachAlong(at, d, 8f) < 8f && distance > 8f)
                {
                    if (tick - DetourTick > 30)
                    {
                        Detour = GroundPath.NextWaypoint(world.Ground, at, at + d * Math.Min(distance, 60f), margin: 30f, maxNodes: 3000);
                        DetourTick = tick;
                    }
                    var round = new Vec3(Detour.X - at.X, 0f, Detour.Z - at.Z);
                    if (round.Length > 1e-3f) d = round * (1f / round.Length);
                }
                if (world.Ground.KindAt(at + d * 2f) == GroundKind.Unloaded) HeldUpTicks++; // waiting for ground to load
                return new PlayerCommand(d.X, d.Z);
            }

            private void Advance()
            {
                if (Next + Step < 0 || Next + Step >= Route.Count) Step = -Step; // back the other way at a road's end
                Next += Step;
            }
        }

        /// <summary>
        /// Ten places on the roads as far from each other (and the town) as can be, each with the road to ride from there:
        /// towards its longer end first.
        /// </summary>
        private static List<(Vec3 start, Rider rider)> SpreadOut(OverworldAsset continent)
        {
            var town = continent.TownCentre;
            var candidates = new List<(int road, int point, Vec3 at)>();
            for (var r = 0; r < continent.Roads.Count; r++)
            for (var p = 0; p < continent.Roads[r].points.Count; p++)
            {
                var q = continent.Roads[r].points[p];
                if (new UnityEngine.Vector2(q.x - town.x, q.z - town.z).magnitude > 150f) candidates.Add((r, p, new Vec3(q.x, 0f, q.z)));
            }
            var chosen = new List<(int road, int point, Vec3 at)>();
            var townAt = new Vec3(town.x, 0f, town.z);
            while (chosen.Count < Players)
            {
                var best = candidates.OrderByDescending(c => chosen.Select(o => (o.at - c.at).Length).Append((c.at - townAt).Length).Min()).First();
                chosen.Add(best);
                candidates.Remove(best);
            }
            return chosen.Select(c =>
            {
                var road = continent.Roads[c.road].points.Select(q => new Vec3(q.x, 0f, q.z)).ToList();
                var forward = road.Count - 1 - c.point >= c.point; // the longer way first
                var rider = new Rider { Route = road, Next = Math.Min(Math.Max(c.point + (forward ? 1 : -1), 0), road.Count - 1), Step = forward ? 1 : -1, LastProgressAt = c.at };
                return (c.at, rider);
            }).ToList();
        }

        [Test, Timeout(10 * 60 * 1000)] // a slip that makes it crawl fails it, rather than leaving it running for hours
        public void TenPlayersRideApartAcrossTheContinent()
        {
            var continent = AssetDatabase.LoadAssetAtPath<OverworldAsset>(ContinentPath);
            if (continent == null || !continent.IsBaked || !File.Exists(continent.TilePath(new TileCoord(0, 0))))
                Assert.Ignore("The continent's tiles are not built here (they are not in git): Hunter System > Overworld > Rebuild Continent Scene.");

            var spread = SpreadOut(continent);
            var memoryBefore = GC.GetTotalMemory(true);

            // The host: every player's tiles.
            var hostConfig = ContentConfig();
            var hostGround = continent.CreateTiledGround();
            continent.ApplyTo(hostConfig, hostGround);
            var network = new SimulatedNetwork(Latency);
            var host = new HostSession(hostConfig, network.HostEndpoint, id => spread[id.Value - 1].start);
            var hostId = host.AddLocalPlayer();
            var hostStreamer = new TileStreamer(hostGround, LoadGround(continent), 600f, 800f);
            hostStreamer.LoadNow(spread.Select(s => s.start));

            // Nine clients: each its own tiles.
            var clients = new List<(ClientSession session, Rider rider, TileStreamer streamer)>();
            for (var i = 1; i < Players; i++)
            {
                var config = ContentConfig();
                var ground = continent.CreateTiledGround();
                continent.ApplyTo(config, ground);
                var streamer = new TileStreamer(ground, LoadGround(continent), 600f, 800f);
                streamer.LoadNow(new[] { spread[i].start });
                clients.Add((new ClientSession(config, network.Connect(), $"rider-{i}"), spread[i].rider, streamer));
            }

            var travelled = new Dictionary<PlayerId, float>();
            var lastAt = new Dictionary<PlayerId, Vec3>();
            var hostTicks = new List<double>();
            var hostStreaming = new List<double>();
            var loadedTiles = new List<int>();
            var clock = new Stopwatch();
            long bytesWhenFull = -1;
            const int FullFromTick = 120;
            var ticks = Seconds * hostConfig.TickRate;
            for (var tick = 0; tick < ticks; tick++)
            {
                network.Advance();
                if (tick == 30) foreach (var id in host.World.PlayerIds) host.World.GrantMount(id); // everyone has joined: a mount each
                if (tick == FullFromTick) bytesWhenFull = host.BytesSent;

                var command = spread[0].rider.Drive(host.World, hostId, tick);
                clock.Restart();
                host.Tick(command);
                hostTicks.Add(clock.Elapsed.TotalMilliseconds);

                clock.Restart();
                hostStreamer.Update(host.World.PlayerIds.Select(id => host.World.GetPlayer(id).Position));
                hostStreaming.Add(clock.Elapsed.TotalMilliseconds);
                if (tick % 60 == 0) loadedTiles.Add(hostGround.Loaded.Count);
                foreach (var id in host.World.PlayerIds)
                {
                    var at = host.World.GetPlayer(id).Position;
                    if (lastAt.TryGetValue(id, out var was)) travelled[id] = (travelled.TryGetValue(id, out var sum) ? sum : 0f) + new Vec3(at.X - was.X, 0f, at.Z - was.Z).Length;
                    lastAt[id] = at;
                }

                foreach (var (session, rider, streamer) in clients)
                {
                    var me = session.PlayerId;
                    session.Tick(session.IsJoined && me.HasValue ? rider.Drive(session.World, me.Value, tick) : PlayerCommand.None);
                    if (session.IsJoined && me.HasValue && session.World.HasPlayer(me.Value)) streamer.Update(new[] { session.World.GetPlayer(me.Value).Position });
                }
            }

            var memoryAfter = GC.GetTotalMemory(true);
            var upload = (host.BytesSent - bytesWhenFull) / (double)(Seconds - FullFromTick / 60);
            var ridden = host.World.PlayerIds.Select(id => travelled.TryGetValue(id, out var m) ? m : 0f).ToList();
            var minApart = host.World.PlayerIds.SelectMany(a => host.World.PlayerIds.Where(b => b.Value > a.Value)
                .Select(b => (host.World.GetPlayer(a).Position - host.World.GetPlayer(b).Position).Length)).Min();
            var clientLongestLoad = clients.Max(c => c.streamer.LongestLoadMilliseconds);
            var heldUp = clients.Sum(c => c.rider.HeldUpTicks);

            var report = Report(hostTicks, hostStreaming, loadedTiles, memoryAfter - memoryBefore, upload, clients.Select(c => c.streamer).ToList(),
                clientLongestLoad, heldUp, ridden, minApart, host, clients.Select(c => c.session).ToList());
            TestContext.WriteLine(report);
            var path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "TestResults", "spread-stress.md"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report);

            Assert.That(clients.All(c => c.session.IsJoined), Is.True, "every rider joined");
            Assert.That(host.World.PlayerIds.All(id => host.World.GetPlayer(id).IsMounted), Is.True, "every one of the ten is riding");
            Assert.That(ridden.Min(), Is.GreaterThan(600f), "and every one rode most of the minute (16 m/s is 960 m)");
            Assert.That(minApart, Is.GreaterThan(500f), "staying far apart");
            Assert.That(host.MalformedMessages, Is.Zero);

            // Guards against getting worse, set from the first measurement (2026-11-16): see docs/measurements/spread-out-stress-test.md.
            Assert.That(hostTicks.Average(), Is.LessThan(GuardHostTickMs), "average host tick (ms)");
            Assert.That(hostStreaming.Average(), Is.LessThan(GuardHostStreamingMs), "average host streaming per tick (ms)");
            Assert.That(loadedTiles.Max(), Is.LessThan(GuardHostTiles), "tiles loaded on the host at once");
            Assert.That(upload, Is.LessThan(GuardUploadBytesPerSecond), "host upload (bytes/s)");
            Assert.That(clientLongestLoad, Is.LessThan(GuardClientLoadMs), "longest tile load on a client (ms)");
            Assert.That(heldUp, Is.Zero, "no rider ever waited for ground to load");
        }

        // The guards, from the measurements (2026-11-16: host tick 0.25 ms; streaming 1.3 ms, then 0.08 ms once it stopped
        // working out the wanted tiles every tick; 362 tiles; 51 KB/s; longest client load 41 to 498 ms, which counts the
        // queue to be put in and the process's pauses), with room for a slower PC or disk, so a real slip fails and noise does not.
        private const double GuardHostTickMs = 1.0;
        private const double GuardHostStreamingMs = 0.5;
        private const int GuardHostTiles = 450;
        private const double GuardUploadBytesPerSecond = 128 * 1024;
        private const double GuardClientLoadMs = 1000.0;

        /// <summary>
        /// Reads and decodes a tile wholly on a worker thread, as OverworldScene does. (An await here would come back to
        /// Unity's main thread, which LoadNow blocks on: a deadlock.)
        /// </summary>
        private static Func<TileCoord, System.Threading.Tasks.Task<TilePayload>> LoadGround(OverworldAsset continent) => tile =>
        {
            var path = continent.TilePath(tile);
            return System.Threading.Tasks.Task.Run(() => new TilePayload { Ground = GroundBake.Decode(File.ReadAllBytes(path)) });
        };

        private static double Percentile(List<double> values, double p)
        {
            var sorted = values.OrderBy(v => v).ToList();
            return sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(p * sorted.Count) - 1)];
        }

        private static string Report(List<double> hostTicks, List<double> hostStreaming, List<int> loadedTiles, long memoryGrowth, double upload,
            List<TileStreamer> clientStreamers, double clientLongestLoad, int heldUp, List<float> ridden, float minApart, HostSession host, List<ClientSession> clients)
        {
            var b = new StringBuilder();
            b.AppendLine($"# Spread-out stress test (headless, {DateTime.Now:yyyy-MM-dd HH:mm})");
            b.AppendLine();
            b.AppendLine(FormattableString.Invariant($"{Players} players on the continent, each riding its own road at mount speed, {Seconds} s at 60 Hz, {Latency * 1000 / 60} ms each way, real content and tiles."));
            b.AppendLine(FormattableString.Invariant($"Each rode {ridden.Min():0} to {ridden.Max():0} m (16 m/s for 60 s is 960 m); the closest two ended {minApart:0} m apart."));
            b.AppendLine();
            b.AppendLine("| Measure | Value |");
            b.AppendLine("|---|---|");
            b.AppendLine(FormattableString.Invariant($"| Host tick (simulate + snapshots), mean / p99 / max | {hostTicks.Average():0.000} / {Percentile(hostTicks, 0.99):0.000} / {hostTicks.Max():0.000} ms |"));
            b.AppendLine(FormattableString.Invariant($"| Host streaming per tick, mean / p99 / max | {hostStreaming.Average():0.000} / {Percentile(hostStreaming, 0.99):0.000} / {hostStreaming.Max():0.000} ms |"));
            b.AppendLine(FormattableString.Invariant($"| Host tiles loaded, mean / max | {loadedTiles.Average():0} / {loadedTiles.Max()} |"));
            b.AppendLine(FormattableString.Invariant($"| Managed memory grown over the run (host and nine clients in one process) | {memoryGrowth / (1024.0 * 1024.0):0.0} MB |"));
            b.AppendLine(FormattableString.Invariant($"| Host upload with all ten in | {upload / 1024:0.0} KB/s ({upload / 1024 / (Players - 1):0.0} KB/s per client) |"));
            b.AppendLine(FormattableString.Invariant($"| Tile loads on clients | {clientStreamers.Sum(s => s.LoadsCompleted)} |"));
            b.AppendLine(FormattableString.Invariant($"| Longest tile load on a client (asked to put in) | {clientLongestLoad:0} ms |"));
            b.AppendLine(FormattableString.Invariant($"| Ticks a rider waited for ground to load | {heldUp} |"));
            b.AppendLine(FormattableString.Invariant($"| Largest client correction | {clients.Max(c => c.LargestCorrection):0.000} m |"));
            return b.ToString();
        }
    }
}
