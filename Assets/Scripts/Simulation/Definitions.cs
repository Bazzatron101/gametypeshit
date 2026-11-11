namespace HunterSystem.Simulation
{
    /// <summary>Data for one enemy type. Content is data, not code: new enemies are new definitions.</summary>
    public sealed class EnemyDefinition
    {
        public string Name { get; set; } = "Enemy";
        public int MaxHealth { get; set; } = 30;

        /// <summary>Chase speed in metres per second.</summary>
        public float MoveSpeed { get; set; } = 3f;

        /// <summary>The enemy stops chasing once it is this close to its target, in metres.</summary>
        public float StopDistance { get; set; } = 1.2f;
    }

    /// <summary>Data for one attack. The light attack is the first; skills will follow the same shape.</summary>
    public sealed class AttackDefinition
    {
        public int Damage { get; set; } = 10;

        /// <summary>How far the attack reaches from the attacker, in metres.</summary>
        public float Range { get; set; } = 2f;

        /// <summary>Total width of the hit cone, centred on the attacker's facing.</summary>
        public float ArcDegrees { get; set; } = 120f;

        public float CooldownSeconds { get; set; } = 0.4f;
    }
}
