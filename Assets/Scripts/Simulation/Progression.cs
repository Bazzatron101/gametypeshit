using System;

namespace HunterSystem.Simulation
{
    /// <summary>The six hunter stats.</summary>
    public enum Stat
    {
        Strength,
        Agility,
        Vitality,
        Intelligence,
        Sense,
        Luck,
    }

    /// <summary>Read-only set of the six stat values.</summary>
    public readonly struct PlayerStats
    {
        public static readonly int Count = Enum.GetValues(typeof(Stat)).Length;

        private readonly int[] _values;

        internal PlayerStats(int[] values) => _values = (int[])values.Clone();

        /// <summary>0 for an empty snapshot (default struct) rather than throwing.</summary>
        public int this[Stat stat] => _values != null ? _values[(int)stat] : 0;
    }

    /// <summary>
    /// Levels and stat points. The curve and the cap are data: raising the cap for the S-rank
    /// story or a DLC is a data change plus more curve entries.
    /// </summary>
    public sealed class ProgressionDefinition
    {
        /// <summary>The highest level a player can reach.</summary>
        public int LevelCap { get; set; } = 95;

        /// <summary>
        /// XP needed to go from each level to the next: entry 0 is level 1 to 2. Needs at least
        /// <see cref="LevelCap"/> - 1 entries.
        /// </summary>
        public int[] XpToNextLevel { get; set; } = DefaultCurve(95);

        /// <summary>Free stat points granted on each level-up, on top of (future) class bonuses.</summary>
        public int FreePointsPerLevel { get; set; } = 3;

        /// <summary>Every stat starts here; stat effects count points above it.</summary>
        public int StartingStatValue { get; set; } = 10;

        /// <summary>A gentle power curve: 50 XP for level 2, rising to about 70,000 for level 95.</summary>
        public static int[] DefaultCurve(int levelCap)
        {
            var curve = new int[Math.Max(0, levelCap - 1)];
            for (var level = 1; level < levelCap; level++)
                curve[level - 1] = (int)Math.Round(50 * Math.Pow(level, 1.6));
            return curve;
        }
    }

    /// <summary>
    /// What each stat point above <see cref="ProgressionDefinition.StartingStatValue"/> does.
    /// Sense and Luck have no effect yet; Luck arrives with loot.
    /// </summary>
    public sealed class StatEffectsDefinition
    {
        /// <summary>Damage multiplier per Strength point, for attacks and Strength skills (0.05 = +5%).</summary>
        public float DamagePerStrength { get; set; } = 0.05f;

        /// <summary>Damage multiplier per Intelligence point for skills that scale with Intelligence (0.05 = +5%).</summary>
        public float DamagePerIntelligence { get; set; } = 0.05f;

        /// <summary>Max health gained per Vitality point.</summary>
        public int HealthPerVitality { get; set; } = 10;

        /// <summary>Move speed multiplier gained per Agility point (0.02 = +2%).</summary>
        public float MoveSpeedPerAgility { get; set; } = 0.02f;
    }
}
