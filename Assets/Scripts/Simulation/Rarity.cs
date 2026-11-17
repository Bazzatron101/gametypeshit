using System;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// How rare a piece of gear is (ticket 61). A higher tier has bigger stats and extra bonuses, and a
    /// Legendary has a special look. The tier belongs to the piece: every copy of it is the same.
    /// </summary>
    public enum Rarity : byte
    {
        Common,
        Rare,
        Epic,
        Legendary,
    }

    /// <summary>
    /// The odds of each rarity in a gate's clear draw (ticket 61). Each item in the pool weighs its
    /// tier's base weight; a higher-rank gate and the player's Luck tip the weights toward the rarer
    /// tiers. Modifiers in a pool weigh as Common. All whole numbers, so every machine draws the same.
    /// </summary>
    public sealed class RarityOddsDefinition
    {
        public const int TierCount = 4;

        /// <summary>Base weight of one item of each tier, Common first. 60/28/10/2 by default.</summary>
        public int[] BaseWeights { get; set; } = { 60, 28, 10, 2 };

        /// <summary>For each rank the gate is above the first (E), every tier above Common weighs this many percent more per tier.</summary>
        public int RankBonusPercent { get; set; } = 25;

        /// <summary>For each point of Luck above the starting value, every tier above Common weighs this many percent more per tier.</summary>
        public int LuckBonusPercent { get; set; } = 2;

        /// <summary>
        /// One item's weight: its tier's base weight, raised by <c>tier × (rank steps × RankBonusPercent
        /// + Luck points × LuckBonusPercent)</c> percent. Common never changes, so the rarer tiers gain
        /// on it. Negative rank steps or Luck count as none.
        /// </summary>
        public int WeightOf(Rarity rarity, int rankSteps, int luckAboveStart)
        {
            var tier = (int)rarity;
            var baseWeight = BaseWeights != null && tier < BaseWeights.Length ? BaseWeights[tier] : 0;
            var bonus = tier * (Math.Max(0, rankSteps) * RankBonusPercent + Math.Max(0, luckAboveStart) * LuckBonusPercent);
            return (int)Math.Min(int.MaxValue / 1024, (long)baseWeight * (100 + bonus) / 100);
        }

        /// <summary>Throws if the odds cannot be used.</summary>
        public void Validate()
        {
            if (BaseWeights == null || BaseWeights.Length != TierCount)
                throw new ArgumentException($"Rarity odds: BaseWeights must have {TierCount} entries, Common to Legendary.");
            for (var i = 0; i < TierCount; i++)
                if (BaseWeights[i] < 1 || BaseWeights[i] > 1_000_000)
                    throw new ArgumentException($"Rarity odds: the {(Rarity)i} weight must be from 1 to 1,000,000.");
            if (RankBonusPercent < 0 || LuckBonusPercent < 0)
                throw new ArgumentException("Rarity odds: the rank and Luck bonuses cannot be negative.");
        }
    }
}
