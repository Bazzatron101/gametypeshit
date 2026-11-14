using HunterSystem.Simulation;

namespace HunterSystem.Netcode
{
    /// <summary>
    /// What a bot teammate does in the party, from its class (ADR 0014). Enemies go for the nearest
    /// player, so roles are mostly about where each one stands: the tank in front, melee beside it,
    /// ranged behind.
    /// </summary>
    public enum CompanionRole
    {
        /// <summary>Warrior: goes in first, stays the closest target, and pulls enemies off the back line.</summary>
        Tank,

        /// <summary>Rogue: melee damage from the flanks and behind a boss; finishes off weak enemies.</summary>
        Skirmisher,

        /// <summary>Archer: steady single-target damage from range, on the tank's target; backs off when rushed.</summary>
        Marksman,

        /// <summary>Wizard: area damage on packed enemies from the back; keeps its big spells for groups and bosses.</summary>
        Caster,
    }

    public static class CompanionRoles
    {
        /// <summary>The role a class plays. An unknown class fights in melee.</summary>
        public static CompanionRole ForClass(string classId)
        {
            switch (classId)
            {
                case "warrior": return CompanionRole.Tank;
                case "archer": return CompanionRole.Marksman;
                case "wizard": return CompanionRole.Caster;
                default: return CompanionRole.Skirmisher;
            }
        }

        public static bool IsFrontLine(CompanionRole role) => role == CompanionRole.Tank || role == CompanionRole.Skirmisher;

        /// <summary>
        /// Where its free stat points go, repeated in order: the tank into Vitality and Strength, the
        /// Rogue into Agility then Luck, the Archer into Agility and Sense, the Wizard into Intelligence.
        /// </summary>
        public static Stat[] StatPlan(CompanionRole role)
        {
            switch (role)
            {
                case CompanionRole.Tank: return new[] { Stat.Vitality, Stat.Strength, Stat.Vitality };
                case CompanionRole.Skirmisher: return new[] { Stat.Agility, Stat.Agility, Stat.Luck };
                case CompanionRole.Marksman: return new[] { Stat.Agility, Stat.Sense, Stat.Agility };
                default: return new[] { Stat.Intelligence, Stat.Intelligence, Stat.Vitality };
            }
        }

        /// <summary>
        /// How far from its target it likes to fight, in metres: melee at arm's length, ranged well
        /// back but inside its shots' range.
        /// </summary>
        public static float PreferredRange(CompanionRole role)
        {
            switch (role)
            {
                case CompanionRole.Marksman: return 8f;
                case CompanionRole.Caster: return 7f;
                default: return 1.5f;
            }
        }

        public static string Title(CompanionRole role)
        {
            switch (role)
            {
                case CompanionRole.Tank: return "Tank";
                case CompanionRole.Skirmisher: return "Skirmisher";
                case CompanionRole.Marksman: return "Marksman";
                default: return "Caster";
            }
        }
    }
}
