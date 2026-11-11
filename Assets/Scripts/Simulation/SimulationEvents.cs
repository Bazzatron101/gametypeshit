namespace HunterSystem.Simulation
{
    /// <summary>Something that happened during a tick. Presentation, tests and (later) the network read these.</summary>
    public abstract class SimulationEvent
    {
        /// <summary>The tick the event happened on (the value of <see cref="SimulationWorld.Tick"/> after that step).</summary>
        public int Tick { get; }

        protected SimulationEvent(int tick) => Tick = tick;
    }

    /// <summary>A player swung a light attack, whether or not it hit anything.</summary>
    public sealed class AttackPerformed : SimulationEvent
    {
        public PlayerId Player { get; }

        public AttackPerformed(int tick, PlayerId player) : base(tick) => Player = player;
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
