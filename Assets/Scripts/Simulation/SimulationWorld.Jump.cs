using System;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// Jumping: a hop along a fixed arc, steerable but with no attacking, skills or dodging in the
    /// air. Being in the air clears ground-level attacks (a boss's slam). Clearing an enemy itself
    /// (a boss's charge) takes a jump higher than the enemy is tall: beyond a base jump, so it comes
    /// from a raised jump height (classes).
    /// </summary>
    public sealed partial class SimulationWorld
    {
        private int JumpTicks => Math.Max(2, SecondsToTicks(_config.JumpSeconds));

        /// <summary>How long a sprint pauses after an attack or skill starts.</summary>
        private int SprintPauseTicks => SecondsToTicks(0.4f);

        /// <summary>True if, since <paramref name="eventsBefore"/>, this player swung, began a windup or charge, or used a skill.</summary>
        private bool StartedFighting(Player player, int eventsBefore)
        {
            for (var i = eventsBefore; i < _events.Count; i++)
            {
                switch (_events[i])
                {
                    case AttackPerformed a when a.Player == player.Id:
                    case AttackWindupStarted w when w.Player == player.Id:
                    case SkillUsed s when s.Player == player.Id:
                    case SkillChargeStarted c when c.Player == player.Id:
                        return true;
                }
            }
            return false;
        }

        private void TryJump(Player player, int tick)
        {
            if (player.IsAirborne || player.IsDodging(tick) || player.IsWindingUp || player.IsCharging) return;
            player.JumpStartTick = tick;
            _events.Add(new PlayerJumped(tick, player.Id));
        }

        /// <summary>Moves a jumping player along the arc for this tick, landing them when it ends.</summary>
        private void UpdateJump(Player player, int tick)
        {
            if (!player.IsAirborne) return;
            var elapsed = tick - player.JumpStartTick + 1;
            if (!player.IsAlive || elapsed >= JumpTicks)
            {
                Land(player);
                return;
            }
            var t = elapsed / (float)JumpTicks;
            var peak = _config.JumpHeight * player.JumpHeightPercent / 100f;
            player.Position = new Vec3(player.Position.X, 4f * peak * t * (1f - t), player.Position.Z);
        }

        private static void Land(Player player)
        {
            player.JumpStartTick = 0;
            player.Position = new Vec3(player.Position.X, 0f, player.Position.Z);
        }

        /// <summary>Whether <paramref name="player"/> is high enough in a jump to pass over <paramref name="enemy"/>.</summary>
        private static bool AboveEnemy(Player player, Enemy enemy) => player.IsAirborne && player.Position.Y >= enemy.Definition.Height;

        /// <summary>
        /// Sets how high this player jumps, as a percentage of the base jump: the hook for classes
        /// (ticket 24). Immediate. Not part of the character's save yet.
        /// </summary>
        public void SetJumpHeightPercent(PlayerId id, int percent)
        {
            if (percent < 0) throw new ArgumentOutOfRangeException(nameof(percent));
            FindPlayer(id).JumpHeightPercent = percent;
        }
    }
}
