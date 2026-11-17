using System;

namespace HunterSystem.Simulation
{
    /// <summary>Why a player cannot get on their mount right now (ticket 42), so the game can say so.</summary>
    public enum MountBlock
    {
        None,
        NoMount,
        Defeated,
        NotInOverworld,
        Busy,
    }

    /// <summary>
    /// Mounts (ticket 42). A character that owns one can ride it out in the overworld, at
    /// <see cref="SimulationConfig.MountSpeed"/> (2× sprint), whatever their agility. There is no fighting
    /// from the saddle: a hit, or starting an attack, a skill or a dodge, puts the rider on foot. So does
    /// entering a gate, or going down (riding in the town is allowed for now). Jumping is fine. A mount does not climb: climbable
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

        /// <summary>
        /// Why <paramref name="id"/> cannot get on a mount right now, or <see cref="MountBlock.None"/> if they can
        /// (or are already riding). For telling the player why R did nothing.
        /// </summary>
        public MountBlock WhyCannotMount(PlayerId id)
        {
            var p = FindPlayer(id);
            if (p.Mounted) return MountBlock.None;
            if (!p.HasMount) return MountBlock.NoMount;
            if (!p.IsAlive) return MountBlock.Defeated;
            if (!InOverworld) return MountBlock.NotInOverworld;
            if (p.IsAirborne || p.IsDodging(Tick) || p.IsWindingUp || p.IsCharging) return MountBlock.Busy;
            return MountBlock.None;
        }

        /// <summary>Whether <paramref name="id"/> could get on their mount where they stand now.</summary>
        public bool CanMount(PlayerId id)
        {
            var p = FindPlayer(id);
            return p.HasMount && !p.Mounted && CanRideAt(p);
        }

        /// <summary>
        /// Riding is for the overworld, the guild town included: the developer lifted the town rule for now
        /// (2026-11-17, playtest). Inside a gate a rider is on foot.
        /// </summary>
        private bool CanRideAt(Player p) => InOverworld;

        private void TryToggleMount(Player p, int tick)
        {
            if (p.Mounted)
            {
                if (CanLandAt(p)) Dismount(p, tick); // a flyer lands only on ground they can stand on (ticket 90)
                return;
            }
            if (!p.HasMount || !p.IsAlive || p.IsAirborne || p.IsDodging(tick) || p.IsWindingUp || p.IsCharging || !CanRideAt(p)) return;
            p.Mounted = true;
            TakeOff(p);
            _events.Add(new MountChanged(tick, p.Id, true));
        }

        private void Dismount(Player p, int tick)
        {
            if (!p.Mounted) return;
            p.Mounted = false;
            Touchdown(p); // a flyer drops to the ground below (ticket 90)
            _events.Add(new MountChanged(tick, p.Id, false));
        }

        /// <summary>After everyone has moved: a rider who has gone down, or is no longer in the overworld, is on foot.</summary>
        private void DismountWhereRidingEnds(int tick)
        {
            foreach (var p in _players)
                if (p.Mounted && (!p.IsAlive || !CanRideAt(p))) Dismount(p, tick);
        }
    }
}
