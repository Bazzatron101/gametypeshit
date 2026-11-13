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

        /// <summary>The bar slot being charged, or null.</summary>
        public readonly int? ChargingSlot;

        /// <summary>How full the current charge is, 0 to 1 (0 when not charging).</summary>
        public readonly float ChargeFraction;

        /// <summary>True while this player's connection is down: their character waits in the run (see <see cref="SimulationWorld.SetAway"/>).</summary>
        public readonly bool IsAway;

        public PlayerState(PlayerId id, Vec3 position, Vec3 facing, int health, int maxHealth,
            PlayerLifeState lifeState, bool isDodging, bool isInvulnerable, bool isWindingUp,
            int level, int xp, int xpToNextLevel, bool isAtLevelCap, int unspentStatPoints, PlayerStats stats,
            IReadOnlyList<SkillSlotState> skillSlots, IReadOnlyList<SkillDefinition> knownSkills,
            IReadOnlyList<ModifierDefinition> activeModifiers, int? chargingSlot = null, float chargeFraction = 0f, bool isAway = false)
        {
            IsAway = isAway;
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
            ChargingSlot = chargingSlot;
            ChargeFraction = chargeFraction;
        }


        public bool IsDefeated => LifeState == PlayerLifeState.Defeated;
    }
}
