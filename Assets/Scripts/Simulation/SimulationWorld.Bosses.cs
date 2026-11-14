using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// Boss behaviour (ticket 18). A boss turns slowly towards the nearest player and picks the
    /// attack whose answer is obvious from where the players stand:
    /// <list type="bullet">
    /// <item>a slam when two or more players crowd it, or its target has got behind it;</item>
    /// <item>a sweep when its target is in front and in reach;</item>
    /// <item>a charge when its target is out of reach;</item>
    /// <item>otherwise it walks closer.</item>
    /// </list>
    /// Each attack is aimed when it starts and lands on whoever is in the marked area when the
    /// windup ends. At its enrage threshold it enrages once: faster windups and cooldowns, and
    /// its adds join the fight.
    /// </summary>
    public sealed partial class SimulationWorld
    {
        private const float DegreesToRadians = (float)(Math.PI / 180.0);

        private void UpdateBoss(Enemy boss, int tick)
        {
            var definition = boss.Definition.Boss;

            if (boss.Charging)
            {
                ContinueCharge(boss, tick);
                return;
            }

            if (boss.AttackLandsAtTick != 0)
            {
                if (tick < boss.AttackLandsAtTick) return;
                LandBossAttack(boss, tick);
                return;
            }

            var target = NearestLivePlayer(boss.Position, out var distance);
            if (target == null) return;
            var toTarget = Flat(target.Position - boss.Position);
            TurnTowards(boss, toTarget, definition.TurnDegreesPerSecond);

            if (tick >= boss.AttackReadyAtTick)
            {
                var choice = ChooseBossAttack(boss, definition, target, toTarget, distance);
                if (choice != null)
                {
                    StartBossAttack(boss, choice, toTarget, distance, tick);
                    return;
                }
            }

            var gap = distance - boss.Definition.StopDistance;
            if (gap <= 0f || distance < Epsilon) return;
            var step = MathF.Min(boss.Definition.MoveSpeed * TickDelta, gap);
            boss.Position += toTarget * (step / distance);
        }

        private BossAttackDefinition ChooseBossAttack(Enemy boss, BossDefinition definition, Player target, Vec3 toTarget, float distance)
        {
            var slam = definition.Attack(BossAttackKind.Slam);
            var sweep = definition.Attack(BossAttackKind.Sweep);
            var charge = definition.Attack(BossAttackKind.Charge);

            var behind = distance > Epsilon && (toTarget.X * boss.Facing.X + toTarget.Z * boss.Facing.Z) < 0f;
            if (slam != null && (PlayersWithin(boss.Position, slam.Radius) >= 2 || (behind && distance <= slam.Radius))) return slam;
            if (slam != null && sweep == null && distance <= slam.Radius) return slam; // no sweep to answer someone close
            if (sweep != null && distance <= sweep.Range && !behind) return sweep;
            if (charge != null && distance > (sweep?.Range ?? 0f) && !behind) return charge; // it turns to face them first
            return null;
        }

        private int PlayersWithin(Vec3 point, float radius)
        {
            var count = 0;
            foreach (var player in _players)
                if (player.IsAlive && !player.Away && Flat(player.Position - point).Length <= radius) count++;
            return count;
        }

        private void StartBossAttack(Enemy boss, BossAttackDefinition attack, Vec3 toTarget, float distance, int tick)
        {
            var direction = distance > Epsilon ? toTarget * (1f / distance) : boss.Facing;
            if (attack.Kind != BossAttackKind.Slam) boss.Facing = direction; // it turns to strike; a slam needs no aim
            boss.BossAttack = boss.Definition.Boss.Attacks.IndexOf(attack);
            boss.AttackOrigin = boss.Position;
            boss.AttackDirection = attack.Kind == BossAttackKind.Slam ? boss.Facing : direction;
            boss.AttackLandsAtTick = tick + Math.Max(1, BossTicks(boss, attack.WindupSeconds));
            boss.ChargeReach = attack.Kind == BossAttackKind.Charge ? ReachBeforeWall(boss.AttackOrigin, boss.AttackDirection, attack.Range) : 0f;
            _events.Add(new BossAttackStarted(tick, boss.Id, attack.Kind, boss.AttackOrigin, boss.AttackDirection, boss.AttackLandsAtTick));
        }

        /// <summary>How far along <paramref name="direction"/> from <paramref name="origin"/> the gate's floor goes, up to <paramref name="range"/>.</summary>
        private float ReachBeforeWall(Vec3 origin, Vec3 direction, float range)
        {
            if (_gate == null) return range;
            const float StepLength = 0.1f;
            for (var travelled = StepLength; travelled <= range; travelled += StepLength)
            {
                var point = origin + direction * travelled;
                if (Flat(_gate.Layout.Confine(point) - point).Length > 1e-3f) return travelled - StepLength;
            }
            return range;
        }

        private int BossTicks(Enemy boss, float seconds)
        {
            var percent = boss.Enraged ? boss.Definition.Boss.EnragedTimePercent : 100;
            return SecondsToTicks(seconds * percent / 100f);
        }

        private void LandBossAttack(Enemy boss, int tick)
        {
            var attack = boss.Definition.Boss.Attacks[boss.BossAttack];
            if (attack.Kind == BossAttackKind.Charge)
            {
                // AttackLandsAtTick stays set while it runs: it names this charge (bots dodge each charge once).
                boss.Charging = true;
                boss.ChargeTravelled = 0f;
                boss.ChargeHit.Clear();
                ContinueCharge(boss, tick);
                return;
            }

            foreach (var player in _players)
            {
                if (boss.Health <= 0) return; // a perfect dodge's counter can end it mid-swing
                if (!player.IsAlive || player.Away || !attack.Covers(boss.AttackOrigin, boss.AttackDirection, player.Position)) continue;
                if (attack.Kind == BossAttackKind.Slam && player.IsAirborne)
                {
                    _events.Add(new AttackJumped(tick, boss.Id, player.Id)); // the shockwave runs along the ground, under them
                    continue;
                }
                HitPlayerWithBossAttack(boss, player, attack, tick);
            }
            EndBossAttack(boss, attack, tick);
        }

        /// <summary>One tick of a charge: run along the line, hit anyone in the lane (once each), stop at the end or a wall.</summary>
        private void ContinueCharge(Enemy boss, int tick)
        {
            var attack = boss.Definition.Boss.Attacks[boss.BossAttack];
            if (attack.Speed <= 0f)
            {
                boss.Charging = false; // a charge that cannot move ends where it stands
                EndBossAttack(boss, attack, tick);
                return;
            }
            var step = MathF.Min(attack.Speed * TickDelta, boss.ChargeReach - boss.ChargeTravelled);
            var next = boss.Position + boss.AttackDirection * step;
            var blocked = false;
            if (_gate != null)
            {
                var confined = _gate.Layout.Confine(next);
                blocked = Flat(confined - next).Length > 1e-3f;
                next = confined;
            }
            boss.ChargeTravelled += Flat(next - boss.Position).Length;
            boss.Position = next;

            foreach (var player in _players)
            {
                if (boss.Health <= 0) return; // a perfect dodge's counter can end it mid-charge
                if (!player.IsAlive || player.Away || boss.ChargeHit.Contains(player.Id.Value)) continue;
                if (Flat(player.Position - boss.Position).Length > attack.Radius) continue;
                if (AboveEnemy(player, boss))
                {
                    boss.ChargeHit.Add(player.Id.Value); // cleared it: it passes under them, once
                    _events.Add(new AttackJumped(tick, boss.Id, player.Id));
                    continue;
                }
                boss.ChargeHit.Add(player.Id.Value);
                HitPlayerWithBossAttack(boss, player, attack, tick);
            }

            if (blocked || boss.ChargeTravelled >= boss.ChargeReach - 1e-4f)
            {
                boss.Charging = false;
                EndBossAttack(boss, attack, tick);
            }
        }

        /// <summary>A defeated boss: whatever it was winding up or running is over.</summary>
        private static void StopBoss(Enemy boss)
        {
            boss.AttackLandsAtTick = 0;
            boss.BossAttack = -1;
            boss.Charging = false;
            boss.ChargeHit.Clear();
            boss.ChargeReach = 0f;
            boss.EnragePending = false;
        }

        private void EndBossAttack(Enemy boss, BossAttackDefinition attack, int tick)
        {
            boss.ChargeReach = 0f;
            boss.AttackLandsAtTick = 0;
            boss.BossAttack = -1;
            boss.ChargeHit.Clear();
            boss.AttackReadyAtTick = tick + BossTicks(boss, attack.CooldownSeconds);
        }

        private void HitPlayerWithBossAttack(Enemy boss, Player player, BossAttackDefinition attack, int tick)
        {
            if (player.IsInvulnerable(tick))
            {
                _events.Add(new AttackDodged(tick, boss.Id, player.Id));
                if (player.IsCharging) CounterWithCharge(player, boss, tick); // a perfect dodge
                return;
            }
            var amount = player.TakeDamage(PartyScalingDefinition.Scale(attack.Damage, boss.DamagePercent), out var defeated);
            if (amount == 0) return;
            _events.Add(new PlayerDamaged(tick, boss.Id, player.Id, amount, player.Health));
            if (defeated) _events.Add(new PlayerDefeated(tick, player.Id, boss.Id));
        }

        /// <summary>Turns <paramref name="enemy"/> to face <paramref name="toward"/> at once.</summary>
        private static void FaceTowards(Enemy enemy, Vec3 toward)
        {
            var length = toward.Length;
            if (length >= Epsilon) enemy.Facing = toward * (1f / length);
        }

        /// <summary>Turns <paramref name="enemy"/> towards <paramref name="toward"/> by at most its turn rate for one tick.</summary>
        private void TurnTowards(Enemy enemy, Vec3 toward, float degreesPerSecond)
        {
            var length = toward.Length;
            if (length < Epsilon) return;
            var wanted = MathF.Atan2(toward.X, toward.Z);
            var current = MathF.Atan2(enemy.Facing.X, enemy.Facing.Z);
            var difference = wanted - current;
            while (difference > MathF.PI) difference -= 2f * MathF.PI;
            while (difference < -MathF.PI) difference += 2f * MathF.PI;
            var most = degreesPerSecond * DegreesToRadians * TickDelta;
            var angle = current + Math.Clamp(difference, -most, most);
            enemy.Facing = new Vec3(MathF.Sin(angle), 0f, MathF.Cos(angle));
        }

        /// <summary>Called whenever a boss takes damage: crossing its threshold marks it to enrage (adds arrive after the enemies' turn).</summary>
        private void CheckEnrage(Enemy enemy)
        {
            var boss = enemy.Definition.Boss;
            if (boss == null || enemy.Enraged || enemy.Health <= 0 || boss.EnrageAtHealthPercent <= 0) return;
            if ((long)enemy.Health * 100 > (long)enemy.MaxHealth * boss.EnrageAtHealthPercent) return;
            enemy.Enraged = true;
            enemy.EnragePending = true;
        }

        /// <summary>After everyone has acted: bosses that enraged this tick call their adds (spawning outside any loop over enemies).</summary>
        private void ApplyEnrages(int tick)
        {
            List<Enemy> enraged = null;
            foreach (var enemy in _enemies)
                if (enemy.EnragePending && enemy.Health > 0) (enraged ??= new List<Enemy>()).Add(enemy);
            if (enraged == null) return;

            foreach (var boss in enraged)
            {
                boss.EnragePending = false;
                _events.Add(new BossEnraged(tick, boss.Id));
                // In a gate, its help is scaled for the party there now, like any room's enemies.
                var tier = _gate != null ? CurrentPartyTier() : null;
                foreach (var (add, offset) in Scaled(boss.Definition.Boss.EnrageAdds, tier))
                {
                    var at = boss.Position + offset;
                    if (_gate != null) at = _gate.Layout.Confine(at);
                    var id = SpawnEnemy(add.Enemy, at, tier);
                    JoinBossRoom(boss.Id, id); // so a wipe takes them away with the boss
                }
            }
        }

        /// <summary>
        /// Scripted damage to an enemy, credited to <paramref name="attacker"/> (traps, scripted
        /// events, tests). Applied at the start of the next tick, like other world operations;
        /// nothing happens if either has gone by then.
        /// </summary>
        public void DamageEnemy(EnemyId enemy, PlayerId attacker, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Damage cannot be negative.");
            Enqueue(tick =>
            {
                if (!_enemiesById.TryGetValue(enemy, out var target) || target.Health <= 0) return;
                if (!_playersById.TryGetValue(attacker, out var player)) return;
                DamageEnemy(player, target, amount, tick, null);
            });
        }
    }
}
