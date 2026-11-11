using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    public class PlayerCommandBufferTest
    {
        [Test]
        public void PressOnAFrameWithNoTicksIsKeptForTheNextTick()
        {
            var buffer = new PlayerCommandBuffer();

            buffer.Add(new PlayerCommand(0f, 0f, lightAttack: true)); // frame 1: runs 0 ticks
            buffer.Add(new PlayerCommand(0f, 0f, lightAttack: false)); // frame 2: button already released

            Assert.That(buffer.TakeForTick().LightAttack, Is.True);
        }

        [Test]
        public void PressReachesOnlyTheFirstTickOfAFrame()
        {
            var buffer = new PlayerCommandBuffer();
            buffer.Add(new PlayerCommand(0f, 0f, lightAttack: true));

            Assert.That(buffer.TakeForTick().LightAttack, Is.True);
            Assert.That(buffer.TakeForTick().LightAttack, Is.False);
        }

        [Test]
        public void DodgePressIsKeptUntilATickTakesItThenGivenToThatTickOnly()
        {
            var buffer = new PlayerCommandBuffer();
            buffer.Add(new PlayerCommand(0f, 0f, dodge: true));
            buffer.Add(PlayerCommand.None);

            Assert.That(buffer.TakeForTick().Dodge, Is.True);
            Assert.That(buffer.TakeForTick().Dodge, Is.False);
        }

        [Test]
        public void HeavyAttackPressIsKeptUntilATickTakesItThenGivenToThatTickOnly()
        {
            var buffer = new PlayerCommandBuffer();
            buffer.Add(new PlayerCommand(0f, 0f, heavyAttack: true));
            buffer.Add(PlayerCommand.None);

            Assert.That(buffer.TakeForTick().HeavyAttack, Is.True);
            Assert.That(buffer.TakeForTick().HeavyAttack, Is.False);
        }

        [Test]
        public void MovementIsTheLatestSampleAndHoldsForEveryTick()
        {
            var buffer = new PlayerCommandBuffer();
            buffer.Add(new PlayerCommand(1f, 0f));
            buffer.Add(new PlayerCommand(0f, -1f));

            var first = buffer.TakeForTick();
            var second = buffer.TakeForTick();

            Assert.That((first.MoveX, first.MoveZ), Is.EqualTo((0f, -1f)));
            Assert.That((second.MoveX, second.MoveZ), Is.EqualTo((0f, -1f)));
        }

        [Test]
        public void EmptyBufferGivesNoInput()
        {
            var cmd = new PlayerCommandBuffer().TakeForTick();

            Assert.That((cmd.MoveX, cmd.MoveZ, cmd.LightAttack), Is.EqualTo((0f, 0f, false)));
        }
    }
}
