using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Authoring asset for levels and stats: the XP curve, the level cap and what stats do.
    /// The simulation only sees the plain definitions it produces.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunter System/Progression", fileName = "Progression")]
    public sealed class ProgressionAsset : ScriptableObject
    {
        [SerializeField, Min(1), Tooltip("95 for the main game; raised for the S-rank story and DLC")] private int levelCap = 95;
        [SerializeField, Min(0)] private int freePointsPerLevel = 3;
        [SerializeField, Min(0)] private int startingStatValue = 10;
        [SerializeField, Tooltip("XP to go from each level to the next: element 0 is level 1 to 2. Needs levelCap - 1 entries.")]
        private int[] xpToNextLevel = ProgressionDefinition.DefaultCurve(95);

        [Header("Stat effects, per point above the starting value")]
        [SerializeField, Tooltip("0.05 = +5% damage per Strength point (attacks and Strength skills)")] private float damagePerStrength = 0.05f;
        [SerializeField, Tooltip("0.05 = +5% damage per Intelligence point (Intelligence skills)")] private float damagePerIntelligence = 0.05f;
        [SerializeField, Tooltip("Max health per Vitality point")] private int healthPerVitality = 10;
        [SerializeField, Tooltip("0.02 = +2% move speed per Agility point")] private float moveSpeedPerAgility = 0.02f;

        public ProgressionDefinition ToProgression() => new ProgressionDefinition
        {
            LevelCap = levelCap,
            FreePointsPerLevel = freePointsPerLevel,
            StartingStatValue = startingStatValue,
            XpToNextLevel = (int[])xpToNextLevel.Clone(),
        };

        public StatEffectsDefinition ToStatEffects() => new StatEffectsDefinition
        {
            DamagePerStrength = damagePerStrength,
            DamagePerIntelligence = damagePerIntelligence,
            HealthPerVitality = healthPerVitality,
            MoveSpeedPerAgility = moveSpeedPerAgility,
        };
    }
}
