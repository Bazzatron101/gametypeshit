using System;
using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Authoring asset for a handcrafted boss (ticket 18): its body, its telegraphed attacks, and
    /// its enrage. The simulation sees a plain <see cref="EnemyDefinition"/> with
    /// <see cref="EnemyDefinition.Boss"/> set. A boss is placed in a gate by a room piece.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunter System/Boss Definition", fileName = "NewBoss")]
    public sealed class BossDefinitionAsset : ScriptableObject
    {
        [Serializable]
        public struct Attack
        {
            public BossAttackKind kind;
            [Min(0)] public int damage;
            [Min(0.1f), Tooltip("The telegraph: time between the area being marked and the hit")] public float windupSeconds;
            [Min(0f)] public float cooldownSeconds;
            [Min(0f), Tooltip("Sweep: reach. Charge: how far it runs.")] public float range;
            [Range(10f, 360f), Tooltip("Sweep: the arc's full width")] public float arcDegrees;
            [Min(0f), Tooltip("Slam: the circle's radius. Charge: half the lane's width.")] public float radius;
            [Min(0f), Tooltip("Charge: metres per second (a charge needs some)")] public float speed;
        }

        [Serializable]
        public struct Add
        {
            public EnemyDefinitionAsset enemy;
            [Tooltip("From the boss, in metres")] public Vector3 offset;
        }

        [SerializeField] private string displayName = "Boss";
        [SerializeField, Min(1)] private int maxHealth = 600;
        [SerializeField, Min(0f)] private float moveSpeed = 2f;
        [SerializeField, Min(0f)] private float stopDistance = 1.8f;
        [SerializeField, Min(0)] private int xpReward = 300;
        [SerializeField, Min(0.1f), Tooltip("How tall it stands: what a jump must clear to pass over its charge")] private float height = 3.5f;
        [SerializeField, Min(1f), Tooltip("Degrees per second. Slow enough that getting behind it is an answer.")] private float turnDegreesPerSecond = 60f;
        [SerializeField, Tooltip("At most one of each kind")] private Attack[] attacks = new Attack[0];

        [Header("Enrage")]
        [SerializeField, Range(0, 100), Tooltip("Enrages once at this percentage of its health (0: never)")] private int enrageAtHealthPercent = 50;
        [SerializeField, Range(10, 100), Tooltip("Enraged, windups and cooldowns take this percentage of their usual time")] private int enragedTimePercent = 70;
        [SerializeField, Tooltip("Enemies that join when it enrages")] private Add[] enrageAdds = new Add[0];

        public string DisplayName => displayName;
        public IReadOnlyList<Attack> Attacks => attacks;
        public IReadOnlyList<Add> EnrageAdds => enrageAdds;

        /// <param name="enemies">Shared definitions for its adds, so snapshots write each enemy type once.</param>
        public EnemyDefinition ToDefinition(Dictionary<ScriptableObject, EnemyDefinition> enemies)
        {
            var boss = new BossDefinition
            {
                TurnDegreesPerSecond = turnDegreesPerSecond,
                EnrageAtHealthPercent = enrageAtHealthPercent,
                EnragedTimePercent = enragedTimePercent,
            };
            foreach (var a in attacks)
            {
                boss.Attacks.Add(new BossAttackDefinition
                {
                    Kind = a.kind, Damage = a.damage, WindupSeconds = a.windupSeconds, CooldownSeconds = a.cooldownSeconds,
                    Range = a.range, ArcDegrees = a.arcDegrees, Radius = a.radius, Speed = a.speed,
                });
            }
            foreach (var add in enrageAdds)
            {
                if (add.enemy == null) { Debug.LogWarning($"{name}: an add with no enemy was skipped.", this); continue; }
                boss.EnrageAdds.Add(new RoomSpawn(RoomPieceAsset.Shared(enemies, add.enemy), new Vec3(add.offset.x, 0f, add.offset.z)));
            }
            return new EnemyDefinition
            {
                Name = displayName, MaxHealth = maxHealth, MoveSpeed = moveSpeed, StopDistance = stopDistance, XpReward = xpReward,
                AttackDamage = 0, AttackRange = 0f, Boss = boss, Height = height,
            };
        }

        /// <summary>For editor tooling that creates bosses in code.</summary>
        public void Set(string name, int health, float speed, float stop, int xp, Attack[] bossAttacks, Add[] adds,
            int enrageAt = 50, int enragedTime = 70, float turnRate = 60f)
        {
            displayName = name;
            maxHealth = health;
            moveSpeed = speed;
            stopDistance = stop;
            xpReward = xp;
            attacks = bossAttacks ?? new Attack[0];
            enrageAdds = adds ?? new Add[0];
            enrageAtHealthPercent = enrageAt;
            enragedTimePercent = enragedTime;
            turnDegreesPerSecond = turnRate;
        }
    }
}
