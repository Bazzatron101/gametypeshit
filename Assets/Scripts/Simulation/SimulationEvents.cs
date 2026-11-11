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
