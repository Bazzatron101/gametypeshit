using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;

namespace HunterSystem.Netcode
{
    /// <summary>
    /// Who the host's five bot teammates are (ADR 0014): one of every class, plus a fifth that
    /// evens out the front line and the back line around the player's own class. New teammates and
    /// ones that fell behind are brought up to the player's level when a session starts.
    /// </summary>
    public static class CompanionParty
    {
        public const int Size = 5;

        private static readonly Dictionary<string, string[]> Names = new Dictionary<string, string[]>
        {
            { "warrior", new[] { "Garrick", "Brannoc" } },
            { "rogue", new[] { "Vex", "Shade" } },
            { "archer", new[] { "Wren", "Kestrel" } },
            { "wizard", new[] { "Orin", "Ilsa" } },
        };

        /// <summary>Which class fills a gap first when two would do: a second tank before another melee, a marksman before a second caster.</summary>
        private static readonly CompanionRole[] FillOrder = { CompanionRole.Tank, CompanionRole.Marksman, CompanionRole.Caster, CompanionRole.Skirmisher };

        /// <summary>
        /// The five teammates' classes for a player of <paramref name="playerClassId"/> (null: none):
        /// every class once, then the class that evens the party's front and back lines (3 and 3 with
        /// four classes). With no classes in the game, five classless teammates.
        /// </summary>
        public static List<string> Roster(string playerClassId, IReadOnlyList<ClassDefinition> classes)
        {
            var roster = new List<string>();
            if (classes == null || classes.Count == 0)
            {
                for (var i = 0; i < Size; i++) roster.Add(null);
                return roster;
            }

            var ordered = classes.Select(c => c.Id).OrderBy(id => Array.IndexOf(FillOrder, CompanionRoles.ForClass(id))).ToList();
            roster.AddRange(ordered.Take(Size));
            while (roster.Count < Size)
            {
                var party = roster.Concat(playerClassId != null ? new[] { playerClassId } : Array.Empty<string>()).ToList();
                var front = party.Count(id => CompanionRoles.IsFrontLine(CompanionRoles.ForClass(id)));
                var wantFront = front <= party.Count - front;
                var pick = ordered
                    .Where(id => CompanionRoles.IsFrontLine(CompanionRoles.ForClass(id)) == wantFront)
                    .OrderBy(id => party.Count(p => p == id))
                    .FirstOrDefault() ?? ordered[0];
                roster.Add(pick);
            }
            return roster;
        }

        /// <summary>Five new rookie teammates for a player of <paramref name="playerClassId"/>, at level 1.</summary>
        public static List<CompanionRecord> NewParty(string playerClassId, SimulationConfig config)
        {
            var party = new List<CompanionRecord>();
            var used = new Dictionary<string, int>();
            foreach (var classId in Roster(playerClassId, config.Classes))
            {
                var key = classId ?? "";
                used.TryGetValue(key, out var n);
                used[key] = n + 1;
                var name = classId != null && Names.TryGetValue(classId, out var pool) ? pool[n % pool.Length] : $"Hunter {party.Count + 1}";
                party.Add(new CompanionRecord
                {
                    Name = name,
                    Character = new CharacterRecord { IsNew = true, ClassId = classId },
                });
            }
            return party;
        }

        /// <summary>
        /// The teammate's character raised to <paramref name="level"/> if it is below it (it trained while
        /// the player was away): its class's stats for the levels gained, and their free points to spend,
        /// which it places by its role. Its skills for those levels are learned when it joins the world.
        /// A teammate at or above the level is returned as it is.
        /// </summary>
        public static CharacterRecord CaughtUp(CharacterRecord character, int level, SimulationConfig config)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            var progression = config.Progression;
            level = Math.Min(level, progression.LevelCap);
            var from = character.IsNew ? 1 : Math.Max(1, character.Level);
            if (level <= from) return character;

            var gained = level - from;
            var chosen = config.ClassById(character.ClassId);
            var stats = new int[PlayerStats.Count];
            for (var i = 0; i < stats.Length; i++)
            {
                var own = character.Stats != null && i < character.Stats.Length ? character.Stats[i] : progression.StartingStatValue;
                stats[i] = Math.Max(own, progression.StartingStatValue) + (chosen?.StatPerLevel(i) ?? 0) * gained;
            }
            return new CharacterRecord
            {
                IsNew = false,
                ClassId = character.ClassId,
                Level = level,
                Xp = 0,
                UnspentStatPoints = Math.Max(0, character.UnspentStatPoints) + progression.FreePointsPerLevel * gained,
                Stats = stats,
                KnownSkills = new List<string>(character.KnownSkills ?? new List<string>()),
                SkillBar = (string[])(character.SkillBar ?? new string[SimulationWorld.SkillSlotCount]).Clone(),
                Modifiers = new List<ModifierDefinition>(character.Modifiers ?? new List<ModifierDefinition>()),
                Gold = character.Gold,
                Duplicates = new List<string>(character.Duplicates ?? new List<string>()),
                Gear = new List<string>(character.Gear ?? new List<string>()),
                Worn = (string[])(character.Worn ?? new string[GearDefinition.SlotCount]).Clone(),
            };
        }
    }
}
