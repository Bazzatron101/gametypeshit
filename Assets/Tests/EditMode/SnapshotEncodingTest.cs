using System;
using System.Linq;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Ticket 31: snapshots go compressed, and written against the last world the client was sent, so a quiet tick
    /// costs almost nothing. These check the wire format on its own; <see cref="NetSessionTest"/> checks it in sessions.
    /// </summary>
    public class SnapshotEncodingTest
    {
        private static readonly EnemyDefinition Goblin = new EnemyDefinition
        {
            Name = "Goblin", MaxHealth = 30, MoveSpeed = 3f, AttackDamage = 5, AttackRange = 1.5f, XpReward = 10,
        };

        private static SimulationWorld Fight(int players, int enemies)
        {
            var world = new SimulationWorld(new SimulationConfig { TickRate = 60, PlayerMoveSpeed = 5f });
            for (var i = 1; i <= players; i++) world.AddPlayer(new PlayerId(i), new Vec3(i * 2f, 0f, 0f));
            for (var i = 0; i < enemies; i++) world.SpawnEnemy(Goblin, new Vec3(i * 2f, 0f, 8f));
            return world;
        }

        private static void Step(SimulationWorld world, int ticks)
        {
            var commands = world.PlayerIds.ToDictionary(id => id, id => new PlayerCommand(id.Value % 2 == 0 ? 1f : -1f, 0.5f));
            for (var i = 0; i < ticks; i++) world.Step(commands);
        }

        private static SnapshotMessage Message(SimulationWorld world)
        {
            var message = new SnapshotMessage { Tick = world.Tick, World = world.CaptureSnapshot() };
            message.LastProcessedSequence[2] = 41;
            message.FarPlayers[7] = (12f, -3f);
            message.Events = new byte[] { 1, 2, 3 };
            return message;
        }

        [Test]
        public void ASnapshotWrittenAgainstTheLastWorldReadsBackWhole()
        {
            var world = Fight(players: 10, enemies: 10);
            var baseline = world.CaptureSnapshot();
            Step(world, 3);
            var sent = Message(world);

            var read = Wire.ReadSnapshot(Wire.Snapshot(sent, baseline), baseline);

            Assert.That(read.World, Is.EqualTo(sent.World));
            Assert.That(read.Tick, Is.EqualTo(sent.Tick));
            Assert.That(read.LastProcessedSequence[2], Is.EqualTo(41));
            Assert.That(read.FarPlayers[7], Is.EqualTo((12f, -3f)));
            Assert.That(read.Events, Is.EqualTo(sent.Events));
        }

        [Test]
        public void AgainstTheLastWorldASnapshotIsAFractionOfTheWholeWorld()
        {
            var world = Fight(players: 10, enemies: 10);
            var baseline = world.CaptureSnapshot();
            Step(world, 3);
            var sent = Message(world);

            var plain = sent.World.Length;
            var compressed = Wire.Snapshot(sent).Length;
            var delta = Wire.Snapshot(sent, baseline).Length;
            TestContext.WriteLine($"world {plain} B; compressed {compressed} B; against the last world {delta} B");

            Assert.That(compressed, Is.LessThan(plain / 2), "compression alone at least halves it");
            Assert.That(delta, Is.LessThan(compressed), "and the difference from the last world is smaller still");
        }

        [Test]
        public void WhenSomethingArrivesTheWholeWorldGoesAndReadsWithoutTheBaseline()
        {
            var world = Fight(players: 2, enemies: 1);
            var baseline = world.CaptureSnapshot();
            world.SpawnEnemy(Goblin, new Vec3(0f, 0f, -5f));
            var sent = Message(world);
            Assert.That(sent.World.Length, Is.Not.EqualTo(baseline.Length), "the test needs a world of a new size");

            var message = Wire.Snapshot(sent, baseline);

            Assert.That(Wire.ReadSnapshot(message, baseline).World, Is.EqualTo(sent.World));
            Assert.That(Wire.ReadSnapshot(message, null).World, Is.EqualTo(sent.World), "a whole world needs no baseline");
        }

        [Test]
        public void ASnapshotWrittenAgainstAWorldTheClientNeverHadIsRefused()
        {
            var world = Fight(players: 2, enemies: 1);
            var baseline = world.CaptureSnapshot();
            Step(world, 3);
            var message = Wire.Snapshot(Message(world), baseline);

            Assert.Throws<FormatException>(() => Wire.ReadSnapshot(message, null));
            Assert.Throws<FormatException>(() => Wire.ReadSnapshot(message, new byte[baseline.Length + 1]));
        }

        [Test]
        public void AWelcomeCarriesTheWholeWorldCompressed()
        {
            var world = Fight(players: 10, enemies: 10);
            var sent = Message(world);

            var welcome = Wire.Welcome(new PlayerId(3), sent);
            var (you, read) = Wire.ReadWelcome(welcome);

            Assert.That(you, Is.EqualTo(new PlayerId(3)));
            Assert.That(read.World, Is.EqualTo(sent.World));
            Assert.That(welcome.Length, Is.LessThan(sent.World.Length));
        }
    }
}
