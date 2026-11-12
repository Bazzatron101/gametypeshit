using System;
using System.Linq;

namespace HunterSystem.Simulation
{
    /// <summary>How a skill delivers its damage. New skills combine these with different numbers and tags.</summary>
    public enum SkillEffectKind
    {
        /// <summary>A wide swing in front of the player: <see cref="SkillDefinition.Range"/> and <see cref="SkillDefinition.ArcDegrees"/>.</summary>
        Cone,

        /// <summary>Hits everything within <see cref="SkillDefinition.Radius"/> of the player, in every direction.</summary>
        Area,

        /// <summary>
        /// A bolt that flies along the player's facing at <see cref="SkillDefinition.ProjectileSpeed"/>, hits the first
        /// enemy within <see cref="SkillDefinition.Radius"/> of its path, and fizzles after <see cref="SkillDefinition.Range"/>.
        /// </summary>
        Projectile,
    }

    /// <summary>
    /// One skill, as data. Skills are governed only by their own cooldown: no mana, no stamina, no
    /// shared cooldown. Tags describe the skill for modifiers (ticket 08) and are free-form strings.
    /// </summary>
    public sealed class SkillDefinition
    {
        /// <summary>Stable identifier, unique across all skills (saves and modifiers refer to it).</summary>
        public string Id { get; set; } = "";

        public string Name { get; set; } = "";
        public float CooldownSeconds { get; set; } = 5f;

        /// <summary>The level at which the skill is learned. 1 means a starter skill.</summary>
        public int UnlockLevel { get; set; } = 1;

        public string[] Tags { get; set; } = Array.Empty<string>();

        /// <summary>The stat whose points above the starting value raise this skill's damage.</summary>
        public Stat ScalingStat { get; set; } = Stat.Strength;

        public SkillEffectKind Effect { get; set; } = SkillEffectKind.Cone;
        public int Damage { get; set; } = 20;

        /// <summary>Cone reach, or how far a projectile flies, in metres.</summary>
        public float Range { get; set; } = 3f;

        /// <summary>Cone width in degrees.</summary>
        public float ArcDegrees { get; set; } = 120f;

        /// <summary>Area radius, or a projectile's hit radius, in metres.</summary>
        public float Radius { get; set; } = 3f;

        /// <summary>Projectile speed in metres per second.</summary>
        public float ProjectileSpeed { get; set; } = 15f;

        /// <summary>
        /// Seconds of holding the key for a full charge, or 0 if the skill cannot be charged. A tap
        /// always fires the normal skill; holding charges it and releasing fires the charged version.
        /// </summary>
        public float ChargeSeconds { get; set; }

        /// <summary>At full charge: +100 doubles the area radius, cone reach or projectile hit radius. Scales with the charge.</summary>
        public int ChargedSizePercent { get; set; }

        /// <summary>At full charge: +100 doubles the base damage. Scales with the charge.</summary>
        public int ChargedDamagePercent { get; set; }

        /// <summary>
        /// The level at which the charge becomes available. Below <see cref="UnlockLevel"/> it simply
        /// comes with the skill.
        /// </summary>
        public int ChargeUnlockLevel { get; set; } = 1;

        public bool CanCharge => ChargeSeconds > 0f;

        /// <summary>The level at which this skill's charge is usable: never before the skill itself.</summary>
        public int EffectiveChargeLevel => Math.Max(UnlockLevel, ChargeUnlockLevel);

        /// <summary>True if the charge arrives on its own at <paramref name="level"/>, after the skill was learned (worth announcing).</summary>
        public bool ChargeUnlocksSeparatelyAt(int level) => CanCharge && ChargeUnlockLevel > UnlockLevel && ChargeUnlockLevel == level;

        public bool HasTag(string tag) => Tags != null && Tags.Contains(tag, StringComparer.OrdinalIgnoreCase);

        public override string ToString() => Id;
    }

    /// <summary>A request to put a known skill in a bar slot (or clear it, with a null id).</summary>
    public readonly struct SkillAssignment
    {
        public readonly int Slot;

        /// <summary>The skill to place, or null to empty the slot.</summary>
        public readonly string SkillId;

        public SkillAssignment(int slot, string skillId)
        {
            Slot = slot;
            SkillId = skillId;
        }
    }

    /// <summary>One slot of a player's six-slot skill bar.</summary>
    public readonly struct SkillSlotState
    {
        /// <summary>The skill in this slot, or null if the slot is empty.</summary>
        public readonly SkillDefinition Skill;

        /// <summary>The first tick on which the skill can be used again.</summary>
        public readonly int ReadyAtTick;

        /// <summary>Length of the cooldown last started, in ticks, after modifiers (for drawing progress).</summary>
        public readonly int CooldownTicks;

        /// <summary>True when the skill can be charged and its charge is unlocked at the player's level.</summary>
        public readonly bool ChargeUnlocked;

        public SkillSlotState(SkillDefinition skill, int readyAtTick, int cooldownTicks, bool chargeUnlocked = false)
        {
            Skill = skill;
            ReadyAtTick = readyAtTick;
            CooldownTicks = cooldownTicks;
            ChargeUnlocked = chargeUnlocked;
        }

        public bool IsEmpty => Skill == null;

        /// <summary>Ticks to wait before the skill can be used, counting the next step as the first chance.</summary>
        public int TicksUntilReady(int currentTick) => Math.Max(0, ReadyAtTick - (currentTick + 1));
    }

    /// <summary>Read-only snapshot of a projectile in flight.</summary>
    public readonly struct ProjectileState
    {
        public readonly int Id;
        public readonly PlayerId Owner;
        public readonly SkillDefinition Skill;
        public readonly Vec3 Position;
        public readonly Vec3 Direction;

        public ProjectileState(int id, PlayerId owner, SkillDefinition skill, Vec3 position, Vec3 direction)
        {
            Id = id;
            Owner = owner;
            Skill = skill;
            Position = position;
            Direction = direction;
        }
    }
}
