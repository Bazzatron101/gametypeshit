using System;
using System.Linq;
using HunterSystem.Simulation;

namespace HunterSystem.Netcode
{
    /// <summary>
    /// Keeps a bot fight going (ticket 14): clears defeated enemies, as a gate would, and keeps a
    /// number of living ones around a point, every fourth a tougher one. Host only.
    /// </summary>
    public sealed class EnemyTopUp
    {
        private const float GoldenAngleRadians = 2.39996323f; // spreads successive spawns round the circle
        private const int ToughEvery = 4;

        private readonly EnemyDefinition _common;
        private readonly EnemyDefinition _tough;
        private readonly int _living;
        private readonly float _radius;

        public EnemyTopUp(EnemyDefinition common, EnemyDefinition tough, int living = 8, float radius = 9f)
        {
            _common = common ?? throw new ArgumentNullException(nameof(common));
            _tough = tough ?? common;
            _living = living;
            _radius = radius;
        }

        public int Spawned { get; private set; }

        public void Apply(SimulationWorld world, Vec3 around)
        {
            foreach (var id in world.EnemyIds.Where(id => world.GetEnemy(id).IsDefeated).ToList()) world.DespawnEnemy(id);
            while (world.EnemyIds.Count < _living)
            {
                var angle = Spawned * GoldenAngleRadians;
                var at = around + new Vec3((float)Math.Cos(angle) * _radius, 0f, (float)Math.Sin(angle) * _radius);
                world.SpawnEnemy(Spawned % ToughEvery == ToughEvery - 1 ? _tough : _common, at);
                Spawned++;
            }
        }
    }
}
