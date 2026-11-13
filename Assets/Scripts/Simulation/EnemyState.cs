namespace HunterSystem.Simulation
{
    /// <summary>Read-only snapshot of one enemy's simulation state.</summary>
    public readonly struct EnemyState
    {
        public readonly EnemyId Id;
        public readonly EnemyDefinition Definition;
        public readonly Vec3 Position;
        public readonly int Health;

        /// <summary>Which way it faces on the floor.</summary>
        public readonly Vec3 Facing;

        /// <summary>A boss's attack being wound up or carried out (a charge), or null. With the fields below, everything needed to draw its telegraph.</summary>
        public readonly BossAttackDefinition CurrentBossAttack;
        public readonly Vec3 AttackOrigin;
        public readonly Vec3 AttackDirection;

        /// <summary>The tick its current attack lands (a charge starts running), or 0.</summary>
        public readonly int AttackLandsAtTick;

        public readonly bool IsCharging;
        public readonly bool IsEnraged;

        /// <summary>A charge's actual length (to the wall, if one is in the way); 0 for other attacks.</summary>
        public readonly float AttackReach;

        public EnemyState(EnemyId id, EnemyDefinition definition, Vec3 position, int health, Vec3 facing = default,
            BossAttackDefinition currentBossAttack = null, Vec3 attackOrigin = default, Vec3 attackDirection = default,
            int attackLandsAtTick = 0, bool isCharging = false, bool isEnraged = false, float attackReach = 0f)
        {
            AttackReach = attackReach;
            Id = id;
            Definition = definition;
            Position = position;
            Health = health;
            Facing = facing;
            CurrentBossAttack = currentBossAttack;
            AttackOrigin = attackOrigin;
            AttackDirection = attackDirection;
            AttackLandsAtTick = attackLandsAtTick;
            IsCharging = isCharging;
            IsEnraged = isEnraged;
        }

        public bool IsBoss => Definition?.Boss != null;

        public bool IsDefeated => Health <= 0;
    }
}
