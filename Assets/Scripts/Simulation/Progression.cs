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

        /// <summary>
        /// The guild's ranks, lowest first, each with the level its band starts at (ticket 25). The first
        /// rank is every new hunter's. Provisional bands from the spec: E 1, D 16, C 36, B 56, A 76, S 96
        /// (S is past the main game's cap of 95 until the S story raises it).
        /// </summary>
        public RankDefinition[] Ranks { get; set; } = DefaultRanks();

        /// <summary>The spec's provisional bands, with the regions ticket 36 opens at each promotion.</summary>
        public static RankDefinition[] DefaultRanks() => new[]
        {
            new RankDefinition { Id = "E", FirstLevel = 1 },
            new RankDefinition { Id = "D", FirstLevel = 16, OpensRegions = new[] { "fenward" }, GrantsMount = true },
            new RankDefinition { Id = "C", FirstLevel = 36 },
            new RankDefinition { Id = "B", FirstLevel = 56, OpensRegions = new[] { "ashen" } },
            new RankDefinition { Id = "A", FirstLevel = 76 },
            new RankDefinition { Id = "S", FirstLevel = 96 },
        };

        /// <summary>The index in <see cref="Ranks"/> of the band <paramref name="level"/> is in; 0 with no ranks.</summary>
        public int RankIndexAt(int level)
        {
            var index = 0;
            for (var i = 0; i < (Ranks?.Length ?? 0); i++)
                if (Ranks[i] != null && level >= Ranks[i].FirstLevel) index = i;
            return index;
        }

        /// <summary>The index of the rank with <paramref name="rankId"/>, or -1.</summary>
        public int RankIndexOf(string rankId) => rankId == null || Ranks == null ? -1 : Array.FindIndex(Ranks, r => r?.Id == rankId);

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
    /// A guild rank (ticket 25): the licence the guild grants, and the level band it covers. Rank is a
    /// licence, not a power: reaching the band is the promotion. The host's promotion is the host's
    /// world's (it opens regions and plays the promotion scene); every character's own rank is its level's band.
    /// </summary>
    public sealed class RankDefinition
    {
        /// <summary>The rank's letter, also its id: "E", "D", ... Never change it once worlds have it.</summary>
        public string Id { get; set; }

        /// <summary>The first level of this rank's band.</summary>
        public int FirstLevel { get; set; } = 1;

        /// <summary>Regions whose guilds start giving work when the host's world is promoted to this rank.</summary>
        public string[] OpensRegions { get; set; } = Array.Empty<string>();

        /// <summary>Whether a character reaching this rank gets their first mount (ADR 0015: it comes with D).</summary>
        public bool GrantsMount { get; set; }
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

        /// <summary>Damage multiplier per Agility point for skills and weapon attacks that scale with Agility, such as the bow (0.05 = +5%).</summary>
        public float DamagePerAgility { get; set; } = 0.05f;

        /// <summary>Max health gained per Vitality point.</summary>
        public int HealthPerVitality { get; set; } = 10;

        /// <summary>Move speed multiplier gained per Agility point (0.02 = +2%).</summary>
        public float MoveSpeedPerAgility { get; set; } = 0.02f;
    }
}
