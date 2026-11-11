using System;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// Turns variable frame time into a whole number of fixed simulation ticks, so the
    /// simulation runs at the same rate whatever the frame rate.
    /// </summary>
    public sealed class FixedTimestepClock
    {
        private readonly double _tickInterval;
        private readonly int _maxTicksPerAdvance;
        private double _accumulator;

        public FixedTimestepClock(int tickRate, int maxTicksPerAdvance = 5)
        {
            if (tickRate <= 0) throw new ArgumentOutOfRangeException(nameof(tickRate));
            if (maxTicksPerAdvance <= 0) throw new ArgumentOutOfRangeException(nameof(maxTicksPerAdvance));
            _tickInterval = 1.0 / tickRate;
            _maxTicksPerAdvance = maxTicksPerAdvance;
        }

        /// <summary>How far (0 to 1) real time has progressed toward the next tick. Use it to interpolate visuals.</summary>
        public float Alpha => (float)(_accumulator / _tickInterval);

        /// <summary>
        /// Adds elapsed real time and returns how many ticks to run now. After a long stall the
        /// count is capped and the backlog dropped, so slow frames cannot snowball into slower ones.
        /// </summary>
        public int Advance(double elapsedSeconds)
        {
            if (elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            _accumulator += elapsedSeconds;

            // Small epsilon so floating-point error in frame deltas (1/120 + 1/120) still completes a tick.
            var ticks = (int)Math.Floor(_accumulator / _tickInterval + 1e-9);

            // Whole ticks are consumed either way; past the cap they are dropped rather than run.
            // The fraction of the next tick is kept so interpolation does not snap back.
            _accumulator = Math.Max(0, _accumulator - ticks * _tickInterval);
            return Math.Min(ticks, _maxTicksPerAdvance);
        }
    }
}
