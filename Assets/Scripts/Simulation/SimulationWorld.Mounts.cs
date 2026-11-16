using System;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// Mounts (ticket 42). A character that owns one can ride it out in the overworld, at
    /// <see cref="SimulationConfig.MountSpeed"/> (2× sprint), whatever their agility. There is no fighting
    /// from the saddle: a hit, or starting an attack, a skill or a dodge, puts the rider on foot. So does
    /// entering a gate or the guild town, or going down. Jumping is fine. A mount does not climb: climbable
    /// faces are cliffs to a rider (developer's choices, 2026-11-16). The mount belongs to the character
    /// (ADR 0005), given at the D-rank promotion once ranks exist (ticket 25).
    /// </summary>
    public sealed partial class SimulationWorld
    {
        /// <summary>
        /// Gives <paramref name="id"/>'s character a mount, at the start of the next tick: the test arena's and F2's
        /// shortcut. Reaching the rank that grants one (D) gives it without this (ticket 25). Host only.
        /// </summary>
        public void GrantMount(PlayerId id)
        {
            var player = FindPlayer(id);
            Enqueue(tick =>
            {
                if (player.HasMount) return;
                player.HasMount = true;
                _events.Add(new MountGranted(tick, player.Id));
            });
        }

        /// <summary>Whether <paramref name="id"/> could get on their mount where they stand now.</summary>
        public bool CanMount(PlayerId id)
        {
            var p = FindPlayer(id);
            return p.HasMount && !p.Mounted && CanRideAt(p);
        }

        /// <summary>Riding is for the overworld outside the town.</summary>
        private bool CanRideAt(Player p) => InOverworld && !InTown(p.Position);

        /// <summary>Inside the guild town's reach (the hub's centre, out in the overworld).</summary>
        private bool InTown(Vec3 p) =>
            _config.Overworld != null && _config.Hub != null && Flat(p - _config.Hub.Centre).Length <= _config.Overworld.TownRadius;

        private void TryToggleMount(Player p, int tick)
        {
            if (p.Mounted)
            {
                Dismount(p, tick);
                return;
            }
            if (!p.HasMount || !p.IsAlive || p.IsAirborne || p.IsDodging(tick) || p.IsWindingUp || p.IsCharging || !CanRideAt(p)) return;
            p.Mounted = true;
            _events.Add(new MountChanged(tick, p.Id, true));
        }

        private void Dismount(Player p, int tick)
        {
            if (!p.Mounted) return;
            p.Mounted = false;
            _events.Add(new MountChanged(tick, p.Id, false));
        }

        /// <summary>After everyone has moved: a rider who has gone down, or ridden into the town, is on foot.</summary>
        private void DismountWhereRidingEnds(int tick)
        {
            foreach (var p in _players)
                if (p.Mounted && (!p.IsAlive || !CanRideAt(p))) Dismount(p, tick);
        }
    }
}
