using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>Enemy behaviour: chase, telegraphed attacks, and hits on players.</summary>
    public sealed partial class SimulationWorld
    {
        /// <summary>Winding up: hold still until the hit lands. Otherwise attack a target in reach, or chase the nearest one.</summary>
        private void UpdateEnemy(Enemy enemy, int tick)
        {
            if (enemy.AttackLandsAtTick != 0)
            {
                if (tick < enemy.AttackLandsAtTick) return;
                ResolveEnemyAttack(enemy, tick);
                return;
            }

            var target = NearestLivePlayer(enemy.Position, out var distance);
            if (target == null) return;

            if (distance <= enemy.Definition.AttackRange && tick >= enemy.AttackReadyAtTick)
            {
                enemy.AttackTarget = target;
                enemy.AttackLandsAtTick = tick + enemy.WindupTicks;
                _events.Add(new EnemyAttackStarted(tick, enemy.Id, target.Id, enemy.AttackLandsAtTick));
                return;
            }

            var gap = distance - enemy.Definition.StopDistance;
            if (gap <= 0f) return;
            var step = MathF.Min(enemy.Definition.MoveSpeed * TickDelta, gap);
            enemy.Position += Flat(target.Position - enemy.Position) * (step / distance);
        }

        private void ResolveEnemyAttack(Enemy enemy, int tick)
        {
            var target = enemy.AttackTarget;
            enemy.AttackLandsAtTick = 0;
            enemy.AttackTarget = null;
            enemy.AttackReadyAtTick = tick + enemy.AttackCooldownTicks;

            if (!target.IsAlive || Flat(target.Position - enemy.Position).Length > enemy.Definition.AttackRange) return; // whiff

            if (target.IsInvulnerable(tick))
            {
                _events.Add(new AttackDodged(tick, enemy.Id, target.Id));
                if (target.IsCharging) CounterWithCharge(target, enemy, tick); // a perfect dodge
                return;
            }

            var amount = target.TakeDamage(enemy.Definition.AttackDamage, out var defeated);
            if (amount == 0) return; // a zero-damage hit is not news
            _events.Add(new PlayerDamaged(tick, enemy.Id, target.Id, amount, target.Health));
            if (defeated) _events.Add(new PlayerDefeated(tick, target.Id, enemy.Id));
        }

        private Player NearestLivePlayer(Vec3 from, out float distance)
        {
            Player nearest = null;
            distance = float.MaxValue;
            foreach (var player in _players)
            {
                if (!player.IsAlive) continue;
                var d = Flat(player.Position - from).Length;
                if (d < distance) { distance = d; nearest = player; }
            }
            return nearest;
        }
    }
}
