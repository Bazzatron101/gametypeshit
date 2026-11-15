using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HunterSystem.Simulation;

namespace HunterSystem.Game
{
    /// <summary>A loaded tile: its ground for the simulation, and anything else made with it off the main thread (its shown mesh's contents).</summary>
    public sealed class TilePayload
    {
        public IGround Ground;
        public object Extra;
    }

    /// <summary>
    /// Keeps the tiles around some points loaded in a <see cref="TiledGround"/> (ticket 40, ADR 0020). Each
    /// <see cref="Update"/> asks for every tile within <see cref="LoadRadius"/> of a point, drops tiles past
    /// <see cref="UnloadRadius"/> from all of them, and puts finished loads in, a few per call so no frame
    /// does all the work. A client streams around its own player; the host around every player.
    /// </summary>
    public sealed class TileStreamer
    {
        private readonly TiledGround _ground;
        private readonly Func<TileCoord, Task<TilePayload>> _load;
        private readonly Dictionary<TileCoord, (Task<TilePayload> task, Stopwatch clock)> _pending = new Dictionary<TileCoord, (Task<TilePayload>, Stopwatch)>();

        public TileStreamer(TiledGround ground, Func<TileCoord, Task<TilePayload>> load, float loadRadius, float unloadRadius)
        {
            _ground = ground ?? throw new ArgumentNullException(nameof(ground));
            _load = load ?? throw new ArgumentNullException(nameof(load));
            if (loadRadius < 0f || unloadRadius < loadRadius) throw new ArgumentException("The unload radius must be at least the load radius.");
            LoadRadius = loadRadius;
            UnloadRadius = unloadRadius;
        }

        public float LoadRadius { get; }
        public float UnloadRadius { get; }

        /// <summary>Finished loads put in per <see cref="Update"/>.</summary>
        public int MaxAppliedPerUpdate { get; set; } = 2;

        public TiledGround Ground => _ground;
        public IReadOnlyCollection<TileCoord> Pending => _pending.Keys;

        /// <summary>A tile went in: its ground is in <see cref="Ground"/>, and the payload has whatever else was made with it.</summary>
        public event Action<TileCoord, TilePayload> TileLoaded;

        /// <summary>A tile came out.</summary>
        public event Action<TileCoord> TileUnloaded;

        /// <summary>Tiles loaded so far, and the longest any of them took from asking to being put in.</summary>
        public int LoadsCompleted { get; private set; }
        public double LongestLoadMilliseconds { get; private set; }

        /// <summary>Asks for the tiles around <paramref name="centres"/>, drops far ones, and puts in finished loads.</summary>
        public void Update(IEnumerable<Vec3> centres)
        {
            var points = centres.ToList();
            var wanted = _ground.TilesWithin(points, LoadRadius);
            var keep = _ground.TilesWithin(points, UnloadRadius);

            foreach (var tile in _ground.Loaded.Where(t => !keep.Contains(t)).ToList())
            {
                _ground.RemoveTile(tile);
                TileUnloaded?.Invoke(tile);
            }
            foreach (var tile in _pending.Keys.Where(t => !keep.Contains(t)).ToList()) _pending.Remove(tile); // no longer needed: its result is dropped

            foreach (var tile in wanted.OrderBy(t => points.Min(c => _ground.DistanceToTile(c, t)))) // nearest first
                if (!_ground.IsLoaded(tile) && !_pending.ContainsKey(tile))
                    _pending[tile] = (_load(tile), Stopwatch.StartNew());

            var applied = 0;
            foreach (var tile in _pending.Keys.ToList())
            {
                var (task, clock) = _pending[tile];
                if (!task.IsCompleted) continue;
                _pending.Remove(tile);
                if (task.IsFaulted || task.IsCanceled || task.Result?.Ground == null)
                {
                    UnityEngine.Debug.LogWarning($"Loading {tile} failed: {task.Exception?.GetBaseException().Message}. It will be asked for again.");
                    continue;
                }
                _ground.SetTile(tile, task.Result.Ground);
                LoadsCompleted++;
                LongestLoadMilliseconds = Math.Max(LongestLoadMilliseconds, clock.Elapsed.TotalMilliseconds);
                TileLoaded?.Invoke(tile, task.Result);
                if (++applied >= MaxAppliedPerUpdate) break;
            }
        }

        /// <summary>Loads the tiles around <paramref name="centres"/> now, waiting for each: the start of a scene, before anyone moves.</summary>
        public void LoadNow(IEnumerable<Vec3> centres)
        {
            var points = centres.ToList();
            foreach (var tile in _ground.TilesWithin(points, LoadRadius))
            {
                if (_ground.IsLoaded(tile)) continue;
                var payload = (_pending.TryGetValue(tile, out var pending) ? pending.task : _load(tile)).Result;
                _pending.Remove(tile);
                _ground.SetTile(tile, payload.Ground);
                LoadsCompleted++;
                TileLoaded?.Invoke(tile, payload);
            }
        }
    }
}
