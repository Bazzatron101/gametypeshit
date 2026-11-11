using NUnit.Framework;

namespace HunterSystem.Tests
{
    public class SmokeTest
    {
        [Test]
        public void HeadlessTestRunnerExecutesTests()
        {
            Assert.That(1 + 1, Is.EqualTo(2));
        }
    }
}
