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

        /// <summary>The character's own stats: starting values, class stats and the points the player spent. Saved with the character.</summary>
        public readonly PlayerStats Stats;

        /// <summary>What worn gear adds to each stat right now (ticket 51); negative for a loss. Never saved.</summary>
        public PlayerStats GearStats { get; }

        /// <summary>A stat as it counts in play: the character's own plus worn gear's.</summary>
        public int TotalStat(Stat stat) => Stats[stat] + GearStats[stat];

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

        /// <summary>In the hub: this player has marked themselves ready for the next gate.</summary>
        public readonly bool IsReady;

        /// <summary>In a jump: <see cref="Position"/>.Y is how high.</summary>
        public readonly bool IsAirborne;

        public readonly bool IsSprinting;

        /// <summary>The character's class, or null for one from before classes.</summary>
        public readonly ClassDefinition Class;

        /// <summary>How high this player jumps, as a percentage of the base jump.</summary>
        public readonly int JumpHeightPercent;

        /// <summary>Gold from selling duplicates.</summary>
        public readonly int Gold;

        /// <summary>Duplicates to sell, one entry per copy.</summary>
        public readonly IReadOnlyList<LootDefinition> Duplicates;

        /// <summary>Every piece of gear the character has, worn or not, in the order found (ticket 23).</summary>
        public readonly IReadOnlyList<GearDefinition> Bag;

        /// <summary>The piece worn in each <see cref="GearSlot"/> (index by slot), or null.</summary>
        public readonly IReadOnlyList<GearDefinition> Worn;

        /// <summary>The hunter's name (ticket 28), or null.</summary>
        public readonly string Name;

        /// <summary>How the hunter looks (ticket 28).</summary>
        public readonly CharacterLook Look;

        /// <summary>The character owns a mount (ticket 42).</summary>
        public bool HasMount { get; }

        /// <summary>Riding it now: faster, and no fighting from the saddle.</summary>
        public bool IsMounted { get; }

        /// <summary>The specialties picked (ticket 26), one per tier, lowest first.</summary>
        public IReadOnlyList<SpecialtyDefinition> Specialties { get; }

        /// <summary>The ranks whose class trial this hunter has passed (ticket 26).</summary>
        public IReadOnlyList<string> PassedTrials { get; }

        /// <summary>The rank of the tier whose pick is waiting (its trial passed, nothing picked there yet), or null.</summary>
        public string SpecialtyPickWaiting { get; }

        /// <summary>A pick is waiting.</summary>
        public bool CanChooseSpecialty => SpecialtyPickWaiting != null;

        public PlayerState(PlayerId id, Vec3 position, Vec3 facing, int health, int maxHealth,
            PlayerLifeState lifeState, bool isDodging, bool isInvulnerable, bool isWindingUp,
            int level, int xp, int xpToNextLevel, bool isAtLevelCap, int unspentStatPoints, PlayerStats stats,
            IReadOnlyList<SkillSlotState> skillSlots, IReadOnlyList<SkillDefinition> knownSkills,
            IReadOnlyList<ModifierDefinition> activeModifiers, int? chargingSlot = null, float chargeFraction = 0f, bool isAway = false, bool isReady = false, bool isAirborne = false, bool isSprinting = false,
            ClassDefinition characterClass = null, int jumpHeightPercent = 100, int gold = 0, IReadOnlyList<LootDefinition> duplicates = null,
            IReadOnlyList<GearDefinition> bag = null, IReadOnlyList<GearDefinition> worn = null, string name = null, CharacterLook look = default,
            bool hasMount = false, bool isMounted = false,
            IReadOnlyList<SpecialtyDefinition> specialties = null, IReadOnlyList<string> passedTrials = null, string specialtyPickWaiting = null,
            PlayerStats gearStats = default)
        {
            GearStats = gearStats;
            Specialties = specialties ?? System.Array.Empty<SpecialtyDefinition>();
            PassedTrials = passedTrials ?? System.Array.Empty<string>();
            SpecialtyPickWaiting = specialtyPickWaiting;
            HasMount = hasMount;
            IsMounted = isMounted;
            Name = name;
            Look = look;
            Gold = gold;
            Duplicates = duplicates ?? System.Array.Empty<LootDefinition>();
            Bag = bag ?? System.Array.Empty<GearDefinition>();
            Worn = worn ?? new GearDefinition[GearDefinition.SlotCount];
            Class = characterClass;
            JumpHeightPercent = jumpHeightPercent;
            IsAirborne = isAirborne;
            IsSprinting = isSprinting;
            IsReady = isReady;
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
