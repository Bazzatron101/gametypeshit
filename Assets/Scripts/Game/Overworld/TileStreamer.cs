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

        /// <summary>
        /// How far a point must move before the wanted tiles are worked out again (ticket 44: doing it every tick for ten
        /// players was the host's biggest cost). 25 m is under two seconds at mount speed, against hundreds of metres of lead.
        /// </summary>
        public float RefreshDistance { get; set; } = 25f;

        private readonly List<Vec3> _points = new List<Vec3>();
        private readonly List<Vec3> _refreshedAt = new List<Vec3>();
        private readonly List<TileCoord> _scratch = new List<TileCoord>();
        private bool _mustRefresh = true;
        private int _updatesSinceRefresh;

        /// <summary>A refresh at least this often (in updates), whatever the movement: someone waiting at an edge does not move.</summary>
        public int RefreshEveryUpdates { get; set; } = 30;

        /// <summary>
        /// Puts in finished loads (a few per call). When any point has moved <see cref="RefreshDistance"/> since the last
        /// look, or points came or went, or a load failed, it also asks for the tiles now within reach (nearest first)
        /// and drops the ones out of reach.
        /// </summary>
        public void Update(IEnumerable<Vec3> centres)
        {
            _points.Clear();
            _points.AddRange(centres);
            if (_mustRefresh || ++_updatesSinceRefresh >= RefreshEveryUpdates || Moved()) Refresh();
            ApplyFinished();
        }

        private bool Moved()
        {
            if (_points.Count != _refreshedAt.Count) return true;
            var distance = Math.Min(RefreshDistance, LoadRadius / 4f); // never a big share of the lead
            var limit = distance * distance;
            for (var i = 0; i < _points.Count; i++)
            {
                var dx = _points[i].X - _refreshedAt[i].X;
                var dz = _points[i].Z - _refreshedAt[i].Z;
                if (dx * dx + dz * dz > limit) return true;
            }
            return false;
        }

        private void Refresh()
        {
            _mustRefresh = false;
            _updatesSinceRefresh = 0;
            _refreshedAt.Clear();
            _refreshedAt.AddRange(_points);
            var wanted = _ground.TilesWithin(_points, LoadRadius);
            var keep = _ground.TilesWithin(_points, UnloadRadius);

            _scratch.Clear();
            foreach (var tile in _ground.Loaded) if (!keep.Contains(tile)) _scratch.Add(tile);
            foreach (var tile in _scratch)
            {
                _ground.RemoveTile(tile);
                TileUnloaded?.Invoke(tile);
            }
            _scratch.Clear();
            foreach (var tile in _pending.Keys) if (!keep.Contains(tile)) _scratch.Add(tile);
            foreach (var tile in _scratch) _pending.Remove(tile); // no longer needed: its result is dropped

            // Only the tiles not yet asked for are ordered, nearest first.
            _scratch.Clear();
            foreach (var tile in wanted) if (!_ground.IsLoaded(tile) && !_pending.ContainsKey(tile)) _scratch.Add(tile);
            if (_scratch.Count > 1) _scratch.Sort((a, b) => NearestDistance(a).CompareTo(NearestDistance(b)));
            foreach (var tile in _scratch) _pending[tile] = (_load(tile), Stopwatch.StartNew());
        }

        private float NearestDistance(TileCoord tile)
        {
            var best = float.MaxValue;
            foreach (var p in _points) best = Math.Min(best, _ground.DistanceToTile(p, tile));
            return best;
        }

        private void ApplyFinished()
        {
            if (_pending.Count == 0) return;
            _scratch.Clear();
            foreach (var pair in _pending) if (pair.Value.task.IsCompleted) _scratch.Add(pair.Key);
            var applied = 0;
            foreach (var tile in _scratch)
            {
                var (task, clock) = _pending[tile];
                _pending.Remove(tile);
                if (task.IsFaulted || task.IsCanceled || task.Result?.Ground == null)
                {
                    UnityEngine.Debug.LogWarning($"Loading {tile} failed: {task.Exception?.GetBaseException().Message}. It will be asked for again.");
                    _mustRefresh = true;
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
