using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HunterSystem.Game;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Streaming the overworld (ticket 40, ADR 0020). Tiles load around players and unload far from all of
    /// them. Ground whose tile is not loaded yet is never stepped onto: a player at its edge waits. The host
    /// keeps every player's tiles; a client only its own. Loading keeps ahead of a mount at 16 m/s.
    /// </summary>
    public class OverworldStreamingTest
    {
        private static readonly PlayerId P = new PlayerId(1);

        /// <summary>A flat tile of <paramref name="size"/> metres at <paramref name="tile"/>, at height <paramref name="height"/>.</summary>
        private static HeightmapGround FlatTile(TiledGround map, TileCoord tile, float height, int cells = 10)
        {
            var corner = map.CornerOf(tile);
            return new HeightmapGround(corner.X, corner.Z, map.TileSize / cells, cells, cells, Enumerable.Repeat(height, (cells + 1) * (cells + 1)).ToArray());
        }

        /// <summary>A strip of <paramref name="tiles"/> tiles of 100 m eastwards from the origin, one row deep, none loaded.</summary>
        private static TiledGround Strip(int tiles, float tileSize = 100f) =>
            new TiledGround(0f, 0f, tileSize, tiles, 1, new HeightmapGround(0f, 0f, tileSize * tiles, 1, 1, new[] { 5f, 5f, 5f, 5f }));

        /// <summary>Loads straight away: each tile flat at its column number in metres.</summary>
        private static Func<TileCoord, Task<TilePayload>> InstantLoader(TiledGround map) =>
            tile => Task.FromResult(new TilePayload { Ground = FlatTile(map, tile, tile.X) });

        private static SimulationWorld World(IGround ground, Vec3 start, float speed = 5f)
        {
            var world = new SimulationWorld(new SimulationConfig { TickRate = 60, PlayerMoveSpeed = speed, Ground = ground });
            world.AddPlayer(P, start);
            return world;
        }

        private static void Step(SimulationWorld world, PlayerCommand command, int ticks = 1)
        {
            for (var i = 0; i < ticks; i++) world.Step(new Dictionary<PlayerId, PlayerCommand> { [P] = command });
        }

        // --- Tiled ground ------------------------------------------------------------------------------

        [Test]
        public void GroundWhoseTileIsNotLoadedIsNotLoadedYet()
        {
            var map = Strip(3);
            map.SetTile(new TileCoord(1, 0), FlatTile(map, new TileCoord(1, 0), 7f));

            Assert.That(map.KindAt(new Vec3(150f, 0f, 50f)), Is.EqualTo(GroundKind.Walkable));
            Assert.That(map.HeightAt(new Vec3(150f, 0f, 50f)), Is.EqualTo(7f));
            Assert.That(map.KindAt(new Vec3(50f, 0f, 50f)), Is.EqualTo(GroundKind.Unloaded));
            Assert.That(map.HeightAt(new Vec3(50f, 0f, 50f)), Is.EqualTo(5f), "the coarse map's height meanwhile");
            Assert.That(map.KindAt(new Vec3(-1f, 0f, 50f)), Is.EqualTo(GroundKind.Blocked), "off the map");
            Assert.That(map.KindAt(new Vec3(350f, 0f, 50f)), Is.EqualTo(GroundKind.Blocked), "off the map");
        }

        [Test]
        public void APlayerWaitsAtTheEdgeOfLoadedGroundAndGoesOnOnceItLoads()
        {
            var map = Strip(3);
            map.SetTile(new TileCoord(0, 0), FlatTile(map, new TileCoord(0, 0), 0f));
            var world = World(map, new Vec3(95f, 0f, 50f));

            Step(world, new PlayerCommand(1f, 0f), 120); // 10 m worth of trying, 5 m from the next tile, which is not loaded

            Assert.That(world.GetPlayer(P).Position.X, Is.LessThan(100f).And.GreaterThan(99.8f), "waiting at the edge");

            map.SetTile(new TileCoord(1, 0), FlatTile(map, new TileCoord(1, 0), 0f));
            Step(world, new PlayerCommand(1f, 0f), 60);

            Assert.That(world.GetPlayer(P).Position.X, Is.GreaterThan(104f), "and on once it loads");
        }

        [Test]
        public void SomeonePlacedOnGroundNotLoadedYetStandsAtTheCoarseHeightAndWaits()
        {
            var map = Strip(3);
            var world = World(map, new Vec3(150f, 0f, 50f)); // a join or a gate's way out, before its tile arrives

            Step(world, new PlayerCommand(1f, 0f), 60);

            Assert.That(world.GetPlayer(P).Position.X, Is.EqualTo(150f), "waiting");
            Assert.That(world.GetPlayer(P).Position.Y, Is.EqualTo(5f), "on the coarse map");

            map.SetTile(new TileCoord(1, 0), FlatTile(map, new TileCoord(1, 0), 1f));
            Step(world, new PlayerCommand(1f, 0f), 60);

            Assert.That(world.GetPlayer(P).Position.X, Is.EqualTo(155f).Within(1e-2f));
            Assert.That(world.GetPlayer(P).Position.Y, Is.EqualTo(1f), "on the tile's ground once it is in");
        }

        // --- The streamer ------------------------------------------------------------------------------

        [Test]
        public void TilesLoadNearAndUnloadOnlyPastTheLargerRadius()
        {
            var map = Strip(10);
            var streamer = new TileStreamer(map, InstantLoader(map), loadRadius: 100f, unloadRadius: 250f) { MaxAppliedPerUpdate = 10 };

            streamer.Update(new[] { new Vec3(450f, 0f, 50f) });
            Assert.That(map.Loaded.Select(t => t.X).OrderBy(x => x), Is.EqualTo(new[] { 3, 4, 5 }), "within 100 m");

            streamer.Update(new[] { new Vec3(660f, 0f, 50f) });
            Assert.That(map.Loaded.Select(t => t.X).OrderBy(x => x), Is.EqualTo(new[] { 4, 5, 6, 7 }),
                "tile 3 (260 m away) is dropped; tile 4 (160 m) is kept though it would not be loaded now");
            Assert.That(map.HeightAt(new Vec3(660f, 0f, 50f)), Is.EqualTo(6f), "the tile's own ground");
        }

        [Test]
        public void FinishedLoadsArePutInAFewAtATime()
        {
            var map = Strip(10);
            var streamer = new TileStreamer(map, InstantLoader(map), loadRadius: 400f, unloadRadius: 500f) { MaxAppliedPerUpdate = 2 };

            streamer.Update(new[] { new Vec3(450f, 0f, 50f) });

            Assert.That(map.Loaded.Count, Is.EqualTo(2), "no frame does all the work");
            Assert.That(map.Loaded.Select(t => t.X), Is.EquivalentTo(new[] { 4, 3 }).Or.EquivalentTo(new[] { 4, 5 }), "nearest first");
            for (var i = 0; i < 5; i++) streamer.Update(new[] { new Vec3(450f, 0f, 50f) });
            Assert.That(map.Loaded.Count, Is.EqualTo(9));
        }

        // --- Host and client ----------------------------------------------------------------------------

        [Test]
        public void TheHostKeepsEveryPlayersTilesAndAClientOnlyItsOwn()
        {
            var hostMap = Strip(12);
            var clientMap = Strip(12);
            var network = new SimulatedNetwork(2);
            Vec3 Spawn(PlayerId id) => id.Value == 1 ? new Vec3(550f, 0f, 50f) : new Vec3(650f, 0f, 50f);
            var hostStreamer = new TileStreamer(hostMap, InstantLoader(hostMap), 100f, 200f) { MaxAppliedPerUpdate = 10 };
            var clientStreamer = new TileStreamer(clientMap, InstantLoader(clientMap), 100f, 200f) { MaxAppliedPerUpdate = 10 };
            hostStreamer.LoadNow(new[] { Spawn(new PlayerId(1)), Spawn(new PlayerId(2)) });
            clientStreamer.LoadNow(new[] { Spawn(new PlayerId(2)) });

            var host = new HostSession(new SimulationConfig { TickRate = 60, PlayerMoveSpeed = 8f, Ground = hostMap }, network.HostEndpoint, Spawn, 3);
            var me = host.AddLocalPlayer();
            var client = new ClientSession(new SimulationConfig { TickRate = 60, PlayerMoveSpeed = 8f, Ground = clientMap }, network.Connect(), "guest");

            void Tick(PlayerCommand hostInput, PlayerCommand clientInput)
            {
                network.Advance();
                host.Tick(hostInput);
                client.Tick(clientInput);
                hostStreamer.Update(host.World.PlayerIds.Select(id => host.World.GetPlayer(id).Position)); // the host: every player
                if (client.World != null && client.PlayerId.HasValue && client.World.HasPlayer(client.PlayerId.Value))
                    clientStreamer.Update(new[] { client.World.GetPlayer(client.PlayerId.Value).Position }); // a client: its own
            }

            for (var i = 0; i < 20; i++) Tick(PlayerCommand.None, PlayerCommand.None); // join
            Assert.That(client.PlayerId.HasValue, Is.True, "test setup: the guest joined");
            for (var i = 0; i < 60 * 70; i++) Tick(new PlayerCommand(-1f, 0f), new PlayerCommand(1f, 0f)); // 70 s apart at 8 m/s: each to an end
            for (var i = 0; i < 20; i++) Tick(PlayerCommand.None, PlayerCommand.None);

            var hostAt = host.World.GetPlayer(me).Position;
            var guestAt = host.World.GetPlayer(client.PlayerId.Value).Position;
            Assert.That(hostAt.X, Is.LessThan(50f), "the host walked west to the map's end");
            Assert.That(guestAt.X, Is.GreaterThan(1150f), "the guest walked east to the other end, never held up on the host");

            var hostTile = hostMap.TileAt(hostAt);
            var guestTile = hostMap.TileAt(guestAt);
            Assert.That(hostMap.IsLoaded(hostTile) && hostMap.IsLoaded(guestTile), Is.True, "the host has both players' ground");
            Assert.That(hostMap.Loaded.Any(t => t.X >= 3 && t.X <= 8), Is.False, "and nothing left in the middle");
            Assert.That(clientMap.IsLoaded(clientMap.TileAt(guestAt)), Is.True, "the guest has its own ground");
            Assert.That(clientMap.Loaded.All(t => t.X >= 9), Is.True, "and nothing near the host");
            Assert.That(client.World.GetPlayer(client.PlayerId.Value).Position.X, Is.EqualTo(guestAt.X).Within(1e-3f), "the guest's prediction agrees with the host");
        }

        // --- Fast enough for a mount ------------------------------------------------------------------

        /// <summary>
        /// Rides east at <paramref name="speed"/> over 20 tiles of 250 m while every tile takes
        /// <paramref name="loadTicks"/> ticks to arrive. Returns the ticks the rider was held up.
        /// </summary>
        private static int TicksHeldUp(float speed, int loadTicks, float loadRadius)
        {
            var map = new TiledGround(0f, 0f, 250f, 20, 1, new HeightmapGround(0f, 0f, 5000f, 1, 1, new float[4]));
            var waiting = new List<(int due, TaskCompletionSource<TilePayload> load, TileCoord tile)>();
            var tick = 0;
            var streamer = new TileStreamer(map, tile =>
            {
                var load = new TaskCompletionSource<TilePayload>();
                waiting.Add((tick + loadTicks, load, tile));
                return load.Task;
            }, loadRadius, loadRadius + 200f) { MaxAppliedPerUpdate = 1 };
            map.SetTile(new TileCoord(0, 0), FlatTile(map, new TileCoord(0, 0), 0f));
            var world = World(map, new Vec3(10f, 0f, 125f), speed);

            var held = 0;
            for (tick = 0; tick < 60 * 400 && world.GetPlayer(P).Position.X < 4900f; tick++)
            {
                foreach (var due in waiting.Where(w => w.due <= tick).ToList())
                {
                    due.load.SetResult(new TilePayload { Ground = FlatTile(map, due.tile, 0f) });
                    waiting.Remove(due);
                }
                streamer.Update(new[] { world.GetPlayer(P).Position });
                var before = world.GetPlayer(P).Position.X;
                Step(world, new PlayerCommand(1f, 0f));
                if (world.GetPlayer(P).Position.X - before < speed / 60f * 0.5f) held++;
            }
            Assert.That(world.GetPlayer(P).Position.X, Is.GreaterThan(4900f), "test setup: the ride reached the far end");
            return held;
        }

        [Test]
        public void LoadingKeepsAheadOfAMountAt16MetresASecond()
        {
            Assert.That(TicksHeldUp(16f, loadTicks: 60, loadRadius: 600f), Is.Zero, "a second per tile, and never held up");
            Assert.That(TicksHeldUp(16f, loadTicks: 60, loadRadius: 1f), Is.GreaterThan(0), "while loading only the next tile at its edge would hold a rider up");
        }

        [Test]
        public void MeasureLoadingARealTile()
        {
            var patch = UnityEditor.AssetDatabase.LoadAssetAtPath<OverworldAsset>("Assets/Data/Overworld/TestPatch.asset");
            Assert.That(patch, Is.Not.Null, "the test patch is baked");
            var map = patch.CreateTiledGround();
            var timings = new List<(double read, double mesh, double toMesh)>();
            for (var i = 0; i < 4; i++)
            {
                var tile = new TileCoord(i, i);
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var ground = patch.LoadTileAsync(tile).Result; // off the main thread in the game
                var read = clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                var corner = map.CornerOf(tile);
                var data = FlatTerrainMesh.BuildData(ground, corner.X, corner.Z, map.TileSize, patch.MeshCellSize,
                    at => FlatTerrainMesh.SurfaceAt(ground, at, patch.SeaLevel, patch.SandAboveSea, patch.DistanceToRoad)); // off the main thread too
                var mesh = clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                UnityEngine.Object.DestroyImmediate(FlatTerrainMesh.ToMesh(data)); // the only main-thread part
                timings.Add((read, mesh, clock.Elapsed.TotalMilliseconds));
            }
            var worst = timings.Skip(1).ToList(); // the first read warms the disk cache and the JIT
            TestContext.WriteLine($"read and decode: {worst.Max(t => t.read):0.0} ms; mesh contents: {worst.Max(t => t.mesh):0.0} ms; " +
                                  $"Mesh on the main thread: {worst.Max(t => t.toMesh):0.0} ms (worst of three tiles)");
            Assert.That(worst.Max(t => t.toMesh), Is.LessThan(16.0), "putting a tile in fits inside one frame");
            Assert.That(worst.Max(t => t.read + t.mesh), Is.LessThan(1000.0), "a tile is ready well inside the second the mount test allows");
        }

        // --- Memory ------------------------------------------------------------------------------------

        [Test]
        public void MeasureTheHostsGroundInMemory()
        {
            // A real tile: 250 m at 257 × 257 samples (about 1 m), heights and kinds.
            const int cells = 256;
            var before = GC.GetTotalMemory(true);
            var tiles = Enumerable.Range(0, 20).Select(_ => new HeightmapGround(0f, 0f, 250f / cells, cells, cells, new float[(cells + 1) * (cells + 1)], new GroundKind[cells * cells])).ToList();
            var perTile = (GC.GetTotalMemory(true) - before) / (double)tiles.Count;
            GC.KeepAlive(tiles);

            var map = new TiledGround(-5250f, -3750f, 250f, 42, 30); // the full map's tiles (ADR 0015: about 10.5 × 7.5 km)
            var one = map.TilesWithin(new[] { new Vec3(0f, 0f, 0f) }, 600f).Count;
            var spread = map.TilesWithin(Enumerable.Range(0, 10).Select(i => new Vec3(-4500f + i * 1000f, 0f, (i % 2 == 0 ? -2500f : 2500f))), 600f).Count;

            TestContext.WriteLine($"one tile: {perTile / 1024:0} KB; one player loads {one} tiles ({one * perTile / (1024 * 1024):0.0} MB); " +
                                  $"10 players spread apart: {spread} tiles ({spread * perTile / (1024 * 1024):0.0} MB); whole map: {map.TilesX * map.TilesZ} tiles");
            Assert.That(perTile, Is.LessThan(400 * 1024), "a tile's ground stays under 400 KB");
            Assert.That(spread * perTile, Is.LessThan(150.0 * 1024 * 1024), "ten players' ground stays under 150 MB on the host");
        }
    }
}
