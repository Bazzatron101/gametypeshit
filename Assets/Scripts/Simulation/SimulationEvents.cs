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

        /// <summary>Health actually removed. Never more than the target had left.</summary>
        public int Amount { get; }

        public int RemainingHealth { get; }

        public DamageDealt(int tick, PlayerId attacker, EnemyId target, int amount, int remainingHealth) : base(tick)
        {
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
        public EnemyId Source { get; }

        public XpGained(int tick, PlayerId player, int amount, EnemyId source) : base(tick)
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
