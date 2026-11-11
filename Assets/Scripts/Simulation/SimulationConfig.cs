namespace HunterSystem.Simulation
{
    /// <summary>Tunable simulation values. Configuration, not code constants.</summary>
    public sealed class SimulationConfig
    {
        /// <summary>Simulation ticks per second.</summary>
        public int TickRate { get; set; } = 60;

        /// <summary>Player movement speed in metres per second at full input.</summary>
        public float PlayerMoveSpeed { get; set; } = 5f;

        public int PlayerMaxHealth { get; set; } = 100;

        public AttackDefinition LightAttack { get; set; } = new AttackDefinition();

        public AttackDefinition HeavyAttack { get; set; } = new AttackDefinition
        {
            Damage = 25, Range = 2.5f, ArcDegrees = 90f, WindupSeconds = 0.35f, CooldownSeconds = 1f,
        };

        public SoftLockOnDefinition SoftLockOn { get; set; } = new SoftLockOnDefinition();

        public DodgeDefinition Dodge { get; set; } = new DodgeDefinition();

        public ProgressionDefinition Progression { get; set; } = new ProgressionDefinition();

        public StatEffectsDefinition StatEffects { get; set; } = new StatEffectsDefinition();
    }
}
