using System;
using System.Collections.Generic;
using System.Linq;

namespace HunterSystem.Simulation
{
    /// <summary>Skills: the six-slot bar, learning skills, using them, and projectiles in flight.</summary>
    public sealed partial class SimulationWorld
    {
        public const int SkillSlotCount = 6;

        /// <summary>Projectiles in flight, oldest first.</summary>
        public IReadOnlyList<ProjectileState> Projectiles
        {
            get
            {
                var list = new List<ProjectileState>(_projectiles.Count);
                foreach (var p in _projectiles) list.Add(new ProjectileState(p.Id, p.Owner.Id, p.Skill, p.Position, p.Direction));
                return list;
            }
        }

        /// <summary>Content validation: every skill must be usable as written, or the world refuses to start.</summary>
        private static void ValidateSkills(IReadOnlyList<SkillDefinition> skills, int levelCap, HashSet<string> knownTags)
        {
            if (skills == null) throw new ArgumentException("Skills must be set (it may be empty).", nameof(skills));
            var ids = new HashSet<string>();
            var starters = 0;
            foreach (var skill in skills)
            {
                if (skill == null) throw new ArgumentException("Skills contains a null entry.", nameof(skills));
                void Require(bool ok, string problem)
                {
                    if (!ok) throw new ArgumentException($"Skill '{skill.Id}': {problem}", nameof(skills));
                }

                Require(!string.IsNullOrWhiteSpace(skill.Id), "has no Id.");
                Require(ids.Add(skill.Id), "shares its Id with another skill.");
                Require(!string.IsNullOrWhiteSpace(skill.Name), "has no Name.");
                Require(skill.Tags != null, "Tags must not be null (use an empty list).");
                foreach (var tag in skill.Tags) Require(knownTags.Contains(tag), $"tag '{tag}' is not a known tag.");
                Require(skill.UnlockLevel >= 1 && skill.UnlockLevel <= levelCap, $"UnlockLevel {skill.UnlockLevel} is outside 1 to the level cap ({levelCap}).");
                Require(skill.CooldownSeconds >= 0f, "CooldownSeconds is negative.");
                Require(skill.Damage >= 0, "Damage is negative.");
                Require(skill.Range >= 0f, "Range is negative.");
                Require(skill.Radius >= 0f, "Radius is negative.");
                if (skill.Effect == SkillEffectKind.Cone)
                    Require(skill.ArcDegrees > 0f && skill.ArcDegrees <= 360f, "ArcDegrees must be above 0 and at most 360.");
                Require(skill.ChargeSeconds >= 0f, "ChargeSeconds is negative.");
                Require(skill.ChargedSizePercent >= 0, "ChargedSizePercent is negative.");
                Require(skill.ChargedDamagePercent >= 0, "ChargedDamagePercent is negative.");
                if (skill.CanCharge)
                    Require(skill.ChargedSizePercent > 0 || skill.ChargedDamagePercent > 0, "a charge needs a size or damage bonus.");
                Require(skill.ChargeUnlockLevel >= 1 && skill.ChargeUnlockLevel <= levelCap,
                    $"ChargeUnlockLevel {skill.ChargeUnlockLevel} is outside 1 to the level cap ({levelCap}).");
                if (skill.Effect == SkillEffectKind.Projectile)
                {
                    Require(skill.ProjectileSpeed > 0f, "a projectile needs a positive ProjectileSpeed.");
                    Require(skill.Range > 0f, "a projectile needs a positive Range.");
                }
                if (skill.UnlockLevel == 1) starters++;
            }
            if (starters > SkillSlotCount)
                throw new ArgumentException($"{starters} starter skills cannot fit on a {SkillSlotCount}-slot bar.", nameof(skills));
        }

        private IReadOnlyList<SkillSlotState> SkillBarSnapshot(Player player)
        {
            var slots = new SkillSlotState[SkillSlotCount];
            for (var i = 0; i < SkillSlotCount; i++)
            {
                var skill = player.SkillSlots[i];
                if (skill == null) { slots[i] = new SkillSlotState(null, 0, 0); continue; }
                player.SkillReadyAtTick.TryGetValue(skill.Id, out var ready);
                var cooldown = player.SkillCooldownTicks.TryGetValue(skill.Id, out var c) ? c : SecondsToTicks(skill.CooldownSeconds);
                slots[i] = new SkillSlotState(skill, ready, cooldown, IsChargeUnlocked(player, skill));
            }
            return slots;
        }

        /// <summary>Starters are known from the first tick, quietly: there is nothing to announce yet.</summary>
        private void LearnStarterSkills(Player player)
        {
            foreach (var skill in _config.Skills)
                if (skill.UnlockLevel <= 1) Learn(player, skill);
        }

        /// <summary>Learns every skill whose unlock level is <paramref name="level"/>, announcing each.</summary>
        private void UnlockSkillsForLevel(Player player, int level, int tick)
        {
            foreach (var skill in _config.Skills)
            {
                if (skill.UnlockLevel != level || player.KnownSkills.Contains(skill)) continue;
                var slot = Learn(player, skill);
                _events.Add(new SkillUnlocked(tick, player.Id, skill, slot));
            }
        }

        /// <summary>Adds the skill to the known list and the first empty bar slot. Returns that slot, or null if the bar is full.</summary>
        private static int? Learn(Player player, SkillDefinition skill)
        {
            player.KnownSkills.Add(skill);
            for (var i = 0; i < SkillSlotCount; i++)
            {
                if (player.SkillSlots[i] != null) continue;
                player.SkillSlots[i] = skill;
                return i;
            }
            return null;
        }

        /// <summary>
        /// A player rearranging their bar. Puts a known skill in a slot, swapping with whatever was
        /// there if the skill sat in another slot; a null skill clears the slot. Invalid requests are
        /// ignored: this is player input.
        /// </summary>
        private void TryAssignSkill(Player player, SkillAssignment assignment, int tick)
        {
            var slot = assignment.Slot;
            if (slot < 0 || slot >= SkillSlotCount) return;
            if (player.IsCharging) return; // the bar is locked while a charge is held

            SkillDefinition skill = null;
            if (assignment.SkillId != null)
            {
                skill = player.KnownSkills.FirstOrDefault(s => s.Id == assignment.SkillId);
                if (skill == null) return;
            }
            if (player.SkillSlots[slot] == skill) return;

            var from = skill == null ? -1 : Array.IndexOf(player.SkillSlots, skill);
            var displaced = player.SkillSlots[slot];
            player.SkillSlots[slot] = skill;
            _events.Add(new SkillSlotChanged(tick, player.Id, slot, skill));
            if (from < 0) return;
            player.SkillSlots[from] = displaced; // swap, or leave it empty if the target was
            _events.Add(new SkillSlotChanged(tick, player.Id, from, displaced));
        }

        private static bool IsChargeUnlocked(Player player, SkillDefinition skill) =>
            skill.CanCharge && player.Level >= skill.EffectiveChargeLevel;

        /// <summary>Announces charges that become usable at <paramref name="level"/>, for skills the player knows.</summary>
        private void UnlockChargesForLevel(Player player, int level, int tick)
        {
            foreach (var skill in player.KnownSkills)
                if (skill.ChargeUnlocksSeparatelyAt(level))
                    _events.Add(new SkillChargeUnlocked(tick, player.Id, skill));
        }

        /// <summary>How full a charge is: nothing during the tap grace, then building to 1 over the skill's charge time.</summary>
        private float ChargeFraction(ActiveCharge charge, int tick)
        {
            var full = Math.Max(1, SecondsToTicks(charge.Skill.ChargeSeconds));
            var building = tick - charge.StartTick - SecondsToTicks(_config.ChargeTapGraceSeconds);
            return Math.Clamp((float)building / full, 0f, 1f);
        }

        /// <summary>
        /// The press of a skill key. If the skill can charge (and its charge is unlocked) and the key
        /// is still held, charging starts; otherwise the skill fires now. Returns whether the tick was used.
        /// </summary>
        private bool TryUseSkill(Player player, int slot, bool keyStillHeld, int tick)
        {
            if (slot < 0 || slot >= SkillSlotCount) return false;
            var skill = player.SkillSlots[slot];
            if (skill == null) return false;
            if (player.SkillReadyAtTick.TryGetValue(skill.Id, out var readyAt) && tick < readyAt) return false;

            if (keyStillHeld && IsChargeUnlocked(player, skill))
            {
                player.Charge = new ActiveCharge { Skill = skill, Slot = slot, StartTick = tick };
                _events.Add(new SkillChargeStarted(tick, player.Id, skill, slot));
                return true;
            }

            FireSkill(player, skill, slot, tick, chargeFraction: 0f, counter: false);
            return true;
        }

        /// <summary>The key was let go: fire with the charge built so far.</summary>
        private void ReleaseCharge(Player player, int tick)
        {
            var charge = player.TakeCharge();
            FireSkill(player, charge.Skill, charge.Slot, tick, ChargeFraction(charge, tick), counter: false);
        }

        /// <summary>A perfect dodge while charging: turn to the attacker and fire at full charge.</summary>
        private void CounterWithCharge(Player player, Enemy attacker, int tick)
        {
            var charge = player.TakeCharge();
            var toward = Flat(attacker.Position - player.Position);
            var distance = toward.Length;
            if (distance > Epsilon) player.Facing = toward * (1f / distance);
            FireSkill(player, charge.Skill, charge.Slot, tick, chargeFraction: 1f, counter: true);
        }

        /// <summary>
        /// Fires a skill: modifiers, then the charge bonus (size and damage scaled by
        /// <paramref name="chargeFraction"/>), then the effect. A counter keeps its aim on the attacker
        /// instead of using soft lock-on. The cooldown starts now.
        /// </summary>
        private void FireSkill(Player player, SkillDefinition skill, int slot, int tick, float chargeFraction, bool counter)
        {
            var resolved = Resolve(player, skill);
            if (chargeFraction > 0f)
            {
                var size = 1f + skill.ChargedSizePercent / 100f * chargeFraction;
                if (skill.Effect == SkillEffectKind.Cone) resolved.Range *= size;
                else resolved.Radius *= size;
                var damagePercent = (int)MathF.Round(skill.ChargedDamagePercent * chargeFraction, MidpointRounding.AwayFromZero);
                resolved.BaseDamage = PercentOf(resolved.BaseDamage, 100 + damagePercent);
            }

            player.SkillReadyAtTick[skill.Id] = tick + resolved.CooldownTicks;
            player.SkillCooldownTicks[skill.Id] = resolved.CooldownTicks;
            _events.Add(new SkillUsed(tick, player.Id, skill, slot, resolved.Range, resolved.Radius, chargeFraction, counter));
            var damage = ScaledDamage(player, resolved.BaseDamage, skill.ScalingStat);

            switch (skill.Effect)
            {
                case SkillEffectKind.Cone:
                    if (!counter) ApplySoftLockOn(player, resolved.Range);
                    var minCos = HalfAngleCos(skill.ArcDegrees);
                    HitEnemiesWhere(player, damage, tick, resolved, at => InCone(player, at, resolved.Range, minCos));
                    break;

                case SkillEffectKind.Area:
                    HitEnemiesWhere(player, damage, tick, resolved, at => Flat(at - player.Position).Length <= resolved.Radius);
                    break;

                case SkillEffectKind.Projectile:
                    if (!counter) ApplySoftLockOn(player, resolved.Range);
                    for (var i = 0; i < resolved.ProjectileCount; i++)
                    {
                        // Fan the bolts out evenly around the aim: -s, 0, +s for three.
                        var offset = (i - (resolved.ProjectileCount - 1) * 0.5f) * resolved.SpreadDegrees;
                        _projectiles.Add(new Projectile
                        {
                            Id = _nextProjectileId++,
                            Owner = player,
                            Skill = skill,
                            Resolved = resolved,
                            Damage = damage,
                            Speed = skill.ProjectileSpeed,
                            Position = player.Position,
                            Direction = RotateFlat(player.Facing, offset),
                            LaunchTick = tick,
                        });
                    }
                    break;
            }
        }

        /// <summary>
        /// Lands a skill's direct hit on every live enemy inside its shape, then sets off any bursts.
        /// Targets are chosen first, so a burst cannot change who the skill itself hits.
        /// </summary>
        private void HitEnemiesWhere(Player attacker, int damage, int tick, ResolvedSkill skill, Func<Vec3, bool> inShape)
        {
            var targets = new List<Enemy>();
            foreach (var enemy in _enemies)
                if (enemy.Health > 0 && inShape(enemy.Position)) targets.Add(enemy);

            foreach (var enemy in targets) skill.DirectlyHit.Add(enemy);
            foreach (var enemy in targets) DamageEnemy(attacker, enemy, damage, tick, skill.Definition);
            foreach (var enemy in targets) ExplodeIfModified(attacker, enemy, damage, skill, tick);
        }

        /// <summary>Turns a floor direction by <paramref name="degrees"/> (positive turns right, toward +X from +Z).</summary>
        private static Vec3 RotateFlat(Vec3 direction, float degrees)
        {
            if (degrees == 0f) return direction;
            var r = degrees * MathF.PI / 180f;
            var cos = MathF.Cos(r);
            var sin = MathF.Sin(r);
            return new Vec3(direction.X * cos + direction.Z * sin, 0f, -direction.X * sin + direction.Z * cos);
        }

        /// <summary>Moves each projectile, hitting the first live enemy along this tick's stretch of its path.</summary>
        private void UpdateProjectiles(int tick)
        {
            for (var i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                if (p.LaunchTick == tick) continue; // it leaves the hand this tick and starts flying next tick

                var step = MathF.Min(p.Speed * TickDelta, p.Resolved.Range - p.Travelled);
                var from = p.Position;
                var hit = FirstEnemyAlong(from, p.Direction, step, p.Resolved.Radius, out var along);
                if (hit != null)
                {
                    p.Position = from + p.Direction * along;
                    p.Resolved.DirectlyHit.Add(hit);
                    DamageEnemy(p.Owner, hit, p.Damage, tick, p.Skill);
                    ExplodeIfModified(p.Owner, hit, p.Damage, p.Resolved, tick);
                    EndProjectile(i, p, tick, hitEnemy: true);
                    continue;
                }

                p.Position = from + p.Direction * step;
                p.Travelled += step;
                if (p.Travelled >= p.Resolved.Range - Epsilon) EndProjectile(i, p, tick, hitEnemy: false);
            }
        }

        private void EndProjectile(int index, Projectile p, int tick, bool hitEnemy)
        {
            _projectiles.RemoveAt(index);
            _events.Add(new ProjectileEnded(tick, p.Id, hitEnemy, p.Position));
        }

        /// <summary>
        /// The live enemy nearest the start of a straight segment whose point lies within
        /// <paramref name="radius"/> of it. <paramref name="along"/> is how far along the segment the contact is.
        /// </summary>
        private Enemy FirstEnemyAlong(Vec3 from, Vec3 direction, float length, float radius, out float along)
        {
            Enemy first = null;
            along = float.MaxValue;
            foreach (var enemy in _enemies)
            {
                if (enemy.Health <= 0) continue;
                var offset = Flat(enemy.Position - from);
                var t = Math.Clamp(offset.X * direction.X + offset.Z * direction.Z, 0f, length);
                var closest = direction * t;
                var miss = Flat(offset - closest).Length;
                if (miss > radius) continue;
                // Back up to where the edge of the hit radius first touches the enemy.
                var contact = Math.Max(0f, t - MathF.Sqrt(MathF.Max(0f, radius * radius - miss * miss)));
                if (contact < along) { along = contact; first = enemy; }
            }
            return first;
        }
    }
}
