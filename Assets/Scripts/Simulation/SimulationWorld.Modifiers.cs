using System;
using System.Collections.Generic;
using System.Linq;

namespace HunterSystem.Simulation
{
    /// <summary>Modifiers: granting, revoking, and resolving a player's view of a skill.</summary>
    public sealed partial class SimulationWorld
    {
        /// <summary>
        /// One player's version of a skill at the moment of use: the shared definition plus every
        /// matching modifier that player has. The shared definition is never changed.
        /// </summary>
        private sealed class ResolvedSkill
        {
            public SkillDefinition Definition;
            public int BaseDamage;
            public int CooldownTicks;
            public float Range;
            public float Radius;
            public int ProjectileCount;
            public float SpreadDegrees;
            public float ExplosionRadius;
            public int ExplosionDamagePercent;

            // Per use: every bolt of one press shares these, so an enemy takes at most one direct
            // hit and one burst from a single use, and no burst from a use that hit it directly.
            public readonly HashSet<Enemy> DirectlyHit = new HashSet<Enemy>();
            public readonly HashSet<Enemy> Burned = new HashSet<Enemy>();
        }

        /// <summary>
        /// Gives the player a modifier (from gear, an upgrade, a test arena). It takes effect at the
        /// start of the next tick, with a <see cref="ModifierGranted"/> event, so it is ordered
        /// against commands and reproducible. Granting one the player already has does nothing.
        /// Throws now if the modifier is malformed or targets a tag or skill that does not exist.
        /// </summary>
        public void GrantModifier(PlayerId id, ModifierDefinition modifier)
        {
            var player = FindPlayer(id);
            ValidateModifier(modifier);
            player.PendingModifierChanges.Add((modifier, null));
        }

        /// <summary>Removes a modifier by id at the start of the next tick, with a <see cref="ModifierRevoked"/> event if the player had it.</summary>
        public void RevokeModifier(PlayerId id, string modifierId)
        {
            var player = FindPlayer(id);
            player.PendingModifierChanges.Add((null, modifierId));
        }

        private void ApplyPendingModifierChanges(int tick)
        {
            foreach (var player in _players)
            {
                foreach (var (grant, revokeId) in player.PendingModifierChanges)
                {
                    if (grant != null)
                    {
                        if (player.Modifiers.Any(m => m.Id == grant.Id)) continue;
                        player.Modifiers.Add(grant);
                        _events.Add(new ModifierGranted(tick, player.Id, grant));
                    }
                    else if (player.Modifiers.RemoveAll(m => m.Id == revokeId) > 0)
                    {
                        _events.Add(new ModifierRevoked(tick, player.Id, revokeId));
                    }
                }
                player.PendingModifierChanges.Clear();
            }
        }

        private void ValidateModifier(ModifierDefinition m)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            void Require(bool ok, string problem)
            {
                if (!ok) throw new ArgumentException($"Modifier '{m.Id}': {problem}", nameof(m));
            }

            Require(!string.IsNullOrWhiteSpace(m.Id), "has no Id.");
            Require(!string.IsNullOrWhiteSpace(m.Name), "has no Name.");
            Require((m.AppliesToTag == null) != (m.AppliesToSkillId == null), "must set exactly one of AppliesToTag or AppliesToSkillId.");
            if (m.AppliesToTag != null) Require(_knownTags.Contains(m.AppliesToTag), $"tag '{m.AppliesToTag}' is not a known tag.");
            if (m.AppliesToSkillId != null) Require(_config.Skills.Any(s => s.Id == m.AppliesToSkillId), $"no skill has the id '{m.AppliesToSkillId}'.");
            Require(m.ExtraProjectiles >= 0, "ExtraProjectiles is negative.");
            Require(m.ExplosionRadius >= 0f, "ExplosionRadius is negative.");
            Require(m.ExplosionDamagePercent >= 0, "ExplosionDamagePercent is negative.");
            Require(m.ProjectileSpreadDegrees >= 0f, "ProjectileSpreadDegrees is negative.");
        }

        private ResolvedSkill Resolve(Player player, SkillDefinition skill)
        {
            int damagePercent = 0, cooldownPercent = 0, extraProjectiles = 0, explosionPercent = 0;
            float rangeAdd = 0f, radiusAdd = 0f, spread = 0f, explosionRadius = 0f;

            foreach (var m in player.Modifiers)
            {
                if (!m.AppliesTo(skill)) continue;
                damagePercent += m.DamagePercent;
                cooldownPercent += m.CooldownPercent;
                rangeAdd += m.RangeAdd;
                radiusAdd += m.RadiusAdd;
                if (m.ExtraProjectiles > 0)
                {
                    extraProjectiles += m.ExtraProjectiles;
                    spread = MathF.Max(spread, m.ProjectileSpreadDegrees);
                }
                if (m.ExplosionRadius > 0f)
                {
                    explosionRadius = MathF.Max(explosionRadius, m.ExplosionRadius);
                    explosionPercent += m.ExplosionDamagePercent;
                }
            }

            var cooldownSeconds = skill.CooldownSeconds * Math.Max(10, 100 + cooldownPercent) / 100f;
            return new ResolvedSkill
            {
                Definition = skill,
                BaseDamage = PercentOf(skill.Damage, 100 + damagePercent),
                CooldownTicks = SecondsToTicks(cooldownSeconds),
                Range = MathF.Max(0f, skill.Range + rangeAdd),
                Radius = MathF.Max(0f, skill.Radius + radiusAdd),
                ProjectileCount = 1 + extraProjectiles,
                SpreadDegrees = spread,
                ExplosionRadius = explosionRadius,
                ExplosionDamagePercent = explosionPercent,
            };
        }

        /// <summary><paramref name="percent"/>% of <paramref name="value"/>, rounding halves up, in integers.</summary>
        private static int PercentOf(int value, int percent) => (int)Math.Max(0L, ((long)value * percent + 50) / 100);

        /// <summary>
        /// If the skill bursts, bursts at an enemy it just hit for <paramref name="hitDamage"/>, damaging
        /// other live enemies nearby. An enemy the same use already hit directly or burned is spared,
        /// and bursts never set off more bursts.
        /// </summary>
        private void ExplodeIfModified(Player attacker, Enemy at, int hitDamage, ResolvedSkill skill, int tick)
        {
            if (skill.ExplosionRadius <= 0f) return;
            _events.Add(new Explosion(tick, attacker.Id, skill.Definition, at.Position, skill.ExplosionRadius));
            var damage = PercentOf(hitDamage, skill.ExplosionDamagePercent);
            foreach (var enemy in _enemies)
            {
                if (enemy == at || enemy.Health <= 0 || skill.DirectlyHit.Contains(enemy) || skill.Burned.Contains(enemy)) continue;
                if (Flat(enemy.Position - at.Position).Length > skill.ExplosionRadius) continue;
                skill.Burned.Add(enemy);
                DamageEnemy(attacker, enemy, damage, tick, skill.Definition, secondary: true);
            }
        }
    }
}
