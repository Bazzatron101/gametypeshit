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

        /// <summary>
        /// The tags a role wants on its gear: the front line melee and physical, the Archer
        /// projectiles, the Wizard magic, fire and areas.
        /// </summary>
        public static string[] GearTags(CompanionRole role)
        {
            switch (role)
            {
                case CompanionRole.Tank:
                case CompanionRole.Skirmisher: return new[] { SkillTags.Melee, SkillTags.Physical };
                case CompanionRole.Marksman: return new[] { SkillTags.Projectile, SkillTags.Physical };
                default: return new[] { SkillTags.Magic, SkillTags.Fire, SkillTags.Area };
            }
        }

        /// <summary>
        /// How well a piece suits a role: one point for each wanted tag it carries, one for each of its
        /// modifiers aimed at one, and its stat bonuses to the stats the role puts its points into
        /// (<see cref="StatPlan"/>), point for point (ticket 51). A minus to one of them counts against it.
        /// </summary>
        public static int GearScore(CompanionRole role, GearDefinition gear)
        {
            var score = 0;
            foreach (var stat in System.Linq.Enumerable.Distinct(StatPlan(role))) score += gear.BonusTo(stat);
            foreach (var tag in GearTags(role))
            {
                if (gear.HasTag(tag)) score++;
                foreach (var m in gear.Modifiers)
                    if (string.Equals(m.AppliesToTag, tag, System.StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(m.PerWornTag, tag, System.StringComparison.OrdinalIgnoreCase)) score++;
            }
            return score;
        }

        /// <summary>
        /// A piece from the bag worth putting on now, or null: the best-suited piece for a slot that is
        /// empty (unless it scores below zero for the role), or that beats what is worn there. The first piece found wins a tie, so it never swaps
        /// back and forth. One piece at a time, slot by slot.
        /// </summary>
        public static string GearToWear(CompanionRole role, PlayerState self)
        {
            for (var slot = 0; slot < GearDefinition.SlotCount; slot++)
            {
                GearDefinition best = null;
                var bestScore = int.MinValue;
                foreach (var gear in self.Bag)
                {
                    if ((int)gear.Slot != slot) continue;
                    var score = GearScore(role, gear);
                    if (score > bestScore) { best = gear; bestScore = score; }
                }
                var worn = self.Worn[slot];
                if (best == null || best == worn) continue;
                // An empty slot takes the best piece unless even that one costs the role more than it gives (ticket 51).
                if (worn == null ? bestScore >= 0 : bestScore > GearScore(role, worn)) return best.Id;
            }
            return null;
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
