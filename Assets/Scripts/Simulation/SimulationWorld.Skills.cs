using System;
using System.Collections.Generic;

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
        private static void ValidateSkills(IReadOnlyList<SkillDefinition> skills, int levelCap)
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
                Require(skill.UnlockLevel >= 1 && skill.UnlockLevel <= levelCap, $"UnlockLevel {skill.UnlockLevel} is outside 1 to the level cap ({levelCap}).");
                Require(skill.CooldownSeconds >= 0f, "CooldownSeconds is negative.");
                Require(skill.Damage >= 0, "Damage is negative.");
                Require(skill.Range >= 0f, "Range is negative.");
                Require(skill.Radius >= 0f, "Radius is negative.");
                if (skill.Effect == SkillEffectKind.Cone)
                    Require(skill.ArcDegrees > 0f && skill.ArcDegrees <= 360f, "ArcDegrees must be above 0 and at most 360.");
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
                slots[i] = new SkillSlotState(skill, skill != null && player.SkillReadyAtTick.TryGetValue(skill.Id, out var ready) ? ready : 0);
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

        /// <summary>Uses the skill in <paramref name="slot"/> if there is one and it is off cooldown. Returns whether it fired.</summary>
        private bool TryUseSkill(Player player, int slot, int tick)
        {
            if (slot < 0 || slot >= SkillSlotCount) return false;
            var skill = player.SkillSlots[slot];
            if (skill == null) return false;
            if (player.SkillReadyAtTick.TryGetValue(skill.Id, out var readyAt) && tick < readyAt) return false;

            player.SkillReadyAtTick[skill.Id] = tick + SecondsToTicks(skill.CooldownSeconds);
            _events.Add(new SkillUsed(tick, player.Id, skill, slot));
            var damage = ScaledDamage(player, skill.Damage, skill.ScalingStat);

            switch (skill.Effect)
            {
                case SkillEffectKind.Cone:
                    ApplySoftLockOn(player, skill.Range);
                    var minCos = HalfAngleCos(skill.ArcDegrees);
                    DamageEnemiesWhere(player, damage, tick, skill, at => InCone(player, at, skill.Range, minCos));
                    break;

                case SkillEffectKind.Area:
                    DamageEnemiesWhere(player, damage, tick, skill, at => Flat(at - player.Position).Length <= skill.Radius);
                    break;

                case SkillEffectKind.Projectile:
                    ApplySoftLockOn(player, skill.Range);
                    _projectiles.Add(new Projectile
                    {
                        Id = _nextProjectileId++,
                        Owner = player,
                        Skill = skill,
                        Damage = damage,
                        Speed = skill.ProjectileSpeed,
                        Range = skill.Range,
                        HitRadius = skill.Radius,
                        Position = player.Position,
                        Direction = player.Facing,
                        LaunchTick = tick,
                    });
                    break;
            }
            return true;
        }

        /// <summary>Moves each projectile, hitting the first live enemy along this tick's stretch of its path.</summary>
        private void UpdateProjectiles(int tick)
        {
            for (var i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                if (p.LaunchTick == tick) continue; // it leaves the hand this tick and starts flying next tick

                var step = MathF.Min(p.Speed * TickDelta, p.Range - p.Travelled);
                var from = p.Position;
                var hit = FirstEnemyAlong(from, p.Direction, step, p.HitRadius, out var along);
                if (hit != null)
                {
                    p.Position = from + p.Direction * along;
                    DamageEnemy(p.Owner, hit, p.Damage, tick, p.Skill);
                    EndProjectile(i, p, tick, hitEnemy: true);
                    continue;
                }

                p.Position = from + p.Direction * step;
                p.Travelled += step;
                if (p.Travelled >= p.Range - Epsilon) EndProjectile(i, p, tick, hitEnemy: false);
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
