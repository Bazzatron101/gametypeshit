using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// Where a player is in the life cycle. Only Alive and Defeated for now; the death rules
    /// (checkpoint revives, co-op revive-or-respawn) will add states such as downed or respawning.
    /// </summary>
    public enum PlayerLifeState
    {
        Alive,
        Defeated,
    }

    /// <summary>Read-only snapshot of one player's simulation state.</summary>
    public readonly struct PlayerState
    {
        public readonly PlayerId Id;
        public readonly Vec3 Position;

        /// <summary>Unit vector on the floor plane: the direction the player last moved. Attacks go this way.</summary>
        public readonly Vec3 Facing;

        public readonly int Health;
        public readonly int MaxHealth;
        public readonly PlayerLifeState LifeState;

        /// <summary>Mid-dash: steering and attacks are locked out.</summary>
        public readonly bool IsDodging;

        /// <summary>Damage is ignored this tick (dodge invulnerability frames).</summary>
        public readonly bool IsInvulnerable;

        /// <summary>Committed to an attack windup (the heavy attack): no moving or other attacks until it swings.</summary>
        public readonly bool IsWindingUp;

        public readonly int Level;

        /// <summary>XP earned toward the next level (resets on each level-up; 0 at the cap).</summary>
        public readonly int Xp;

        /// <summary>XP needed to reach the next level from the start of this one; 0 at the cap.</summary>
        public readonly int XpToNextLevel;

        public readonly bool IsAtLevelCap;
        public readonly int UnspentStatPoints;
        public readonly PlayerStats Stats;

        /// <summary>The six-slot skill bar (keys 1 to 6).</summary>
        public readonly IReadOnlyList<SkillSlotState> SkillSlots;

        /// <summary>Every skill learned, in learning order, slotted or not.</summary>
        public readonly IReadOnlyList<SkillDefinition> KnownSkills;

        /// <summary>Modifiers in effect, in the order they were granted.</summary>
        public readonly IReadOnlyList<ModifierDefinition> ActiveModifiers;

        public PlayerState(PlayerId id, Vec3 position, Vec3 facing, int health, int maxHealth,
            PlayerLifeState lifeState, bool isDodging, bool isInvulnerable, bool isWindingUp,
            int level, int xp, int xpToNextLevel, bool isAtLevelCap, int unspentStatPoints, PlayerStats stats,
            IReadOnlyList<SkillSlotState> skillSlots, IReadOnlyList<SkillDefinition> knownSkills,
            IReadOnlyList<ModifierDefinition> activeModifiers)
        {
            Id = id;
            Position = position;
            Facing = facing;
            Health = health;
            MaxHealth = maxHealth;
            LifeState = lifeState;
            IsDodging = isDodging;
            IsInvulnerable = isInvulnerable;
            IsWindingUp = isWindingUp;
            Level = level;
            Xp = xp;
            XpToNextLevel = xpToNextLevel;
            IsAtLevelCap = isAtLevelCap;
            UnspentStatPoints = unspentStatPoints;
            Stats = stats;
            SkillSlots = skillSlots;
            KnownSkills = knownSkills;
            ActiveModifiers = activeModifiers;
        }


        public bool IsDefeated => LifeState == PlayerLifeState.Defeated;
    }
}
