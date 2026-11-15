using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>The tags skills and modifiers may use. Anything else is a typo and is rejected.</summary>
    public static class SkillTags
    {
        public const string Melee = "melee";
        public const string Physical = "physical";
        public const string Area = "area";
        public const string Fire = "fire";
        public const string Projectile = "projectile";
        public const string Magic = "magic";
        public const string Summon = "summon";

        public static readonly IReadOnlyList<string> Standard = new[] { Melee, Physical, Area, Fire, Projectile, Magic, Summon };
    }

    /// <summary>
    /// Changes how skills behave. It targets either every skill with a tag (<see cref="AppliesToTag"/>)
    /// or one signature skill (<see cref="AppliesToSkillId"/>, a hand-authored modifier). All of a
    /// player's matching modifiers combine when the skill is used: percentages add, extras add.
    /// Gate clears, gear and skill upgrades grant these; the shared skill data is never changed.
    /// </summary>
    public sealed class ModifierDefinition : LootDefinition
    {

        /// <summary>Applies to every skill carrying this tag. Set this or <see cref="AppliesToSkillId"/>, not both.</summary>
        public string AppliesToTag { get; set; }

        /// <summary>Applies to this one skill only. Set this or <see cref="AppliesToTag"/>, not both.</summary>
        public string AppliesToSkillId { get; set; }

        /// <summary>+50 means 50% more base damage, before stat scaling.</summary>
        public int DamagePercent { get; set; }

        /// <summary>-25 means a 25% shorter cooldown. Cooldowns never drop below 10% of the original.</summary>
        public int CooldownPercent { get; set; }

        /// <summary>Metres added to cone reach and projectile range.</summary>
        public float RangeAdd { get; set; }

        /// <summary>Metres added to area radius and projectile hit radius.</summary>
        public float RadiusAdd { get; set; }

        /// <summary>Projectile skills fire this many more bolts, fanned out evenly.</summary>
        public int ExtraProjectiles { get; set; }

        /// <summary>Angle between neighbouring bolts when <see cref="ExtraProjectiles"/> adds some.</summary>
        public float ProjectileSpreadDegrees { get; set; } = 15f;

        /// <summary>
        /// When above 0, every enemy the skill hits bursts, damaging other enemies within this many
        /// metres of it for <see cref="ExplosionDamagePercent"/> of the hit. Bursts do not chain.
        /// </summary>
        public float ExplosionRadius { get; set; }

        public int ExplosionDamagePercent { get; set; }

        /// <summary>
        /// When set, the modifier counts once for every piece of worn gear carrying this tag, and not
        /// at all with none (ticket 23): with three fire pieces on, +15% damage is +45%. Every number
        /// scales, including extra bolts and the burst's share; the burst's radius does not.
        /// </summary>
        public string PerWornTag { get; set; }

        public bool AppliesTo(SkillDefinition skill) =>
            (AppliesToTag != null && skill.HasTag(AppliesToTag)) ||
            (AppliesToSkillId != null && string.Equals(AppliesToSkillId, skill.Id, StringComparison.Ordinal));

        public override string ToString() => Id;
    }
}
