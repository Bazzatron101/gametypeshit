using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// The small perk a species brings (ticket 81, ADR 0024). Each is a nudge, not a build-decider; its numbers
    /// are the species' <see cref="SpeciesDefinition.PerkAmount"/> and <see cref="SpeciesDefinition.PerkThreshold"/>.
    /// A new species picks one of these, so adding a species needs no code.
    /// </summary>
    public enum SpeciesPerk : byte
    {
        None = 0,

        /// <summary>The hunter's hits push ordinary enemies back. Amount: metres. Bosses stand firm.</summary>
        Knockback = 1,

        /// <summary>Hits on an enemy at or below Threshold % of its health do Amount % more damage.</summary>
        FinishingBlow = 2,

        /// <summary>The way down from a jump is slower: it lasts Amount % longer than the way up.</summary>
        Glide = 3,

        /// <summary>A hit from behind (an enemy outside the front half of the hunter's facing) does Amount % less damage.</summary>
        ThickHide = 4,

        /// <summary>After Threshold seconds without hitting or being hit, regains Amount % of max health each second.</summary>
        RestfulHealing = 5,

        /// <summary>Skills cool down Amount % faster.</summary>
        QuickThinking = 6,

        /// <summary>Senses hidden things from Amount % further away: hidden gates now; hidden enemies and traps when they exist.</summary>
        KeenSenses = 7,

        /// <summary>Counts Amount more Luck points in a gate's clear draw (on top of the Luck stat, ticket 61).</summary>
        Lucky = 8,
    }

    /// <summary>
    /// A species (ticket 81, ADR 0024): every hunter is an Australian animal-folk. The species gives a stat
    /// bonus that grows with rank, and one small perk. Chosen when the hunter is made; permanent, like the
    /// class. Looks (ears, snout, tail) are the game layer's.
    /// </summary>
    public sealed class SpeciesDefinition
    {
        /// <summary>Stable, unique. Saves and snapshots refer to it; never rename it once players have hunters.</summary>
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        /// <summary>The stat the species is good at.</summary>
        public Stat BonusStat { get; set; }

        /// <summary>Added to <see cref="BonusStat"/> at the first rank (E).</summary>
        public int BonusAtFirstRank { get; set; } = 2;

        /// <summary>Added on top for each rank above the first.</summary>
        public int BonusPerRank { get; set; } = 1;

        public SpeciesPerk Perk { get; set; }

        /// <summary>How the hunter travels when they mount up (ticket 90): fly, run or ride a bicycle.</summary>
        public TravelStyle Travel { get; set; }

        /// <summary>The perk's size; see <see cref="SpeciesPerk"/> for what it means for each.</summary>
        public float PerkAmount { get; set; }

        /// <summary>The perk's condition, for the perks that have one (see <see cref="SpeciesPerk"/>).</summary>
        public float PerkThreshold { get; set; }

        /// <summary>The stat bonus for a hunter at rank index <paramref name="rankIndex"/> (0 = E).</summary>
        public int BonusAtRank(int rankIndex) => BonusAtFirstRank + BonusPerRank * Math.Max(0, rankIndex);

        public override string ToString() => Id;

        /// <summary>
        /// The eight species the game ships with (ticket 81). The game content holds its own editable copy;
        /// this is its starting point and what tests and bare scenes use.
        /// </summary>
        public static SpeciesDefinition[] Standard() => new[]
        {
            new SpeciesDefinition { Id = "kangaroo", Travel = TravelStyle.Run, Name = "Kangaroo", BonusStat = Stat.Strength, Perk = SpeciesPerk.Knockback, PerkAmount = 1.2f,
                Description = "Powerful legs and a solid tail. Hits push ordinary enemies back." },
            new SpeciesDefinition { Id = "tasmanian-devil", Travel = TravelStyle.Run, Name = "Tasmanian devil", BonusStat = Stat.Strength, Perk = SpeciesPerk.FinishingBlow, PerkAmount = 15f, PerkThreshold = 30f,
                Description = "Small, loud and relentless. Hits on badly hurt enemies do more damage." },
            new SpeciesDefinition { Id = "sugar-glider", Travel = TravelStyle.Fly, Name = "Sugar glider", BonusStat = Stat.Agility, Perk = SpeciesPerk.Glide, PerkAmount = 60f,
                Description = "Light and quick. Glides on the way down from a jump." },
            new SpeciesDefinition { Id = "wombat", Travel = TravelStyle.Bicycle, Name = "Wombat", BonusStat = Stat.Vitality, Perk = SpeciesPerk.ThickHide, PerkAmount = 20f,
                Description = "Built like a barrel, with a hard rump. Takes less damage from behind." },
            new SpeciesDefinition { Id = "koala", Travel = TravelStyle.Bicycle, Name = "Koala", BonusStat = Stat.Vitality, Perk = SpeciesPerk.RestfulHealing, PerkAmount = 1f, PerkThreshold = 6f,
                Description = "Calm and unhurried. Slowly regains health out of a fight." },
            new SpeciesDefinition { Id = "cockatoo", Travel = TravelStyle.Fly, Name = "Cockatoo", BonusStat = Stat.Intelligence, Perk = SpeciesPerk.QuickThinking, PerkAmount = 8f,
                Description = "Clever and noisy. Skills cool down a little faster." },
            new SpeciesDefinition { Id = "platypus", Travel = TravelStyle.Bicycle, Name = "Platypus", BonusStat = Stat.Sense, Perk = SpeciesPerk.KeenSenses, PerkAmount = 50f,
                Description = "Feels the world through its bill. Senses hidden things from further away." },
            new SpeciesDefinition { Id = "quokka", Travel = TravelStyle.Bicycle, Name = "Quokka", BonusStat = Stat.Luck, Perk = SpeciesPerk.Lucky, PerkAmount = 5f,
                Description = "Always smiling, somehow. Slightly better drops." },
        };

        /// <summary>Checks a game's species: ids unique and present, numbers in range. Throws an <see cref="ArgumentException"/> naming the problem.</summary>
        public static void Validate(IReadOnlyList<SpeciesDefinition> species)
        {
            var ids = new HashSet<string>();
            foreach (var s in species ?? Array.Empty<SpeciesDefinition>())
            {
                if (s == null) throw new ArgumentException("A species is missing.");
                if (string.IsNullOrEmpty(s.Id)) throw new ArgumentException("A species has no id.");
                if (!ids.Add(s.Id)) throw new ArgumentException($"Species '{s.Id}' is defined twice.");
                if ((int)s.BonusStat < 0 || (int)s.BonusStat >= PlayerStats.Count) throw new ArgumentException($"Species '{s.Id}': its stat is not one of the six.");
                if (s.BonusAtFirstRank < 0 || s.BonusPerRank < 0) throw new ArgumentException($"Species '{s.Id}': its stat bonus cannot be negative.");
                if (s.PerkAmount < 0f || s.PerkThreshold < 0f) throw new ArgumentException($"Species '{s.Id}': its perk numbers cannot be negative.");
                if (s.Perk == SpeciesPerk.ThickHide && s.PerkAmount > 100f) throw new ArgumentException($"Species '{s.Id}': it cannot take more than 100% less damage.");
                if (s.Perk == SpeciesPerk.QuickThinking && s.PerkAmount >= 100f) throw new ArgumentException($"Species '{s.Id}': cooldowns cannot be 100% faster.");
            }
        }
    }

    public sealed partial class CharacterRecord
    {
        /// <summary>
        /// This character as <paramref name="chosen"/>, for a character made before species (ticket 81).
        /// Class and progress stay. The bonus is not stored in the stats: it is worked out from the rank,
        /// so it grows by itself. The choice is permanent.
        /// </summary>
        public CharacterRecord WithSpecies(SpeciesDefinition chosen)
        {
            if (chosen == null) throw new ArgumentNullException(nameof(chosen));
            if (SpeciesId != null) throw new InvalidOperationException($"This hunter is already a {SpeciesId}; the choice is permanent.");
            var copy = CharacterCodec.Decode(CharacterCodec.Encode(this));
            copy.IsNew = IsNew;
            copy.SpeciesId = chosen.Id;
            return copy;
        }
    }
}
