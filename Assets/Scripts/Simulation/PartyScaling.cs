using System;

namespace HunterSystem.Simulation
{
    public enum PartyTierKind : byte
    {
        /// <summary>One player present.</summary>
        Solo,

        /// <summary>Two to four players present.</summary>
        Small,

        /// <summary>Five to ten players present.</summary>
        Large,
    }

    /// <summary>How one party-size tier changes the enemies that spawn while it applies.</summary>
    public sealed class PartyTier
    {
        public int HealthPercent { get; set; } = 100;
        public int DamagePercent { get; set; } = 100;

        /// <summary>How many enemies a room (or a boss's call for help) brings, as a percentage of what is authored. Bosses are never multiplied.</summary>
        public int SpawnPercent { get; set; } = 100;
    }

    /// <summary>
    /// Party-size scaling (ticket 27, spec story 73): the tiers' multipliers. Solo is the gate as
    /// authored, so every normal gate stays soloable. Players and rewards are never scaled.
    /// </summary>
    public sealed class PartyScalingDefinition
    {
        public PartyTier Solo { get; set; } = new PartyTier();
        public PartyTier Small { get; set; } = new PartyTier();
        public PartyTier Large { get; set; } = new PartyTier();

        public static PartyTierKind TierFor(int playersPresent) =>
            playersPresent <= 1 ? PartyTierKind.Solo : playersPresent <= 4 ? PartyTierKind.Small : PartyTierKind.Large;

        public PartyTier this[PartyTierKind tier] => tier switch
        {
            PartyTierKind.Small => Small,
            PartyTierKind.Large => Large,
            _ => Solo,
        };

        /// <summary><paramref name="value"/> scaled by <paramref name="percent"/>, in whole numbers, rounding halves up (no float maths, so every machine agrees).</summary>
        public static int Scale(int value, int percent) => (int)Math.Max(0L, ((long)value * Math.Max(0, percent) + 50) / 100);

        /// <summary>How many to spawn for <paramref name="authored"/> authored enemies at <paramref name="percent"/>, rounding halves up.</summary>
        public static int Count(int authored, int percent) => Scale(authored, percent);
    }
}
