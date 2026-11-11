using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>Progression: stat effects, XP, levels and stat points.</summary>
    public sealed partial class SimulationWorld
    {
        private int PointsAboveStart(Player player, Stat stat) => player.Stats[(int)stat] - _config.Progression.StartingStatValue;

        private int ComputeMaxHealth(Player player) =>
            Math.Max(1, _config.PlayerMaxHealth + PointsAboveStart(player, Stat.Vitality) * _config.StatEffects.HealthPerVitality);

        /// <summary>
        /// Damage raised by <paramref name="scalingStat"/>'s points above the start. Integer maths in
        /// thousandths, rounding halves up, so the result never depends on float precision.
        /// </summary>
        private int ScaledDamage(Player player, int baseDamage, Stat scalingStat)
        {
            var perPoint = scalingStat == Stat.Strength ? _damagePerMillePerStrength
                : scalingStat == Stat.Intelligence ? _damagePerMillePerIntelligence
                : 0;
            var perMille = 1000 + PointsAboveStart(player, scalingStat) * perPoint;
            return (int)Math.Max(0L, ((long)baseDamage * perMille + 500) / 1000);
        }

        /// <summary>XP needed to leave the current level, or 0 at the cap.</summary>
        private int XpToNextLevel(Player player) =>
            player.Level >= _config.Progression.LevelCap ? 0 : _config.Progression.XpToNextLevel[player.Level - 1];

        /// <summary>Every player gets the full reward: no splitting, no reduction for guests or carried players.</summary>
        private void AwardXp(Enemy enemy, int tick)
        {
            var reward = enemy.Definition.XpReward;
            if (reward <= 0) return;
            foreach (var player in _players) GainXp(player, reward, enemy.Id, tick);
        }

        private void GainXp(Player player, int amount, EnemyId source, int tick)
        {
            var gained = 0;
            var levelsGained = 0;
            while (amount > 0 && player.Level < _config.Progression.LevelCap)
            {
                var needed = XpToNextLevel(player) - player.Xp;
                var take = Math.Min(amount, needed);
                player.Xp += take;
                gained += take;
                amount -= take;
                if (player.Xp < XpToNextLevel(player)) break;

                player.Level++;
                player.Xp = 0;
                player.UnspentStatPoints += _config.Progression.FreePointsPerLevel;
                levelsGained++;
            }

            if (gained == 0) return; // at the cap: nothing counted
            _events.Add(new XpGained(tick, player.Id, gained, source));
            for (var i = levelsGained - 1; i >= 0; i--)
                _events.Add(new PlayerLeveledUp(tick, player.Id, player.Level - i, _config.Progression.FreePointsPerLevel));
            for (var i = levelsGained - 1; i >= 0; i--)
                UnlockSkillsForLevel(player, player.Level - i, tick);

            // The System restores a hunter who levels up.
            if (levelsGained > 0 && player.IsAlive) player.Health = player.MaxHealth;
        }

        private void TryAllocateStat(Player player, Stat stat, int tick)
        {
            if (player.UnspentStatPoints <= 0) return;
            player.UnspentStatPoints--;
            player.Stats[(int)stat]++;

            if (stat == Stat.Vitality)
            {
                var before = player.MaxHealth;
                player.MaxHealth = ComputeMaxHealth(player);
                if (player.IsAlive) player.Health += player.MaxHealth - before; // the new health arrives filled
            }

            _events.Add(new StatPointAllocated(tick, player.Id, stat, player.Stats[(int)stat]));
        }
    }
}
