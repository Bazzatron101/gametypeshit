using System;
using System.Collections.Generic;
using System.Linq;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// World operations from outside the players' commands: the host, gates, quests, gear and the
    /// test arena. Each one validates when called and applies at the start of the next tick, in
    /// call order, so it is ordered against that tick's commands and reproducible on replay.
    /// </summary>
    public sealed partial class SimulationWorld
    {
        private readonly List<Action<int>> _pendingOperations = new List<Action<int>>();

        private void Enqueue(Action<int> operation) => _pendingOperations.Add(operation);

        private void ApplyPendingOperations(int tick)
        {
            // Copy first: an operation may queue another, which then waits for the following tick.
            var operations = _pendingOperations.ToArray();
            _pendingOperations.Clear();
            foreach (var operation in operations) operation(tick);
        }

        /// <summary>Grants XP as if from a kill: level-ups, stat points, unlocks and their events all follow.</summary>
        public void GrantXp(PlayerId id, int amount)
        {
            var player = FindPlayer(id);
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "XP cannot be negative.");
            Enqueue(tick => GainXp(player, amount, source: null, tick));
        }

        /// <summary>
        /// Raises the player to <paramref name="level"/> (clamped to the cap) through the normal XP path.
        /// The XP is worked out when the operation applies, so repeated requests do not overshoot,
        /// and a lower level does nothing.
        /// </summary>
        public void RaiseToLevel(PlayerId id, int level)
        {
            var player = FindPlayer(id);
            Enqueue(tick =>
            {
                var xp = XpToReachLevel(id, level);
                if (xp > 0) GainXp(player, xp, source: null, tick);
            });
        }

        /// <summary>Total XP the player still needs to reach <paramref name="level"/> (clamped to the cap); 0 if already there.</summary>
        public int XpToReachLevel(PlayerId id, int level)
        {
            var player = FindPlayer(id);
            var target = Math.Min(level, _config.Progression.LevelCap);
            var needed = 0;
            for (var l = player.Level; l < target; l++) needed += _config.Progression.XpToNextLevel[l - 1];
            return Math.Max(0, needed - (target > player.Level ? player.Xp : 0));
        }

        /// <summary>Teaches a skill ahead of its unlock level, slotting it if the bar has room. Does nothing if already known.</summary>
        public void TeachSkill(PlayerId id, string skillId)
        {
            var player = FindPlayer(id);
            var skill = _config.Skills.FirstOrDefault(s => s.Id == skillId)
                ?? throw new ArgumentException($"No skill has the id '{skillId}'.", nameof(skillId));
            Enqueue(tick =>
            {
                if (player.KnownSkills.Contains(skill)) return;
                var slot = Learn(player, skill);
                _events.Add(new SkillUnlocked(tick, player.Id, skill, slot));
            });
        }

        /// <summary>Makes every attack, skill and the dodge ready again.</summary>
        public void ResetCooldowns(PlayerId id)
        {
            var player = FindPlayer(id);
            Enqueue(_ =>
            {
                player.SkillReadyAtTick.Clear();
                Array.Clear(player.AttackReadyAtTick, 0, player.AttackReadyAtTick.Length);
                player.DodgeReadyAtTick = 0;
            });
        }

        /// <summary>Fills health and brings a defeated player back. (Death rules will add their own revive.)</summary>
        public void RestoreHealth(PlayerId id)
        {
            var player = FindPlayer(id);
            Enqueue(_ =>
            {
                player.Health = player.MaxHealth;
                player.LifeState = PlayerLifeState.Alive;
                player.PendingAttackLandsAtTick = 0; // back on their feet, not mid-swing or mid-charge
                player.ClearCharge();
                player.DodgeEndsAtTick = 0;
                player.InvulnerableUntilTick = 0;
            });
        }

        public bool HasEnemy(EnemyId id) => _enemiesById.ContainsKey(id);

        /// <summary>
        /// Removes an enemy outright: no defeat, no XP. Immediate, like <see cref="SpawnEnemy"/>,
        /// because callers manage the enemy's presentation by id at the same moment.
        /// </summary>
        public void DespawnEnemy(EnemyId id)
        {
            if (!_enemiesById.TryGetValue(id, out var enemy)) throw new ArgumentException($"{id} is not in the simulation.", nameof(id));
            _enemiesById.Remove(id);
            _enemies.Remove(enemy);
            _enemyIds.Remove(id);
        }
    }
}
