using HunterSystem.Game;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    public class AnnouncementQueueTest
    {
        [Test]
        public void AnnouncementsShowOneAfterAnotherInOrder()
        {
            var queue = new AnnouncementQueue(showSeconds: 3f);
            queue.Enqueue("LEVEL UP", "Level 3", now: 10f);
            queue.Enqueue("NEW SKILL", "Flame Burst", now: 10f);

            Assert.That(queue.Current(now: 10f)?.Title, Is.EqualTo("LEVEL UP"));
            Assert.That(queue.Current(now: 12.9f)?.Title, Is.EqualTo("LEVEL UP"));
            Assert.That(queue.Current(now: 13.1f)?.Title, Is.EqualTo("NEW SKILL"));
            Assert.That(queue.Current(now: 16.2f), Is.Null);
        }

        [Test]
        public void AnnouncementFadesOutAtTheEndOfItsTime()
        {
            var queue = new AnnouncementQueue(showSeconds: 3f, fadeSeconds: 0.5f);
            queue.Enqueue("LEVEL UP", "", now: 0f);

            Assert.That(queue.Current(now: 1f).Value.Alpha, Is.EqualTo(1f));
            Assert.That(queue.Current(now: 2.75f).Value.Alpha, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void ALaterAnnouncementWaitsItsTurnRatherThanCuttingIn()
        {
            var queue = new AnnouncementQueue(showSeconds: 3f);
            queue.Enqueue("FIRST", "", now: 0f);
            queue.Enqueue("SECOND", "", now: 1f);

            Assert.That(queue.Current(now: 1.5f)?.Title, Is.EqualTo("FIRST"));
            Assert.That(queue.Current(now: 3.5f)?.Title, Is.EqualTo("SECOND"));
        }
    }
}
