using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// The player's core combat numbers: tick rate, health, movement, light and heavy attacks, the
    /// dodge and soft lock-on. One asset, listed in <see cref="GameContentAsset"/>, so every scene
    /// plays by the same rules and tuning made in one place shows up everywhere.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunter System/Player Tuning", fileName = "PlayerTuning")]
    public sealed class PlayerTuningAsset : ScriptableObject
    {
        [SerializeField, Min(1)] private int tickRate = 60;
        [SerializeField, Min(0f)] private float playerMoveSpeed = 5f;
        [SerializeField, Min(1)] private int playerMaxHealth = 100;

        [Header("Light attack")]
        [SerializeField, Min(0)] private int lightAttackDamage = 10;
        [SerializeField, Min(0f)] private float lightAttackRange = 2f;
        [SerializeField, Range(1f, 360f)] private float lightAttackArcDegrees = 120f;
        [SerializeField, Min(0f)] private float lightAttackCooldownSeconds = 0.4f;

        [Header("Heavy attack")]
        [SerializeField, Min(0)] private int heavyAttackDamage = 25;
        [SerializeField, Min(0f)] private float heavyAttackRange = 2.5f;
        [SerializeField, Range(1f, 360f)] private float heavyAttackArcDegrees = 90f;
        [SerializeField, Min(0f)] private float heavyAttackWindupSeconds = 0.35f;
        [SerializeField, Min(0f)] private float heavyAttackCooldownSeconds = 1f;

        [Header("Soft lock-on")]
        [SerializeField] private bool softLockOnEnabled = true;
        [SerializeField, Min(0f)] private float softLockOnRange = 4f;
        [SerializeField, Range(1f, 360f)] private float softLockOnConeDegrees = 90f;

        [Header("Dodge")]
        [SerializeField, Min(0f)] private float dodgeDistance = 3f;
        [SerializeField, Min(0f)] private float dodgeDurationSeconds = 0.25f;
        [SerializeField, Min(0f)] private float dodgeInvulnerabilitySeconds = 0.3f;
        [SerializeField, Min(0f)] private float dodgeCooldownSeconds = 0.8f;

        public int TickRate => tickRate;

        public void ApplyTo(SimulationConfig config)
        {
            config.TickRate = tickRate;
            config.PlayerMoveSpeed = playerMoveSpeed;
            config.PlayerMaxHealth = playerMaxHealth;
            config.LightAttack = new AttackDefinition
            {
                Damage = lightAttackDamage, Range = lightAttackRange, ArcDegrees = lightAttackArcDegrees, CooldownSeconds = lightAttackCooldownSeconds,
            };
            config.HeavyAttack = new AttackDefinition
            {
                Damage = heavyAttackDamage, Range = heavyAttackRange, ArcDegrees = heavyAttackArcDegrees,
                WindupSeconds = heavyAttackWindupSeconds, CooldownSeconds = heavyAttackCooldownSeconds,
            };
            config.SoftLockOn = new SoftLockOnDefinition { Enabled = softLockOnEnabled, Range = softLockOnRange, ConeDegrees = softLockOnConeDegrees };
            config.Dodge = new DodgeDefinition
            {
                Distance = dodgeDistance, DurationSeconds = dodgeDurationSeconds,
                InvulnerabilitySeconds = dodgeInvulnerabilitySeconds, CooldownSeconds = dodgeCooldownSeconds,
            };
        }
    }
}
