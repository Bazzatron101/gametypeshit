namespace HunterSystem.Simulation
{
    /// <summary>Tunable simulation values. Configuration, not code constants.</summary>
    public sealed class SimulationConfig
    {
        /// <summary>Simulation ticks per second.</summary>
        public int TickRate { get; set; } = 60;

        /// <summary>Player movement speed in metres per second at full input.</summary>
        public float PlayerMoveSpeed { get; set; } = 5f;
    }
}
