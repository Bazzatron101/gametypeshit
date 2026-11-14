using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>Authoring asset for one enemy type. The simulation only ever sees the plain <see cref="EnemyDefinition"/>.</summary>
    [CreateAssetMenu(menuName = "Hunter System/Enemy Definition", fileName = "NewEnemy")]
    public sealed class EnemyDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string displayName = "Enemy";
        [SerializeField, Min(1)] private int maxHealth = 30;
        [SerializeField, Min(0f), Tooltip("Metres per second")] private float moveSpeed = 3f;
        [SerializeField, Min(0f), Tooltip("Stops chasing this close to its target, in metres")] private float stopDistance = 1.2f;
        [SerializeField, Min(0.1f), Tooltip("How tall it stands: what a jump must clear to pass over it")] private float height = 1.8f;

        [Header("Attack")]
        [SerializeField, Min(0)] private int attackDamage = 10;
        [SerializeField, Min(0f), Tooltip("Starts an attack when the target is this close; the hit lands only if it still is")] private float attackRange = 1.6f;
        [SerializeField, Min(0f), Tooltip("Telegraph before the hit lands: the player's window to dodge")] private float attackWindupSeconds = 0.5f;
        [SerializeField, Min(0f)] private float attackCooldownSeconds = 1.5f;

        [Header("Reward")]
        [SerializeField, Min(0), Tooltip("XP every player gains when this enemy is defeated")] private int xpReward = 25;

        public EnemyDefinition ToDefinition() => new EnemyDefinition
        {
            Name = displayName,
            MaxHealth = maxHealth,
            MoveSpeed = moveSpeed,
            StopDistance = stopDistance,
            AttackDamage = attackDamage,
            AttackRange = attackRange,
            AttackWindupSeconds = attackWindupSeconds,
            AttackCooldownSeconds = attackCooldownSeconds,
            XpReward = xpReward,
            Height = height,
        };

        /// <summary>For editor tooling that creates definitions in code.</summary>
        public void SetAttackAndReward(int damage, float range, float windupSeconds, float cooldownSeconds, int xpReward)
        {
            attackDamage = damage;
            attackRange = range;
            attackWindupSeconds = windupSeconds;
            attackCooldownSeconds = cooldownSeconds;
            this.xpReward = xpReward;
        }

        /// <summary>For editor tooling that creates definitions in code. Attack values keep their defaults unless set.</summary>
        public void SetStats(string name, int maxHealth, float moveSpeed, float stopDistance)
        {
            displayName = name;
            this.maxHealth = maxHealth;
            this.moveSpeed = moveSpeed;
            this.stopDistance = stopDistance;
        }
    }
}
