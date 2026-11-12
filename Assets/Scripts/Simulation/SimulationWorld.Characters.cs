using System;
using System.Linq;

namespace HunterSystem.Simulation
{
    /// <summary>Characters entering and leaving a world: what a player keeps between sessions (ticket 15).</summary>
    public sealed partial class SimulationWorld
    {
        /// <summary>The player's character as it stands now, to save or to carry into another session.</summary>
        public CharacterRecord CaptureCharacter(PlayerId id)
        {
            if (!_playersById.TryGetValue(id, out var player)) throw new ArgumentException($"{id} is not in the simulation.", nameof(id));
            var record = new CharacterRecord
            {
                Level = player.Level,
                Xp = player.Xp,
                UnspentStatPoints = player.UnspentStatPoints,
                Stats = (int[])player.Stats.Clone(),
            };
            record.KnownSkills.AddRange(player.KnownSkills.Select(s => s.Id));
            for (var i = 0; i < SkillSlotCount; i++) record.SkillBar[i] = player.SkillSlots[i]?.Id;
            record.Modifiers.AddRange(player.Modifiers);
            return record;
        }

        /// <summary>
        /// Adds a player who brings <paramref name="character"/> with them (their own save, or a
        /// guest joining a host). The character arrives at full health with nothing on cooldown.
        /// <para>
        /// A record comes from the player's own machine, so nothing in it is trusted to be
        /// well-formed: values outside the rules are brought inside them (level to the cap, XP
        /// into its level, stats no lower than the start), skills the game no longer has are
        /// dropped, and skills its level should have (added to the game since) are learned.
        /// It is not checked for honesty; sessions are host-run among friends (spec, out of scope).
        /// </para>
        /// </summary>
        public void AddPlayer(PlayerId id, Vec3 spawnPosition, CharacterRecord character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            AddPlayer(id, spawnPosition);
            if (!character.IsNew) Apply(_playersById[id], character);
        }

        /// <summary>
        /// Swaps a player's character for <paramref name="character"/> where they stand (a returning
        /// guest whose own save is further along than the one the session kept). Immediate; the
        /// same rules as bringing a character in. A standing player is at full health afterwards;
        /// a defeated one stays defeated.
        /// </summary>
        public void ReplaceCharacter(PlayerId id, CharacterRecord character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (!_playersById.TryGetValue(id, out var player)) throw new ArgumentException($"{id} is not in the simulation.", nameof(id));
            if (character.IsNew) return;
            player.ClearCharge();
            player.SkillReadyAtTick.Clear();
            player.SkillCooldownTicks.Clear();
            var defeated = !player.IsAlive;
            Apply(player, character);
            if (defeated) player.Health = 0; // swapping characters is not a revive
        }

        private void Apply(Player player, CharacterRecord character)
        {
            var progression = _config.Progression;

            player.Level = Math.Min(Math.Max(character.Level, 1), progression.LevelCap);
            player.Xp = Math.Min(Math.Max(character.Xp, 0), Math.Max(0, XpToNextLevel(player) - 1));
            player.UnspentStatPoints = Math.Max(character.UnspentStatPoints, 0);
            for (var i = 0; i < player.Stats.Length; i++)
            {
                var saved = character.Stats != null && i < character.Stats.Length ? character.Stats[i] : progression.StartingStatValue;
                player.Stats[i] = Math.Max(saved, progression.StartingStatValue);
            }

            // Skills: what it knew and the game still has, in the order it learned them...
            player.KnownSkills.Clear();
            Array.Clear(player.SkillSlots, 0, SkillSlotCount);
            foreach (var skillId in character.KnownSkills ?? Enumerable.Empty<string>())
            {
                var skill = _config.Skills.FirstOrDefault(s => s.Id == skillId);
                if (skill != null && !player.KnownSkills.Contains(skill)) player.KnownSkills.Add(skill);
            }
            // ...on the bar where it kept them (a skill in one slot only)...
            for (var slot = 0; slot < SkillSlotCount && character.SkillBar != null && slot < character.SkillBar.Length; slot++)
            {
                var skill = player.KnownSkills.FirstOrDefault(s => s.Id == character.SkillBar[slot]);
                if (skill != null && Array.IndexOf(player.SkillSlots, skill) < 0) player.SkillSlots[slot] = skill;
            }
            // ...plus anything its level should have that it does not (skills added to the game since it was saved).
            foreach (var skill in _config.Skills)
                if (skill.UnlockLevel <= player.Level && !player.KnownSkills.Contains(skill)) Learn(player, skill);

            player.Modifiers.Clear();
            foreach (var modifier in character.Modifiers ?? Enumerable.Empty<ModifierDefinition>())
                if (modifier != null && !player.Modifiers.Any(m => m.Id == modifier.Id)) player.Modifiers.Add(modifier);

            player.MaxHealth = ComputeMaxHealth(player);
            player.Health = player.MaxHealth;
        }
    }
}
