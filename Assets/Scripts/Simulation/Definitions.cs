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

    public enum AttackKind
    {
        Light,
        Heavy,
    }

    /// <summary>Data for one attack. Light and heavy attacks share this shape; skills will follow it.</summary>
    public sealed class AttackDefinition
    {
        public int Damage { get; set; } = 10;

        /// <summary>How far the attack reaches from the attacker, in metres.</summary>
        public float Range { get; set; } = 2f;

        /// <summary>Total width of the hit cone, centred on the attacker's facing.</summary>
        public float ArcDegrees { get; set; } = 120f;

        /// <summary>
        /// Time from pressing to the hit landing. 0 hits on the press tick. During a windup the
        /// player is committed: no moving or other attacks, though a dodge cancels it.
        /// </summary>
        public float WindupSeconds { get; set; }

        /// <summary>Time from pressing until this attack can be used again.</summary>
        public float CooldownSeconds { get; set; } = 0.4f;
    }

    /// <summary>
    /// Aim assist applied when an attack starts: the attack turns toward the nearest live enemy
    /// it can reach inside a cone around the player's aim, turning at most half the cone. Aiming
    /// away (outside the cone) overrides it.
    /// </summary>
    public sealed class SoftLockOnDefinition
    {
        public bool Enabled { get; set; } = true;

        /// <summary>Upper limit in metres. The attack's own range also limits it: the assist only turns toward enemies the swing can reach.</summary>
        public float Range { get; set; } = 4f;

        /// <summary>Total width of the assist cone around the aim direction.</summary>
        public float ConeDegrees { get; set; } = 90f;
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
