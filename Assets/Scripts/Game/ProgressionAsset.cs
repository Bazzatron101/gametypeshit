using System;
using System.Linq;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Authoring asset for levels and stats: the XP curve, the level cap, the guild's rank bands and what stats do.
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

        [Serializable]
        public struct Rank
        {
            [Tooltip("The rank's letter, also its id. Never change it once worlds have it.")] public string id;
            [Min(1), Tooltip("The first level of this rank's band")] public int firstLevel;
            [Tooltip("Regions whose guilds start giving work when the host's world is promoted to this rank")] public string[] opensRegions;
            [Tooltip("A character reaching this rank gets their first mount (ADR 0015: D)")] public bool grantsMount;
        }

        [SerializeField, Tooltip("The guild's ranks, lowest first: each band starts at its first level (ticket 25). The first rank is every new hunter's.")]
        private Rank[] ranks = ProgressionDefinition.DefaultRanks().Select(r => new Rank
        {
            id = r.Id, firstLevel = r.FirstLevel, opensRegions = r.OpensRegions.ToArray(), grantsMount = r.GrantsMount,
        }).ToArray();

        [Header("Stat effects, per point above the starting value")]
        [SerializeField, Tooltip("0.05 = +5% damage per Strength point (attacks and Strength skills)")] private float damagePerStrength = 0.05f;
        [SerializeField, Tooltip("0.05 = +5% damage per Intelligence point (Intelligence skills)")] private float damagePerIntelligence = 0.05f;
        [SerializeField, Tooltip("0.05 = +5% damage per Agility point (Agility skills and the bow)")] private float damagePerAgility = 0.05f;
        [SerializeField, Tooltip("Max health per Vitality point")] private int healthPerVitality = 10;
        [SerializeField, Tooltip("0.02 = +2% move speed per Agility point")] private float moveSpeedPerAgility = 0.02f;

        public ProgressionDefinition ToProgression() => new ProgressionDefinition
        {
            LevelCap = levelCap,
            FreePointsPerLevel = freePointsPerLevel,
            StartingStatValue = startingStatValue,
            XpToNextLevel = (int[])xpToNextLevel.Clone(),
            Ranks = (ranks ?? new Rank[0]).Select(r => new RankDefinition
            {
                Id = r.id, FirstLevel = r.firstLevel, OpensRegions = (r.opensRegions ?? new string[0]).ToArray(), GrantsMount = r.grantsMount,
            }).ToArray(),
        };

        public StatEffectsDefinition ToStatEffects() => new StatEffectsDefinition
        {
            DamagePerStrength = damagePerStrength,
            DamagePerIntelligence = damagePerIntelligence,
            DamagePerAgility = damagePerAgility,
            HealthPerVitality = healthPerVitality,
            MoveSpeedPerAgility = moveSpeedPerAgility,
        };
    }
}
