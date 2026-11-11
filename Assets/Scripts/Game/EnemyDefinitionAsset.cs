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

        public EnemyDefinition ToDefinition() => new EnemyDefinition
        {
            Name = displayName,
            MaxHealth = maxHealth,
            MoveSpeed = moveSpeed,
            StopDistance = stopDistance,
        };

        /// <summary>For editor tooling that creates definitions in code.</summary>
        public void SetStats(string name, int maxHealth, float moveSpeed, float stopDistance)
        {
            displayName = name;
            this.maxHealth = maxHealth;
            this.moveSpeed = moveSpeed;
            this.stopDistance = stopDistance;
        }
    }
}
