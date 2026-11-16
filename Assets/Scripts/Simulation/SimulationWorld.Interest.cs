using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// What one client is sent (ticket 41): the players, enemies and projectiles near its player. Anything an
    /// included entity points at (an enemy's target, a bolt's thrower) is included too, so a snapshot cut
    /// down this way still restores whole. <see cref="Everything"/> sends the whole world.
    /// </summary>
    public sealed class SnapshotInterest
    {
        public static readonly SnapshotInterest Everything = new SnapshotInterest(null, null, null, null);

        private readonly HashSet<int> _players, _enemies, _projectiles;

        internal SnapshotInterest(PlayerId? viewer, HashSet<int> players, HashSet<int> enemies, HashSet<int> projectiles)
        {
            Viewer = viewer;
            _players = players;
            _enemies = enemies;
            _projectiles = projectiles;
        }

        /// <summary>Whose interest this is; null for <see cref="Everything"/>.</summary>
        public PlayerId? Viewer { get; }

        public bool IsEverything => _players == null;

        public bool Includes(PlayerId player) => _players == null || _players.Contains(player.Value);
        public bool Includes(EnemyId enemy) => _enemies == null || _enemies.Contains(enemy.Value);
        internal bool IncludesProjectile(int id) => _projectiles == null || _projectiles.Contains(id);

        public IEnumerable<PlayerId> Players => _players?.Select(v => new PlayerId(v)) ?? Enumerable.Empty<PlayerId>();

        /// <summary>
        /// Whether a client with this interest is told about <paramref name="e"/>: always when it has no one in it
        /// (a gate cleared, a story scene) or names the viewer's own player; otherwise only when every player and
        /// enemy it names is included, so a client never hears about someone it cannot see.
        /// </summary>
        public bool Includes(SimulationEvent e)
        {
            if (IsEverything) return true;
            var all = true;
            foreach (var reference in EventReferences.Of(e))
            {
                if (reference is PlayerId p)
                {
                    if (Viewer.HasValue && p == Viewer.Value) return true;
                    if (!Includes(p)) all = false;
                }
                else if (reference is EnemyId en && !Includes(en)) all = false;
            }
            return all;
        }

        /// <summary>The players and enemies an event names, found once per event type.</summary>
        private static class EventReferences
        {
            private static readonly Dictionary<Type, PropertyInfo[]> ByType = new Dictionary<Type, PropertyInfo[]>();

            public static IEnumerable<object> Of(SimulationEvent e)
            {
                PropertyInfo[] properties;
                lock (ByType)
                {
                    if (!ByType.TryGetValue(e.GetType(), out properties))
                    {
                        properties = e.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.PropertyType == typeof(PlayerId) || p.PropertyType == typeof(EnemyId) ||
                                        p.PropertyType == typeof(PlayerId?) || p.PropertyType == typeof(EnemyId?))
                            .ToArray();
                        ByType[e.GetType()] = properties;
                    }
                }
                foreach (var property in properties)
                {
                    var value = property.GetValue(e);
                    if (value != null) yield return value;
                }
            }
        }
    }

    public sealed partial class SimulationWorld
    {
        /// <summary>
        /// What <paramref name="viewer"/>'s client is sent (ticket 41). Out in the overworld: their own player, and every
        /// player, enemy and projectile within <paramref name="radius"/> of them on the floor, plus whatever those point at.
        /// In a gate or the hub the party is together, so everything.
        /// </summary>
        public SnapshotInterest InterestOf(PlayerId viewer, float radius)
        {
            if (!InOverworld || !_playersById.TryGetValue(viewer, out var me)) return SnapshotInterest.Everything;
            bool Near(Vec3 p) => Flat(p - me.Position).Length <= radius;

            var players = new HashSet<int> { viewer.Value };
            foreach (var p in _players) if (Near(p.Position)) players.Add(p.Id.Value);
            var enemies = new HashSet<int>();
            foreach (var e in _enemies)
            {
                if (!Near(e.Position)) continue;
                enemies.Add(e.Id.Value);
                if (e.AttackTarget != null) players.Add(e.AttackTarget.Id.Value); // a snapshot never names someone it leaves out
            }
            var projectiles = new HashSet<int>();
            foreach (var p in _projectiles)
            {
                if (!Near(p.Position)) continue;
                projectiles.Add(p.Id);
                players.Add(p.Owner.Id.Value);
            }
            return new SnapshotInterest(viewer, players, enemies, projectiles);
        }

        /// <summary>Where every player stands (on the floor), for a map: the far party members a cut-down snapshot leaves out.</summary>
        public IEnumerable<(PlayerId id, Vec3 position)> PlayerPositions() => _players.Select(p => (p.Id, p.Position));
    }
}
