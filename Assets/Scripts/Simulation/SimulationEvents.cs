namespace HunterSystem.Simulation
{
    /// <summary>Something that happened during a tick. Presentation, tests and (later) the network read these.</summary>
    public abstract class SimulationEvent
    {
        /// <summary>The tick the event happened on (the value of <see cref="SimulationWorld.Tick"/> after that step).</summary>
        public int Tick { get; }

        protected SimulationEvent(int tick) => Tick = tick;
    }

    /// <summary>A player's attack swung (landed its hit frame), whether or not it hit anything.</summary>
    public sealed class AttackPerformed : SimulationEvent
    {
        public PlayerId Player { get; }
        public AttackKind Kind { get; }

        public AttackPerformed(int tick, PlayerId player, AttackKind kind) : base(tick)
        {
            Player = player;
            Kind = kind;
        }
    }

    /// <summary>A player started an attack with a windup (the heavy attack). It swings on <see cref="LandsAtTick"/> unless cancelled.</summary>
    public sealed class AttackWindupStarted : SimulationEvent
    {
        public PlayerId Player { get; }
        public AttackKind Kind { get; }
        public int LandsAtTick { get; }

        public AttackWindupStarted(int tick, PlayerId player, AttackKind kind, int landsAtTick) : base(tick)
        {
            Player = player;
            Kind = kind;
            LandsAtTick = landsAtTick;
        }
    }

    public sealed class DamageDealt : SimulationEvent
    {
        public PlayerId Attacker { get; }
        public EnemyId Target { get; }

        /// <summary>The skill that dealt it, or null for a light or heavy attack.</summary>
        public SkillDefinition Skill { get; }

        /// <summary>True for knock-on damage (an explosion), false for the skill's or attack's own hit.</summary>
        public bool IsSecondary { get; }

        /// <summary>Health actually removed. Never more than the target had left.</summary>
        public int Amount { get; }

        public int RemainingHealth { get; }

        public DamageDealt(int tick, PlayerId attacker, EnemyId target, int amount, int remainingHealth, SkillDefinition skill = null,
            bool isSecondary = false)
            : base(tick)
        {
            Skill = skill;
            IsSecondary = isSecondary;
            Attacker = attacker;
            Target = target;
            Amount = amount;
            RemainingHealth = remainingHealth;
        }
    }

    /// <summary>A player started a dodge. Invulnerability begins this same tick.</summary>
    public sealed class DodgePerformed : SimulationEvent
    {
        public PlayerId Player { get; }

        public DodgePerformed(int tick, PlayerId player) : base(tick) => Player = player;
    }

    /// <summary>An enemy began winding up an attack. This is the telegraph: the hit lands on <see cref="LandsAtTick"/>.</summary>
    public sealed class EnemyAttackStarted : SimulationEvent
    {
        public EnemyId Enemy { get; }
        public PlayerId Target { get; }
        public int LandsAtTick { get; }

        public EnemyAttackStarted(int tick, EnemyId enemy, PlayerId target, int landsAtTick) : base(tick)
        {
            Enemy = enemy;
            Target = target;
            LandsAtTick = landsAtTick;
        }
    }

    public sealed class PlayerDamaged : SimulationEvent
    {
        public EnemyId Attacker { get; }
        public PlayerId Target { get; }

        /// <summary>Health actually removed. Never more than the player had left.</summary>
        public int Amount { get; }

        public int RemainingHealth { get; }

        public PlayerDamaged(int tick, EnemyId attacker, PlayerId target, int amount, int remainingHealth) : base(tick)
        {
            Attacker = attacker;
            Target = target;
            Amount = amount;
            RemainingHealth = remainingHealth;
        }
    }

    /// <summary>An enemy hit would have landed, but the player was inside a dodge's invulnerability window.</summary>
    public sealed class AttackDodged : SimulationEvent
    {
        public EnemyId Attacker { get; }
        public PlayerId Player { get; }

        public AttackDodged(int tick, EnemyId attacker, PlayerId player) : base(tick)
        {
            Attacker = attacker;
            Player = player;
        }
    }

    public sealed class PlayerDefeated : SimulationEvent
    {
        public PlayerId Player { get; }

        /// <summary>The enemy that landed the final hit.</summary>
        public EnemyId DefeatedBy { get; }

        public PlayerDefeated(int tick, PlayerId player, EnemyId defeatedBy) : base(tick)
        {
            Player = player;
            DefeatedBy = defeatedBy;
        }
    }

    /// <summary>A player earned XP. <see cref="Amount"/> is what actually counted (nothing past the level cap).</summary>
    public sealed class XpGained : SimulationEvent
    {
        public PlayerId Player { get; }
        public int Amount { get; }

        /// <summary>The defeated enemy it came from, or null for XP granted directly (quests, the test arena).</summary>
        public EnemyId? Source { get; }

        public XpGained(int tick, PlayerId player, int amount, EnemyId? source) : base(tick)
        {
            Player = player;
            Amount = amount;
            Source = source;
        }
    }

    /// <summary>One level gained. A large XP gain emits one of these per level.</summary>
    public sealed class PlayerLeveledUp : SimulationEvent
    {
        public PlayerId Player { get; }
        public int NewLevel { get; }
        public int StatPointsGranted { get; }

        public PlayerLeveledUp(int tick, PlayerId player, int newLevel, int statPointsGranted) : base(tick)
        {
            Player = player;
            NewLevel = newLevel;
            StatPointsGranted = statPointsGranted;
        }
    }

    public sealed class StatPointAllocated : SimulationEvent
    {
        public PlayerId Player { get; }
        public Stat Stat { get; }
        public int NewValue { get; }

        public StatPointAllocated(int tick, PlayerId player, Stat stat, int newValue) : base(tick)
        {
            Player = player;
            Stat = stat;
            NewValue = newValue;
        }
    }

    public sealed class SkillUsed : SimulationEvent
    {
        public PlayerId Player { get; }
        public SkillDefinition Skill { get; }
        public int Slot { get; }

        /// <summary>Cone reach or projectile range for this use, after modifiers.</summary>
        public float Range { get; }

        /// <summary>Area radius or projectile hit radius for this use, after modifiers.</summary>
        public float Radius { get; }

        /// <summary>0 for a tap, up to 1 for a full charge.</summary>
        public float ChargeFraction { get; }

        /// <summary>True when a perfect dodge launched it at the attacker.</summary>
        public bool IsCounter { get; }

        public SkillUsed(int tick, PlayerId player, SkillDefinition skill, int slot, float range, float radius,
            float chargeFraction = 0f, bool isCounter = false) : base(tick)
        {
            Player = player;
            Skill = skill;
            Slot = slot;
            Range = range;
            Radius = radius;
            ChargeFraction = chargeFraction;
            IsCounter = isCounter;
        }
    }

    /// <summary>A player began charging a skill by holding its key.</summary>
    public sealed class SkillChargeStarted : SimulationEvent
    {
        public PlayerId Player { get; }
        public SkillDefinition Skill { get; }
        public int Slot { get; }

        public SkillChargeStarted(int tick, PlayerId player, SkillDefinition skill, int slot) : base(tick)
        {
            Player = player;
            Skill = skill;
            Slot = slot;
        }
    }

    /// <summary>A skill's charged version became available on level-up (only when later than the skill itself).</summary>
    public sealed class SkillChargeUnlocked : SimulationEvent
    {
        public PlayerId Player { get; }
        public SkillDefinition Skill { get; }

        public SkillChargeUnlocked(int tick, PlayerId player, SkillDefinition skill) : base(tick)
        {
            Player = player;
            Skill = skill;
        }
    }

    /// <summary>
    /// A bar slot's contents changed: <see cref="Skill"/> is what it holds now, or null if empty.
    /// A move or swap emits one of these for each slot it touched.
    /// </summary>
    public sealed class SkillSlotChanged : SimulationEvent
    {
        public PlayerId Player { get; }
        public int Slot { get; }
        public SkillDefinition Skill { get; }

        public SkillSlotChanged(int tick, PlayerId player, int slot, SkillDefinition skill) : base(tick)
        {
            Player = player;
            Slot = slot;
            Skill = skill;
        }
    }

    /// <summary>A modifier came into effect for a player at the start of this tick.</summary>
    public sealed class ModifierGranted : SimulationEvent
    {
        public PlayerId Player { get; }
        public ModifierDefinition Modifier { get; }

        public ModifierGranted(int tick, PlayerId player, ModifierDefinition modifier) : base(tick)
        {
            Player = player;
            Modifier = modifier;
        }
    }

    /// <summary>A modifier stopped applying to a player at the start of this tick.</summary>
    public sealed class ModifierRevoked : SimulationEvent
    {
        public PlayerId Player { get; }
        public string ModifierId { get; }

        public ModifierRevoked(int tick, PlayerId player, string modifierId) : base(tick)
        {
            Player = player;
            ModifierId = modifierId;
        }
    }

    /// <summary>A player learned a skill on level-up. <see cref="Slot"/> is where it went, or null if the bar was full.</summary>
    public sealed class SkillUnlocked : SimulationEvent
    {
        public PlayerId Player { get; }
        public SkillDefinition Skill { get; }
        public int? Slot { get; }

        public SkillUnlocked(int tick, PlayerId player, SkillDefinition skill, int? slot) : base(tick)
        {
            Player = player;
            Skill = skill;
            Slot = slot;
        }
    }

    /// <summary>A skill hit burst at an enemy (from a modifier such as Ember Core).</summary>
    public sealed class Explosion : SimulationEvent
    {
        public PlayerId Player { get; }
        public SkillDefinition Skill { get; }
        public Vec3 Position { get; }
        public float Radius { get; }

        public Explosion(int tick, PlayerId player, SkillDefinition skill, Vec3 position, float radius) : base(tick)
        {
            Player = player;
            Skill = skill;
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>A projectile hit an enemy or reached its range.</summary>
    public sealed class ProjectileEnded : SimulationEvent
    {
        public int Projectile { get; }
        public bool HitEnemy { get; }
        public Vec3 Position { get; }

        public ProjectileEnded(int tick, int projectile, bool hitEnemy, Vec3 position) : base(tick)
        {
            Projectile = projectile;
            HitEnemy = hitEnemy;
            Position = position;
        }
    }

    /// <summary>The party reached a room of the gate and its enemies appeared.</summary>
    public sealed class RoomEntered : SimulationEvent
    {
        public int Room { get; }

        public RoomEntered(int tick, int room) : base(tick) => Room = room;
    }

    /// <summary>Every enemy of a gate room is defeated.</summary>
    public sealed class RoomCleared : SimulationEvent
    {
        public int Room { get; }

        public RoomCleared(int tick, int room) : base(tick) => Room = room;
    }

    /// <summary>A living player reached a checkpoint for the first time this attempt.</summary>
    public sealed class CheckpointActivated : SimulationEvent
    {
        /// <summary>Its number along the gate (see <see cref="GateLayout.Checkpoints"/>).</summary>
        public int Checkpoint { get; }

        public PlayerId Player { get; }

        public CheckpointActivated(int tick, int checkpoint, PlayerId player) : base(tick)
        {
            Checkpoint = checkpoint;
            Player = player;
        }
    }

    /// <summary>A fallen player is back on their feet at a checkpoint (or the entrance) of the gate.</summary>
    public sealed class PlayerRevived : SimulationEvent
    {
        /// <summary><see cref="RevivesLeft"/> for a co-op revive, which uses none up.</summary>
        public const int NoLimit = -1;

        public PlayerId Player { get; }

        /// <summary>The checkpoint they came back at, or -1 for the entrance.</summary>
        public int Checkpoint { get; }

        /// <summary>Solo revives left after this one, or <see cref="NoLimit"/> when a teammate brought them back.</summary>
        public int RevivesLeft { get; }

        public PlayerRevived(int tick, PlayerId player, int checkpoint, int revivesLeft) : base(tick)
        {
            Player = player;
            Checkpoint = checkpoint;
            RevivesLeft = revivesLeft;
        }
    }

    /// <summary>Co-op: the whole party was down. Everyone is back at a checkpoint (-1: the entrance) and unfinished rooms start over.</summary>
    public sealed class GateWiped : SimulationEvent
    {
        public int Checkpoint { get; }

        public GateWiped(int tick, int checkpoint) : base(tick) => Checkpoint = checkpoint;
    }

    /// <summary>Solo, out of revives: the gate starts again from the entrance.</summary>
    public sealed class GateRestarted : SimulationEvent
    {
        /// <summary>Which attempt is starting (2 for the first restart).</summary>
        public int Attempt { get; }

        public GateRestarted(int tick, int attempt) : base(tick) => Attempt = attempt;
    }

    /// <summary>Every room of the gate is cleared.</summary>
    public sealed class GateCleared : SimulationEvent
    {
        public GateCleared(int tick) : base(tick)
        {
        }
    }

    public sealed class EnemyDefeated : SimulationEvent
    {
        public EnemyId Enemy { get; }

        /// <summary>The player who landed the final hit.</summary>
        public PlayerId DefeatedBy { get; }

        public EnemyDefeated(int tick, EnemyId enemy, PlayerId defeatedBy) : base(tick)
        {
            Enemy = enemy;
            DefeatedBy = defeatedBy;
        }
    }
}
