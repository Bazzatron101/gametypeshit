namespace HunterSystem.Simulation
{
    /// <summary>Read-only snapshot of one enemy's simulation state.</summary>
    public readonly struct EnemyState
    {
        public readonly EnemyId Id;
        public readonly EnemyDefinition Definition;
        public readonly Vec3 Position;
        public readonly int Health;

        public EnemyState(EnemyId id, EnemyDefinition definition, Vec3 position, int health)
        {
            Id = id;
            Definition = definition;
            Position = position;
            Health = health;
        }

        public bool IsDefeated => Health <= 0;
    }
}
