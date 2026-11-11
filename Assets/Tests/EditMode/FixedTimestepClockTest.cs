using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    public class FixedTimestepClockTest
    {
        [Test]
        public void RunsOneTickPerTickIntervalRegardlessOfFrameRate()
        {
            var clock = new FixedTimestepClock(tickRate: 60);

            Assert.That(clock.Advance(1.0 / 30), Is.EqualTo(2), "30 fps frame");
            Assert.That(clock.Advance(1.0 / 120), Is.EqualTo(0), "half a tick accumulates");
            Assert.That(clock.Advance(1.0 / 120), Is.EqualTo(1), "second half completes the tick");
        }

        [Test]
        public void AlphaIsTheFractionOfTheNextTickElapsed()
        {
            var clock = new FixedTimestepClock(tickRate: 60);

            clock.Advance(1.5 / 60);

            Assert.That(clock.Alpha, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void LongStallsAreCappedSoTheGameDoesNotSpiral()
        {
            var clock = new FixedTimestepClock(tickRate: 60, maxTicksPerAdvance: 5);

            Assert.That(clock.Advance(2.0), Is.EqualTo(5));
            Assert.That(clock.Advance(0.0), Is.EqualTo(0), "the backlog is dropped, not carried over");
        }

        [Test]
        public void CappingKeepsTheFractionOfTheNextTickSoVisualsDoNotSnapBack()
        {
            var clock = new FixedTimestepClock(tickRate: 60, maxTicksPerAdvance: 5);

            clock.Advance(2.0 + 0.5 / 60);

            Assert.That(clock.Alpha, Is.EqualTo(0.5f).Within(1e-3f));
        }
    }
}
