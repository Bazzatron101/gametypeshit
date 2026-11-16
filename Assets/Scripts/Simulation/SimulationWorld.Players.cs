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

        private void MovePlayer(Player player, PlayerCommand cmd, float speedFactor = 1f)
        {
            if (!TryGetMoveDirection(cmd, out var direction, out var magnitude)) return;

            var speed = player.Mounted
                ? _config.MountSpeed // the mount's pace, whatever the rider's agility
                : speedFactor * _config.PlayerMoveSpeed * Math.Max(MinStatScalePerMille / 1000f, 1f + PointsAboveStart(player, Stat.Agility) * _config.StatEffects.MoveSpeedPerAgility);
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
        /// Starts an attack if it is off cooldown: the worn weapon's (ticket 46), or the default one.
        /// The default attack turns the player with soft lock-on first; a weapon's hit aims as a skill
        /// does, when it lands. The attack swings now (no windup) or is held until its windup ends.
        /// </summary>
        private void TryStartAttack(Player player, AttackKind kind, int tick)
        {
            if (tick < player.AttackReadyAtTick[(int)kind]) return;
            var weapon = WeaponAttackOf(player, kind);
            int cooldownTicks, windupTicks;
            if (weapon != null)
            {
                cooldownTicks = Resolve(player, weapon.Hit).CooldownTicks;
                windupTicks = SecondsToTicks(weapon.WindupSeconds);
            }
            else
            {
                var attack = _attacks[(int)kind];
                cooldownTicks = attack.CooldownTicks;
                windupTicks = attack.WindupTicks;
                ApplySoftLockOn(player, attack.Definition.Range);
            }
            player.AttackReadyAtTick[(int)kind] = tick + cooldownTicks;

            if (windupTicks == 0)
            {
                SwingAttack(player, kind, tick);
                return;
            }

            player.PendingAttack = kind;
            player.PendingAttackLandsAtTick = tick + windupTicks;
            _events.Add(new AttackWindupStarted(tick, player.Id, kind, player.PendingAttackLandsAtTick));
        }

        /// <summary>The worn weapon's attack of this kind, or null for the default one.</summary>
        private static WeaponAttack WeaponAttackOf(Player player, AttackKind kind)
        {
            var weapon = player.Worn[(int)GearSlot.Weapon];
            if (weapon == null) return null;
            return kind == AttackKind.Heavy ? weapon.HeavyAttack : weapon.LightAttack;
        }

        /// <summary>
        /// Turns the player toward the nearest live enemy inside the assist cone around their
        /// current facing (which already reflects any direction they are holding). The turn is at
        /// most half the cone: that is the nudge. Enemies outside the cone are ignored, so aiming
        /// away overrides the assist. Only enemies within <paramref name="attackReach"/> count, so the
        /// assist never drags a swing off a hittable enemy toward one it cannot touch.
        /// </summary>
        private void ApplySoftLockOn(Player player, float attackReach)
        {
            var assist = _config.SoftLockOn;
            if (!assist.Enabled) return;
            var reach = MathF.Min(assist.Range, attackReach);

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

        /// <summary>The attack's hit frame: the weapon's hit, as resolved for this player now, or the default swing.</summary>
        private void SwingAttack(Player player, AttackKind kind, int tick)
        {
            var weapon = WeaponAttackOf(player, kind);
            if (weapon != null)
            {
                var resolved = Resolve(player, weapon.Hit);
                _events.Add(new AttackPerformed(tick, player.Id, kind, weapon.Hit, resolved.Range, resolved.Radius));
                PerformHit(player, resolved, tick, counter: false);
                return;
            }

            var attack = _attacks[(int)kind];
            _events.Add(new AttackPerformed(tick, player.Id, attack.Kind));
            var damage = ScaledDamage(player, attack.Definition.Damage, Stat.Strength);

            DamageEnemiesWhere(player, damage, tick, skill: null, at => InCone(player, at, attack.Definition.Range, attack.MinCos));
        }

        /// <summary>Damages every live enemy whose position passes <paramref name="inShape"/>.</summary>
        private void DamageEnemiesWhere(Player attacker, int damage, int tick, SkillDefinition skill, Func<Vec3, bool> inShape)
        {
            foreach (var enemy in _enemies)
                if (enemy.Health > 0 && inShape(enemy.Position))
                    DamageEnemy(attacker, enemy, damage, tick, skill);
        }

        /// <summary>
        /// The one place enemy health goes down, from attacks, skills, projectiles and explosions alike.
        /// Returns the health actually removed.
        /// </summary>
        private int DamageEnemy(Player attacker, Enemy enemy, int damage, int tick, SkillDefinition skill, bool secondary = false)
        {
            var amount = Math.Min(Math.Max(0, damage), enemy.Health);
            if (amount == 0) return 0;
            enemy.Health -= amount;
            _events.Add(new DamageDealt(tick, attacker.Id, enemy.Id, amount, enemy.Health, skill, secondary));
            CheckEnrage(enemy);
            if (enemy.Health == 0)
            {
                if (enemy.Definition.Boss != null) StopBoss(enemy);
                _events.Add(new EnemyDefeated(tick, enemy.Id, attacker.Id));
                AwardXp(enemy, tick);
            }
            return amount;
        }

        private static bool InCone(Player player, Vec3 target, float range, float minCos)
        {
            var offset = Flat(target - player.Position);
            var distance = offset.Length;
            if (distance > range) return false;
            if (distance < Epsilon) return true; // standing on top of it counts as a hit
            return WithinCone(player.Facing, offset, distance, minCos);
        }

        private static bool WithinCone(Vec3 facing, Vec3 offset, float distance, float minCos)
        {
            var cos = (offset.X * facing.X + offset.Z * facing.Z) / distance;
            return cos >= minCos - 1e-6f;
        }
    }
}
