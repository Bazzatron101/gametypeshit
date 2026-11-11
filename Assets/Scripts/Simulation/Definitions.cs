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

        public int AttackDamage { get; set; } = 10;

        /// <summary>The enemy starts an attack when its target is this close, and the hit lands only if the target still is.</summary>
        public float AttackRange { get; set; } = 1.6f;

        /// <summary>Telegraph time between starting an attack and the hit landing. This is the player's chance to dodge.</summary>
        public float AttackWindupSeconds { get; set; } = 0.5f;

        /// <summary>Time after a hit lands (or misses) before the next attack can start.</summary>
        public float AttackCooldownSeconds { get; set; } = 1.5f;
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

    /// <summary>The player's dodge: a short dash with invulnerability frames.</summary>
    public sealed class DodgeDefinition
    {
        /// <summary>How far the dash carries the player, in metres.</summary>
        public float Distance { get; set; } = 3f;

        /// <summary>How long the dash lasts. The player cannot steer or attack until it ends.</summary>
        public float DurationSeconds { get; set; } = 0.25f;

        /// <summary>How long damage is ignored, starting on the tick the dodge begins.</summary>
        public float InvulnerabilitySeconds { get; set; } = 0.3f;

        public float CooldownSeconds { get; set; } = 0.8f;
    }
}
