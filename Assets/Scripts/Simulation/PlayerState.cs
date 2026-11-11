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

        public PlayerState(PlayerId id, Vec3 position, Vec3 facing, int health, int maxHealth,
            PlayerLifeState lifeState, bool isDodging, bool isInvulnerable)
        {
            Id = id;
            Position = position;
            Facing = facing;
            Health = health;
            MaxHealth = maxHealth;
            LifeState = lifeState;
            IsDodging = isDodging;
            IsInvulnerable = isInvulnerable;
        }

        public bool IsDefeated => LifeState == PlayerLifeState.Defeated;
    }
}
