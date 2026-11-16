using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// A class, chosen when a character is made (ticket 24): which skills it learns (skills whose
    /// <see cref="SkillDefinition.ClassId"/> is this class, plus shared ones), the stat points it
    /// gives in its main stats every level on top of the free ones, and how high it jumps.
    /// </summary>
    public sealed class ClassDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>Points added to each stat at every level gained, indexed by <see cref="Stat"/>.</summary>
        public int[] StatsPerLevel { get; set; } = new int[PlayerStats.Count];

        /// <summary>Jump height as a percentage of the base jump (above an enemy's height, a jump clears it).</summary>
        public int JumpHeightPercent { get; set; } = 100;

        /// <summary>
        /// The weapon a character of this class is issued (ticket 46), by gear id, or null. A
        /// character gets it when it has no weapon at all: worn or in the bag.
        /// </summary>
        public string StartingWeaponId { get; set; }

        /// <summary>
        /// The class's trials (ticket 26), by gate id: one per specialty tier, and the gate's rank is the tier's
        /// (D, then B). Clearing one, at its rank, unlocks that tier's pick. Empty for a class with no trials yet.
        /// </summary>
        public List<string> TrialGateIds { get; } = new List<string>();

        /// <summary>
        /// The specialties this class branches into (ticket 26), as a tree: two at D, and two at B under each D
        /// specialty. One is picked for good at each tier, after that tier's trial.
        /// </summary>
        public List<SpecialtyDefinition> Specialties { get; } = new List<SpecialtyDefinition>();

        public int StatPerLevel(int stat) => StatsPerLevel != null && stat < StatsPerLevel.Length ? StatsPerLevel[stat] : 0;
    }

    /// <summary>
    /// A specialty (ticket 26): a branch a class takes at a rank (D or B) once that rank's trial is cleared. The
    /// pick is permanent. A specialty brings its own skills (skills whose <see cref="SkillDefinition.SpecialtyId"/>
    /// is this one, learned at their levels) and passive modifiers on the class's skills, always in effect.
    /// The B-rank specialties branch from the D-rank ones: only those under the hunter's D pick are offered.
    /// </summary>
    public sealed class SpecialtyDefinition
    {
        /// <summary>Stable, unique across all classes. Saves refer to it; never rename it once characters have it.</summary>
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>The class it branches from.</summary>
        public string ClassId { get; set; }

        /// <summary>The tier: the rank whose trial unlocks the pick ("D" or "B").</summary>
        public string Rank { get; set; }

        /// <summary>The specialty of the tier below that this one branches from, or null at the first tier.</summary>
        public string ParentId { get; set; }

        /// <summary>Always in effect for a character with this specialty, on top of its own modifiers and its gear's.</summary>
        public List<ModifierDefinition> Passives { get; } = new List<ModifierDefinition>();

        public override string ToString() => Id;
    }

    public sealed partial class CharacterRecord
    {
        /// <summary>
        /// This character with <paramref name="chosen"/> as its class, for a character made before
        /// classes existed: it gains the class's stats for every level it has already reached. Its
        /// own placed points stay. The choice is permanent.
        /// </summary>
        /// <param name="addEarnedStats">
        /// False for a character whose class is gone from the game: its stats already hold that
        /// class's bonuses, which cannot be told apart, so the new class gives nothing for past levels.
        /// </param>
        public CharacterRecord WithClass(ClassDefinition chosen, ProgressionDefinition progression, bool addEarnedStats = true)
        {
            if (chosen == null) throw new ArgumentNullException(nameof(chosen));
            if (ClassId != null) throw new InvalidOperationException($"This character is already a {ClassId}; the choice is permanent.");
            var stats = new int[PlayerStats.Count];
            for (var i = 0; i < stats.Length; i++)
            {
                var own = Stats != null && i < Stats.Length ? Stats[i] : progression.StartingStatValue;
                stats[i] = own + (addEarnedStats ? chosen.StatPerLevel(i) * Math.Max(0, Level - 1) : 0);
            }
            return new CharacterRecord
            {
                IsNew = IsNew,
                Name = Name,
                Look = Look,
                ClassId = chosen.Id,
                Specialties = new List<string>(Specialties ?? new List<string>()),
                PassedTrials = new List<string>(PassedTrials ?? new List<string>()),
                Level = Level,
                Xp = Xp,
                UnspentStatPoints = UnspentStatPoints,
                Stats = IsNew ? (int[])Stats?.Clone() : stats, // a new character has no levels to make up
                KnownSkills = new List<string>(KnownSkills ?? new List<string>()),
                SkillBar = (string[])(SkillBar ?? new string[SimulationWorld.SkillSlotCount]).Clone(),
                Modifiers = new List<ModifierDefinition>(Modifiers ?? new List<ModifierDefinition>()),
                Gold = Gold,
                Duplicates = new List<string>(Duplicates ?? new List<string>()),
                Gear = new List<string>(Gear ?? new List<string>()),
                Worn = (string[])(Worn ?? new string[GearDefinition.SlotCount]).Clone(),
            };
        }
    }
}
