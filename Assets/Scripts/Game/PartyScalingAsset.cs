using System;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Party-size scaling (ticket 27): how much tougher, harder-hitting and more numerous the
    /// enemies gates spawn are for a party of two to four, and of five to ten. Solo is always the
    /// gate as authored, so every normal gate stays soloable. Players and rewards are never scaled.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunter System/Party Scaling", fileName = "PartyScaling")]
    public sealed class PartyScalingAsset : ScriptableObject
    {
        [Serializable]
        public struct Tier
        {
            [Range(10, 1000), Tooltip("Enemy health, as a percentage of authored")] public int healthPercent;
            [Range(10, 500), Tooltip("Enemy damage, as a percentage of authored")] public int damagePercent;
            [Range(10, 500), Tooltip("Enemies per room (and a boss's adds), as a percentage of authored; bosses are never multiplied")] public int spawnPercent;

            public PartyTier ToTier() => new PartyTier { HealthPercent = healthPercent, DamagePercent = damagePercent, SpawnPercent = spawnPercent };
        }

        [SerializeField, Tooltip("Two to four players present")] private Tier small = new Tier { healthPercent = 160, damagePercent = 115, spawnPercent = 150 };
        [SerializeField, Tooltip("Five to ten players present")] private Tier large = new Tier { healthPercent = 250, damagePercent = 130, spawnPercent = 200 };

        public PartyScalingDefinition ToDefinition() => new PartyScalingDefinition { Small = small.ToTier(), Large = large.ToTier() };
    }
}
