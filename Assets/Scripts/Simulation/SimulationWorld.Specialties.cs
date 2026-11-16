using System;
using System.Collections.Generic;
using System.Linq;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// Specialties (ticket 26), a tree in two tiers. Each tier has a trial, a gate the guild posts at the
    /// tier's rank (D, then B). Clearing it as a hunter of that class and rank passes it, and the hunter
    /// may then pick one of that tier's specialties, for good: at D one of two, at B one of the two that
    /// branch from their D pick. A specialty brings its own skills (at their levels) and passive modifiers
    /// on the class's skills. Trials and picks are the character's own and are saved with it, wherever
    /// they were earned (a guest in a host's world too).
    /// </summary>
    public sealed partial class SimulationWorld
    {
        /// <summary>Every specialty a class has, at every tier, in order; empty for an unknown class.</summary>
        public IReadOnlyList<SpecialtyDefinition> SpecialtiesFor(string classId) =>
            (IReadOnlyList<SpecialtyDefinition>)_config.ClassById(classId)?.Specialties ?? Array.Empty<SpecialtyDefinition>();

        /// <summary>
        /// What the player may pick now: the specialties of the tier whose pick is waiting, under their pick at
        /// the tier below. Stubs are included (see <see cref="IsSpecialtyBuilt"/>). Empty when no pick is waiting.
        /// </summary>
        public IReadOnlyList<SpecialtyDefinition> SpecialtyOptions(PlayerId id)
        {
            var player = FindPlayer(id);
            var rank = PickWaiting(player);
            if (rank == null) return Array.Empty<SpecialtyDefinition>();
            return player.Class.Specialties.Where(s => s.Rank == rank && IsUnderPicks(player, s)).ToArray();
        }

        /// <summary>
        /// Whether a specialty has anything in it yet (a skill or a passive). One that has nothing is a stub
        /// still to be built: it is shown but cannot be picked, so nobody spends their only pick on it.
        /// </summary>
        public bool IsSpecialtyBuilt(SpecialtyDefinition specialty) =>
            specialty != null && (specialty.Passives.Count > 0 || _config.Skills.Any(s => s.SpecialtyId == specialty.Id));

        /// <summary>For testing only (the test arena): passes the player's next class trial at the start of the next tick.</summary>
        public void PassTrialForTesting(PlayerId id)
        {
            var player = FindPlayer(id);
            Enqueue(tick =>
            {
                var next = player.Class == null ? null : TierRanks(player.Class).FirstOrDefault(r => !player.PassedTrials.Contains(r));
                if (next != null) PassTrial(player, next, tick);
            });
        }

        /// <summary>The class's tiers, lowest first: the ranks its specialties are picked at.</summary>
        private IEnumerable<string> TierRanks(ClassDefinition c) =>
            c.Specialties.Select(s => s.Rank).Where(r => r != null).Distinct().OrderBy(_config.Progression.RankIndexOf);

        /// <summary>The lowest tier whose trial the player has passed and where they have picked nothing yet, or null.</summary>
        private string PickWaiting(Player player)
        {
            if (player.Class == null) return null;
            foreach (var rank in TierRanks(player.Class))
                if (player.PassedTrials.Contains(rank) && !player.Specialties.Any(s => s.Rank == rank)) return rank;
            return null;
        }

        /// <summary>A specialty at the first tier, or one branching from the player's pick at the tier below.</summary>
        private static bool IsUnderPicks(Player player, SpecialtyDefinition specialty) =>
            specialty.ParentId == null || player.Specialties.Any(s => s.Id == specialty.ParentId);

        /// <summary>After a gate's clear: everyone present whose class's trial it is, at the gate's rank or above, passes it.</summary>
        private void PassTrials(int tick)
        {
            var gate = _gate.Definition;
            if (gate.Rank == null) return; // a trial is a tier's, so it has a rank
            var rank = _config.Progression.RankIndexOf(gate.Rank);
            foreach (var player in _players)
            {
                if (player.Away || player.Class == null || !player.Class.TrialGateIds.Contains(gate.Id)) continue;
                if (_config.Progression.RankIndexAt(player.Level) < rank) continue; // the trial is for hunters of its rank
                PassTrial(player, gate.Rank, tick);
            }
        }

        /// <summary>
        /// Passes the <paramref name="rank"/> tier's trial, if the player has picked at every tier below it: a B trial
        /// is for a hunter who has their D specialty, since the B branches grow from it.
        /// </summary>
        private void PassTrial(Player player, string rank, int tick)
        {
            if (player.Class == null || player.PassedTrials.Contains(rank)) return;
            var index = _config.Progression.RankIndexOf(rank);
            foreach (var lower in TierRanks(player.Class).Where(r => _config.Progression.RankIndexOf(r) < index))
                if (!player.Specialties.Any(s => s.Rank == lower)) return;
            player.PassedTrials.Add(rank);
            _events.Add(new SpecialtyUnlocked(tick, player.Id, player.Class.Id, rank));
        }

        /// <summary>
        /// The pick, from a command. Out of a gate, with a pick waiting, one of the waiting tier's built specialties
        /// under the player's picks. It is permanent. Its skills the player's level has reached are learned at once;
        /// the rest come at their levels. Anything else is ignored: this is player input.
        /// </summary>
        private void TryChooseSpecialty(Player player, string specialtyId, int tick)
        {
            if (_gate != null) return;
            var rank = PickWaiting(player);
            var specialty = _config.SpecialtyById(specialtyId);
            if (rank == null || specialty == null || specialty.ClassId != player.Class?.Id || specialty.Rank != rank) return;
            if (!IsUnderPicks(player, specialty) || !IsSpecialtyBuilt(specialty)) return;

            player.Specialties.Add(specialty);
            _events.Add(new SpecialtyChosen(tick, player.Id, specialty.Id));
            foreach (var skill in _config.Skills.Where(s => s.SpecialtyId == specialty.Id && s.UnlockLevel <= player.Level).OrderBy(s => s.UnlockLevel))
            {
                if (player.KnownSkills.Contains(skill) || !IsForClass(skill, player)) continue;
                var slot = Learn(player, skill);
                _events.Add(new SkillUnlocked(tick, player.Id, skill, slot));
            }
        }

        /// <summary>
        /// A saved character's trials and picks, as far as this game still has them: a pick the game no longer has, from
        /// another class, a second pick at one tier, or one whose parent was dropped is dropped. A pick's trial counts as passed.
        /// </summary>
        private void RestoreSpecialties(Player player, CharacterRecord character)
        {
            player.Specialties.Clear();
            player.PassedTrials.Clear();
            foreach (var rank in character.PassedTrials ?? Enumerable.Empty<string>())
                if (_config.Progression.RankIndexOf(rank) >= 0 && !player.PassedTrials.Contains(rank)) player.PassedTrials.Add(rank);
            if (player.Class == null) return;

            var picks = (character.Specialties ?? Enumerable.Empty<string>())
                .Select(_config.SpecialtyById)
                .Where(s => s != null && s.ClassId == player.Class.Id)
                .OrderBy(s => _config.Progression.RankIndexOf(s.Rank));
            foreach (var specialty in picks)
            {
                if (player.Specialties.Any(s => s.Rank == specialty.Rank) || !IsUnderPicks(player, specialty)) continue;
                player.Specialties.Add(specialty);
                if (!player.PassedTrials.Contains(specialty.Rank)) player.PassedTrials.Add(specialty.Rank);
            }
        }

        /// <summary>
        /// Content validation: every class's trials are ranked gates in the game, one per tier; every specialty has a
        /// configured rank and, above the first tier, a parent of its own class at a lower tier; and every specialty
        /// skill belongs to its specialty's class.
        /// </summary>
        private void ValidateSpecialties()
        {
            var progression = _config.Progression;
            var ids = new HashSet<string>();
            var specialties = new Dictionary<string, SpecialtyDefinition>();
            foreach (var c in _config.Classes ?? Array.Empty<ClassDefinition>())
            {
                if (c == null) continue;
                var trialRanks = new HashSet<string>();
                foreach (var gateId in c.TrialGateIds)
                {
                    var gate = (_config.Gates ?? Array.Empty<GateDefinition>()).FirstOrDefault(g => g?.Id == gateId)
                        ?? throw new ArgumentException($"Class '{c.Id}': its trial gate '{gateId}' is not one of the game's gates.");
                    if (progression.RankIndexOf(gate.Rank) < 0)
                        throw new ArgumentException($"Class '{c.Id}': its trial gate '{gateId}' needs a rank, the tier it unlocks.");
                    if (!trialRanks.Add(gate.Rank))
                        throw new ArgumentException($"Class '{c.Id}': two trials at {gate.Rank}-rank; one per tier.");
                }

                var own = c.Specialties.Where(s => s?.Id != null).GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
                foreach (var s in c.Specialties)
                {
                    void Require(bool ok, string problem)
                    {
                        if (!ok) throw new ArgumentException($"Specialty '{s?.Id}' of class '{c.Id}': {problem}");
                    }

                    Require(s != null, "is empty.");
                    Require(!string.IsNullOrWhiteSpace(s.Id), "has no Id.");
                    Require(ids.Add(s.Id), "shares its Id with another specialty.");
                    Require(!string.IsNullOrWhiteSpace(s.Name), "has no Name.");
                    Require(s.ClassId == c.Id, $"names class '{s.ClassId}' but is listed under '{c.Id}'.");
                    Require(progression.RankIndexOf(s.Rank) >= 0, $"its rank '{s.Rank}' is not one of the progression's ranks.");
                    if (s.ParentId != null)
                    {
                        Require(own.TryGetValue(s.ParentId, out var parent), $"branches from '{s.ParentId}', which is not one of this class's specialties.");
                        Require(progression.RankIndexOf(parent.Rank) < progression.RankIndexOf(s.Rank), $"branches from '{s.ParentId}', which is not at a lower tier.");
                    }
                    foreach (var m in s.Passives) ValidateModifier(m);
                    specialties[s.Id] = s;
                }
            }
            foreach (var skill in _config.Skills)
            {
                if (skill.SpecialtyId == null) continue;
                if (!specialties.TryGetValue(skill.SpecialtyId, out var s))
                    throw new ArgumentException($"Skill '{skill.Id}': its specialty '{skill.SpecialtyId}' is not one of the game's specialties.");
                if (skill.ClassId != s.ClassId)
                    throw new ArgumentException($"Skill '{skill.Id}': it belongs to specialty '{s.Id}', so its class must be '{s.ClassId}', not '{skill.ClassId}'.");
            }
        }
    }
}
