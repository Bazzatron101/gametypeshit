using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>Player actions: movement, dodge, attacks, soft lock-on and hit resolution.</summary>
    public sealed partial class SimulationWorld
    {
        /// <summary>The command's move input as a unit direction, if there is any input at all.</summary>
        private static bool TryGetMoveDirection(PlayerCommand cmd, out Vec3 direction, out float magnitude)
        {
            var input = new Vec3(cmd.MoveX, 0f, cmd.MoveZ);
            magnitude = input.Length;
            direction = magnitude < Epsilon ? Vec3.Zero : input * (1f / magnitude);
            return magnitude >= Epsilon;
        }

        private void MovePlayer(Player player, PlayerCommand cmd)
        {
            if (!TryGetMoveDirection(cmd, out var direction, out var magnitude)) return;

            var speed = _config.PlayerMoveSpeed * (1f + PointsAboveStart(player, Stat.Agility) * _config.StatEffects.MoveSpeedPerAgility);
            player.Position += direction * (magnitude * speed * TickDelta);
            player.Facing = direction;
        }

        private void TryDodge(Player player, PlayerCommand cmd, int tick)
        {
            if (tick < player.DodgeReadyAtTick || player.IsDodging(tick)) return;

            player.DodgeDirection = TryGetMoveDirection(cmd, out var direction, out _) ? direction : player.Facing;
            player.Facing = player.DodgeDirection;
            player.DodgeEndsAtTick = tick + _dodgeTicks;
            player.InvulnerableUntilTick = tick + _dodgeInvulnerableTicks;
            player.DodgeReadyAtTick = tick + _dodgeCooldownTicks;
            player.PendingAttackLandsAtTick = 0; // a dodge cancels any attack windup
            _events.Add(new DodgePerformed(tick, player.Id));
        }

        /// <summary>
        /// Starts an attack if it is off cooldown: soft lock-on turns the player first, then the
        /// attack swings now (no windup) or is held until its windup ends.
        /// </summary>
        private void TryStartAttack(Player player, ResolvedAttack attack, int tick)
        {
            if (tick < player.AttackReadyAtTick[(int)attack.Kind]) return;
            player.AttackReadyAtTick[(int)attack.Kind] = tick + attack.CooldownTicks;

            ApplySoftLockOn(player, attack);

            if (attack.WindupTicks == 0)
            {
                SwingAttack(player, attack, tick);
                return;
            }

            player.PendingAttack = attack.Kind;
            player.PendingAttackLandsAtTick = tick + attack.WindupTicks;
            _events.Add(new AttackWindupStarted(tick, player.Id, attack.Kind, player.PendingAttackLandsAtTick));
        }

        /// <summary>
        /// Turns the player toward the nearest live enemy inside the assist cone around their
        /// current facing (which already reflects any direction they are holding). The turn is at
        /// most half the cone: that is the nudge. Enemies outside the cone are ignored, so aiming
        /// away overrides the assist. Only enemies this attack can reach count, so the assist never
        /// drags a swing off a hittable enemy toward one it cannot touch.
        /// </summary>
        private void ApplySoftLockOn(Player player, ResolvedAttack attack)
        {
            var assist = _config.SoftLockOn;
            if (!assist.Enabled) return;
            var reach = MathF.Min(assist.Range, attack.Definition.Range);

            Vec3? best = null;
            var bestDistance = float.MaxValue;
            foreach (var enemy in _enemies)
            {
                if (enemy.Health <= 0) continue;
                var offset = Flat(enemy.Position - player.Position);
                var distance = offset.Length;
                if (distance < Epsilon || distance > reach || distance >= bestDistance) continue;
                if (!WithinCone(player.Facing, offset, distance, _softLockMinCos)) continue;
                best = offset * (1f / distance);
                bestDistance = distance;
            }

            if (best.HasValue) player.Facing = best.Value;
        }

        private void SwingAttack(Player player, ResolvedAttack attack, int tick)
        {
            _events.Add(new AttackPerformed(tick, player.Id, attack.Kind));
            var damage = ScaledDamage(player, attack.Definition.Damage);

            foreach (var enemy in _enemies)
            {
                if (enemy.Health <= 0 || !InAttackCone(player, enemy.Position, attack)) continue;

                var amount = Math.Min(damage, enemy.Health);
                enemy.Health -= amount;
                _events.Add(new DamageDealt(tick, player.Id, enemy.Id, amount, enemy.Health));
                if (enemy.Health == 0)
                {
                    _events.Add(new EnemyDefeated(tick, enemy.Id, player.Id));
                    AwardXp(enemy, tick);
                }
            }
        }

        private static bool InAttackCone(Player player, Vec3 target, ResolvedAttack attack)
        {
            var offset = Flat(target - player.Position);
            var distance = offset.Length;
            if (distance > attack.Definition.Range) return false;
            if (distance < Epsilon) return true; // standing on top of it counts as a hit
            return WithinCone(player.Facing, offset, distance, attack.MinCos);
        }

        private static bool WithinCone(Vec3 facing, Vec3 offset, float distance, float minCos)
        {
            var cos = (offset.X * facing.X + offset.Z * facing.Z) / distance;
            return cos >= minCos - 1e-6f;
        }
    }
}
