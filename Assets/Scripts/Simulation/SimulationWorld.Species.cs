using System;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// Species perks (ticket 81, ADR 0024): small nudges, run by the same rules on host and clients. Each hooks
    /// into the one place its rule already lives (damage, cooldowns, jumps, finding gates, the clear draw).
    /// </summary>
    public sealed partial class SimulationWorld
    {
        private static bool HasPerk(Player player, SpeciesPerk perk) => player?.Species != null && player.Species.Perk == perk;

        /// <summary>Tasmanian devil: a hit on an enemy at or below the threshold of its health does more damage.</summary>
        private static int WithFinishingBlow(Player attacker, Enemy enemy, int damage)
        {
            if (!HasPerk(attacker, SpeciesPerk.FinishingBlow) || enemy.MaxHealth <= 0) return damage;
            var species = attacker.Species;
            if (enemy.Health * 100f > enemy.MaxHealth * species.PerkThreshold) return damage;
            return (int)MathF.Round(damage * (100f + species.PerkAmount) / 100f, MidpointRounding.AwayFromZero);
        }

        /// <summary>Kangaroo: an ordinary enemy still standing after the hit is pushed straight away from the hunter. Bosses stand firm.</summary>
        private void Knockback(Player attacker, Enemy enemy)
        {
            if (!HasPerk(attacker, SpeciesPerk.Knockback) || enemy.Health <= 0 || enemy.Definition.Boss != null) return;
            var away = Flat(enemy.Position - attacker.Position);
            var length = away.Length;
            if (length < Epsilon) away = attacker.Facing; else away *= 1f / length;
            var pushed = enemy.Position + away * attacker.Species.PerkAmount;
            enemy.Position = OnGround(Ground.Confine(enemy.Position, pushed));
        }

        /// <summary>
        /// Wombat: a hit from behind does less. "Behind" is outside the front half of where the hunter faces;
        /// an attacker standing on top of them counts as in front.
        /// </summary>
        private static int WithThickHide(Player target, Vec3 attackerPosition, int damage)
        {
            if (!HasPerk(target, SpeciesPerk.ThickHide)) return damage;
            var toAttacker = Flat(attackerPosition - target.Position);
            var distance = toAttacker.Length;
            if (distance < Epsilon) return damage;
            var cos = (toAttacker.X * target.Facing.X + toAttacker.Z * target.Facing.Z) / distance;
            if (cos >= 0f) return damage;
            return (int)MathF.Round(damage * (100f - target.Species.PerkAmount) / 100f, MidpointRounding.AwayFromZero);
        }

        /// <summary>Cockatoo: a skill's cooldown, shortened. Never below one tick.</summary>
        private static int WithQuickThinking(Player player, int cooldownTicks)
        {
            if (!HasPerk(player, SpeciesPerk.QuickThinking) || cooldownTicks <= 0) return cooldownTicks;
            return Math.Max(1, (int)MathF.Round(cooldownTicks * (100f - player.Species.PerkAmount) / 100f, MidpointRounding.AwayFromZero));
        }

        /// <summary>Platypus: how far away this player finds hidden things, from the usual <paramref name="radius"/>.</summary>
        private static float SenseRadius(Player player, float radius) =>
            HasPerk(player, SpeciesPerk.KeenSenses) ? radius * (100f + player.Species.PerkAmount) / 100f : radius;

        /// <summary>The Luck that tips <paramref name="id"/>'s clear draw (ticket 61): Luck points above the start, plus the quokka's perk.</summary>
        public int DropLuck(PlayerId id) => DropLuck(FindPlayer(id));

        private int DropLuck(Player player) => Math.Max(0, PointsAboveStart(player, Stat.Luck)) + LuckyDrops(player);

        /// <summary>Quokka: Luck points added to the clear draw, on top of the Luck stat.</summary>
        private static int LuckyDrops(Player player) =>
            HasPerk(player, SpeciesPerk.Lucky) ? (int)MathF.Round(player.Species.PerkAmount, MidpointRounding.AwayFromZero) : 0;

        /// <summary>
        /// Sugar glider: how many ticks the way down from a jump takes; the way up takes half a normal jump.
        /// Everyone else comes down as fast as they went up.
        /// </summary>
        private int JumpFallTicks(Player player)
        {
            var half = JumpTicks / 2f;
            return HasPerk(player, SpeciesPerk.Glide)
                ? Math.Max(1, (int)MathF.Round(half * (100f + player.Species.PerkAmount) / 100f, MidpointRounding.AwayFromZero))
                : JumpTicks - JumpTicks / 2;
        }

        /// <summary>The player hit something or was hit: the koala's healing waits for a quiet spell again.</summary>
        private static void NoteFight(Player player, int tick) => player.LastFightTick = tick;

        /// <summary>
        /// Koala: once a second, after enough quiet seconds without hitting or being hit, regains a share of max
        /// health. Quiet: no event, like the health bar filling.
        /// </summary>
        private void UpdateRestfulHealing(Player player, int tick)
        {
            if (!HasPerk(player, SpeciesPerk.RestfulHealing) || !player.IsAlive || player.Health >= player.MaxHealth) return;
            if (tick % _config.TickRate != 0) return;
            if (player.LastFightTick != 0 && tick - player.LastFightTick < SecondsToTicks(player.Species.PerkThreshold)) return;
            var amount = Math.Max(1, (int)MathF.Round(player.MaxHealth * player.Species.PerkAmount / 100f, MidpointRounding.AwayFromZero));
            player.Health = Math.Min(player.MaxHealth, player.Health + amount);
        }
    }
}
