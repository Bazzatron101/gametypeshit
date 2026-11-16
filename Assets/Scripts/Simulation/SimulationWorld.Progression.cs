using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>Progression: stat effects, XP, levels and stat points.</summary>
    public sealed partial class SimulationWorld
    {
        /// <summary>
        /// The least a stat effect can scale move speed or damage to, however far worn gear pulls a stat
        /// below its start (ticket 51): a tenth. Max health has its own floor of 1.
        /// </summary>
        private const int MinStatScalePerMille = 100;

        /// <summary>
        /// The stat as it counts in play: the character's own value plus what worn gear adds (ticket 51).
        /// Every stat effect goes through here, so gear reaches them all at once.
        /// </summary>
        private static int EffectiveStat(Player player, Stat stat) => player.Stats[(int)stat] + GearStatBonus(player, stat);

        /// <summary>What the gear the player wears adds to <paramref name="stat"/> (negative for a loss).</summary>
        private static int GearStatBonus(Player player, Stat stat)
        {
            var total = 0;
            foreach (var gear in player.Worn)
                if (gear != null) total += gear.BonusTo(stat);
            return total;
        }

        /// <summary>Points above the start, counting worn gear. Below zero when gear pulls a stat under its start.</summary>
        private int PointsAboveStart(Player player, Stat stat) => EffectiveStat(player, stat) - _config.Progression.StartingStatValue;

        private int ComputeMaxHealth(Player player) =>
            Math.Max(1, _config.PlayerMaxHealth + PointsAboveStart(player, Stat.Vitality) * _config.StatEffects.HealthPerVitality);

        /// <summary>
        /// After worn gear changed: max health follows Vitality, but putting a piece on is never a free heal
        /// (ticket 51). Current health stays as it is when the max goes up, and is cut to the new max when it
        /// goes down.
        /// </summary>
        private void RefreshMaxHealthAfterGearChange(Player player)
        {
            player.MaxHealth = ComputeMaxHealth(player);
            if (player.Health > player.MaxHealth) player.Health = player.MaxHealth;
        }

        /// <summary>
        /// Damage raised by <paramref name="scalingStat"/>'s points above the start. Integer maths in
        /// thousandths, rounding halves up, so the result never depends on float precision.
        /// </summary>
        private int ScaledDamage(Player player, int baseDamage, Stat scalingStat)
        {
            var perPoint = scalingStat == Stat.Strength ? _damagePerMillePerStrength
                : scalingStat == Stat.Intelligence ? _damagePerMillePerIntelligence
                : scalingStat == Stat.Agility ? _damagePerMillePerAgility
                : scalingStat == Stat.Sense ? _damagePerMillePerSense
                : 0;
            var perMille = Math.Max(MinStatScalePerMille, 1000 + PointsAboveStart(player, scalingStat) * perPoint);
            return (int)Math.Max(0L, ((long)baseDamage * perMille + 500) / 1000);
        }

        /// <summary>XP needed to leave the current level, or 0 at the cap.</summary>
        private int XpToNextLevel(Player player) =>
            player.Level >= _config.Progression.LevelCap ? 0 : _config.Progression.XpToNextLevel[player.Level - 1];

        /// <summary>Every player gets the full reward: no splitting, no reduction for guests or carried players.</summary>
        private void AwardXp(Enemy enemy, int tick)
        {
            var reward = PartyScalingDefinition.Scale(enemy.Definition.XpReward, _gate?.XpPercent ?? 100);
            if (reward <= 0) return;
            foreach (var player in _players) GainXp(player, reward, enemy.Id, tick);
        }

        private void GainXp(Player player, int amount, EnemyId? source, int tick)
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
                GainClassStats(player);
                levelsGained++;
            }

            if (gained == 0) return; // at the cap: nothing counted
            _events.Add(new XpGained(tick, player.Id, gained, source));
            for (var i = levelsGained - 1; i >= 0; i--)
                _events.Add(new PlayerLeveledUp(tick, player.Id, player.Level - i, _config.Progression.FreePointsPerLevel));
            for (var i = levelsGained - 1; i >= 0; i--)
            {
                UnlockSkillsForLevel(player, player.Level - i, tick);
                UnlockChargesForLevel(player, player.Level - i, tick);
            }

            // The System restores a hunter who levels up.
            if (levelsGained > 0 && player.IsAlive) player.Health = player.MaxHealth;
        }

        /// <summary>A level's class stats, on top of the free points (spec: "a few free points per level plus class bonuses").</summary>
        private void GainClassStats(Player player)
        {
            if (player.Class == null) return;
            for (var i = 0; i < player.Stats.Length; i++) player.Stats[i] += player.Class.StatPerLevel(i);
            player.MaxHealth = ComputeMaxHealth(player);
        }

        private void TryAllocateStat(Player player, Stat stat, int tick)
        {
            if (player.UnspentStatPoints <= 0 || (int)stat < 0 || (int)stat >= player.Stats.Length) return;
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
