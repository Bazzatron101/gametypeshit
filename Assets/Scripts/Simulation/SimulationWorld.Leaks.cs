using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// Monsters out in the overworld (ticket 43, ADR 0015). Each gate leaks a few of its own kinds round its
    /// entrance (<see cref="OverworldDefinition.LeakCount"/>), so danger follows the gates. They exist only
    /// while the gate's tile is loaded (ticket 40), a cleared gate stops leaking, and one killed is replaced
    /// after a while. A leaked monster only chases players near its gate and walks back otherwise. Out in the
    /// overworld, monsters steer round what they cannot cross (<see cref="GroundPath"/>).
    /// <para>Leaks are the host's: a guest's world (<see cref="RunsLeaks"/> false) only shows the ones snapshots bring.</para>
    /// </summary>
    public sealed partial class SimulationWorld
    {
        /// <summary>How often a monster that has to go round something works its way out again.</summary>
        private const int SteerEveryTicks = 15;

        private readonly SortedDictionary<string, (bool filled, int nextTick)> _leaks = new SortedDictionary<string, (bool, int)>(StringComparer.Ordinal);

        /// <summary>
        /// Whether this world spawns and removes gates' leaked monsters itself: the host's does (the default);
        /// a guest's does not, so its prediction never shows a monster the host has not made.
        /// </summary>
        public bool RunsLeaks { get; set; } = true;

        private void UpdateLeaks(int tick)
        {
            if (!RunsLeaks || !InOverworld) return;
            var overworld = _config.Overworld;
            foreach (var site in overworld.Gates)
            {
                if (site == null) continue;
                var gate = _config.Gates.FirstOrDefault(g => g.Id == site.GateId);
                if (gate == null) continue;
                var mine = _enemies.Where(e => e.LeakedFrom == site.GateId).ToList();

                if (!IsGroundLoaded(site.Entrance))
                {
                    foreach (var e in mine) DespawnEnemy(e.Id); // its tile is gone: so are its monsters
                    _leaks.Remove(site.GateId);
                    continue;
                }
                if (_clearedGates.Contains(site.GateId)) continue; // a cleared gate stops leaking
                var kinds = LeakKinds(gate);
                if (kinds.Count == 0) continue;

                _leaks.TryGetValue(site.GateId, out var state);
                var alive = mine.Count(e => e.Health > 0);
                var wait = Math.Max(1, SecondsToTicks(overworld.LeakRespawnSeconds));
                if (!state.filled)
                {
                    // The first time its tile is up: the whole leak at once.
                    foreach (var dead in mine.Where(e => e.Health <= 0)) DespawnEnemy(dead.Id);
                    for (var i = alive; i < overworld.LeakCount; i++) SpawnLeak(site, kinds[i % kinds.Count], i);
                    _leaks[site.GateId] = (true, 0);
                    continue;
                }
                if (alive >= overworld.LeakCount)
                {
                    if (state.nextTick != 0) _leaks[site.GateId] = (true, 0);
                    continue;
                }
                if (state.nextTick == 0)
                {
                    _leaks[site.GateId] = (true, tick + wait); // one has been killed: the wait starts now
                    continue;
                }
                if (tick < state.nextTick) continue;

                // After that, one at a time, each after its own wait.
                foreach (var dead in mine.Where(e => e.Health <= 0)) DespawnEnemy(dead.Id);
                SpawnLeak(site, kinds[alive % kinds.Count], alive);
                _leaks[site.GateId] = (true, alive + 1 < overworld.LeakCount ? tick + wait : 0);
            }
        }

        /// <summary>A gate's own kinds of monster, as its rooms place them: no bosses.</summary>
        private static List<EnemyDefinition> LeakKinds(GateDefinition gate) =>
            gate.Rooms.SelectMany(r => r.Spawns).Select(s => s.Enemy).Where(e => e != null && e.Boss == null).Distinct().ToList();

        private void SpawnLeak(GateSite site, EnemyDefinition kind, int index)
        {
            var overworld = _config.Overworld;
            var ground = Ground;
            var at = site.Entrance;
            for (var attempt = 0; attempt < 8; attempt++) // round the entrance; a spot it cannot stand on is passed over
            {
                var angle = (index + attempt * 0.37f) * GoldenAngleRadians;
                var p = site.Entrance + new Vec3(MathF.Cos(angle) * overworld.LeakRadius, 0f, MathF.Sin(angle) * overworld.LeakRadius);
                if (!ground.IsWalkable(p)) continue;
                at = p;
                break;
            }
            var id = SpawnEnemy(kind, at);
            var enemy = _enemiesById[id];
            enemy.LeakedFrom = site.GateId;
            enemy.Home = site.Entrance;
            enemy.Leash = overworld.LeakLeash;
        }

        /// <summary>Whether the ground at <paramref name="p"/> is loaded: always, except a tiled world's tiles not streamed in.</summary>
        private bool IsGroundLoaded(Vec3 p) => !(Ground is TiledGround tiled) || tiled.IsLoaded(tiled.TileAt(p));

        /// <summary>A leaked monster's prey: the nearest standing player within its leash of home.</summary>
        private Player NearestLivePlayerNearHome(Enemy enemy, out float distance)
        {
            Player nearest = null;
            distance = float.MaxValue;
            foreach (var player in _players)
            {
                if (!player.IsAlive || player.Away || Flat(player.Position - enemy.Home.Value).Length > enemy.Leash) continue;
                var d = Flat(player.Position - enemy.Position).Length;
                if (d < distance) { distance = d; nearest = player; }
            }
            return nearest;
        }

        private void WalkHome(Enemy enemy, int tick)
        {
            var home = enemy.Home.Value;
            var away = Flat(home - enemy.Position).Length;
            if (away < 1f) return;
            MoveEnemyToward(enemy, home, MathF.Min(enemy.Definition.MoveSpeed * TickDelta, away), tick);
        }

        /// <summary>
        /// One step towards <paramref name="goal"/>. In a gate or the hub, straight there (walls slide it). Out in the
        /// overworld, round what it cannot cross: straight when the line is clear, else along a found way, worked out
        /// again every few ticks or when the goal moves.
        /// </summary>
        private void MoveEnemyToward(Enemy enemy, Vec3 goal, float step, int tick)
        {
            var toward = goal;
            if (InOverworld)
            {
                var stale = tick - enemy.WaypointTick >= SteerEveryTicks || Flat(enemy.WaypointGoal - goal).Length > 3f || Flat(enemy.Waypoint - enemy.Position).Length < 0.5f;
                if (stale)
                {
                    enemy.Waypoint = GroundPath.NextWaypoint(Ground, enemy.Position, goal);
                    enemy.WaypointGoal = goal;
                    enemy.WaypointTick = tick;
                }
                toward = enemy.Waypoint;
            }
            var offset = Flat(toward - enemy.Position);
            var length = offset.Length;
            if (length < Epsilon) return;
            enemy.Position += offset * (MathF.Min(step, length) / length);
        }

        private void WriteLeaks(BinaryWriter w)
        {
            w.Write(_leaks.Count);
            foreach (var pair in _leaks)
            {
                w.Write(pair.Key);
                w.Write(pair.Value.filled);
                w.Write(pair.Value.nextTick);
            }
        }

        private void RestoreLeaks(IEnumerable<(string gate, bool filled, int nextTick)> leaks)
        {
            _leaks.Clear();
            foreach (var (gate, filled, nextTick) in leaks) _leaks[gate] = (filled, nextTick);
        }
    }
}
