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
        [SerializeField, Range(0, 100), Tooltip("Move speed while charging a skill, as a percentage of normal")] private int chargeMoveSpeedPercent = 40;
        [SerializeField, Min(0f), Tooltip("A hold shorter than this is a tap: the charge only builds after it")] private float chargeTapGraceSeconds = 0.15f;

        [Header("Sprint and jump")]
        [SerializeField, Range(100, 300), Tooltip("Holding sprint: move speed as a percentage of walking")] private int sprintSpeedPercent = 160;
        [SerializeField, Min(0.1f), Tooltip("A base jump's peak, in metres: keep it below every enemy's height (classes raise it)")] private float jumpHeight = 0.9f;
        [SerializeField, Min(0.2f), Tooltip("Take-off to landing")] private float jumpSeconds = 0.6f;

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
            config.ChargeMoveSpeedPercent = chargeMoveSpeedPercent;
            config.ChargeTapGraceSeconds = chargeTapGraceSeconds;
            config.SprintSpeedPercent = sprintSpeedPercent;
            config.JumpHeight = jumpHeight;
            config.JumpSeconds = jumpSeconds;
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
