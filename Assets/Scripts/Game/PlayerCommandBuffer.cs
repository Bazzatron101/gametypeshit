using System.Collections.Generic;
using HunterSystem.Simulation;

namespace HunterSystem.Game
{
    /// <summary>
    /// Bridges per-frame input sampling to per-tick commands. Movement is the latest sample and
    /// holds for every tick. A button press is kept until a tick takes it, then given to that
    /// tick only. So a quick tap on a frame that runs no ticks is not lost, and a frame that
    /// runs several ticks does not repeat it.
    /// </summary>
    public sealed class PlayerCommandBuffer
    {
        private float _moveX, _moveZ;
        private bool _lightAttackPending;
        private bool _heavyAttackPending;
        private bool _dodgePending;
        private readonly Queue<Stat> _statAllocations = new Queue<Stat>();

        /// <summary>Call once per frame with that frame's sampled input.</summary>
        public void Add(PlayerCommand sampled)
        {
            _moveX = sampled.MoveX;
            _moveZ = sampled.MoveZ;
            _lightAttackPending |= sampled.LightAttack;
            _heavyAttackPending |= sampled.HeavyAttack;
            _dodgePending |= sampled.Dodge;
        }

        /// <summary>Queues one stat point (from a UI click). Commands carry one per tick, in click order.</summary>
        public void QueueStatAllocation(Stat stat) => _statAllocations.Enqueue(stat);

        /// <summary>Call once per simulation tick.</summary>
        public PlayerCommand TakeForTick()
        {
            Stat? allocate = _statAllocations.Count > 0 ? _statAllocations.Dequeue() : (Stat?)null;
            var command = new PlayerCommand(_moveX, _moveZ,
                lightAttack: _lightAttackPending, dodge: _dodgePending, heavyAttack: _heavyAttackPending,
                allocateStat: allocate);
            _lightAttackPending = false;
            _heavyAttackPending = false;
            _dodgePending = false;
            return command;
        }
    }
}
