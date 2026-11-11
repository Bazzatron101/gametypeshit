using System;
using System.Collections.Generic;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    public class SimulationMovementTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);
        private static readonly PlayerId P2 = new PlayerId(2);

        private static SimulationConfig Config => new SimulationConfig { TickRate = 60, PlayerMoveSpeed = 5f };

        private static Dictionary<PlayerId, PlayerCommand> Commands(params (PlayerId id, PlayerCommand cmd)[] entries)
        {
            var d = new Dictionary<PlayerId, PlayerCommand>();
            foreach (var (id, cmd) in entries) d[id] = cmd;
            return d;
        }

        private static void Run(SimulationWorld sim, int ticks, Dictionary<PlayerId, PlayerCommand> commands)
        {
            for (var i = 0; i < ticks; i++) sim.Step(commands);
        }

        [Test]
        public void ScriptedMoveForOneSecondMovesPlayerByMoveSpeed()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, Vec3.Zero);

            Run(sim, 60, Commands((P1, new PlayerCommand(moveX: 1f, moveZ: 0f))));

            var pos = sim.GetPlayer(P1).Position;
            Assert.That(pos.X, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(pos.Y, Is.EqualTo(0f));
            Assert.That(pos.Z, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(sim.Tick, Is.EqualTo(60));
        }

        [Test]
        public void MovementStartsFromSpawnPosition()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, new Vec3(2f, 0f, -3f));

            Run(sim, 30, Commands((P1, new PlayerCommand(0f, 1f))));

            var pos = sim.GetPlayer(P1).Position;
            Assert.That(pos.X, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(pos.Z, Is.EqualTo(-0.5f).Within(1e-4f));
        }

        [Test]
        public void DiagonalInputIsNotFasterThanStraightInput()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, Vec3.Zero);

            Run(sim, 60, Commands((P1, new PlayerCommand(1f, 1f))));

            Assert.That(sim.GetPlayer(P1).Position.Length, Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void PartialStickInputMovesProportionallySlower()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, Vec3.Zero);

            Run(sim, 60, Commands((P1, new PlayerCommand(0.5f, 0f))));

            Assert.That(sim.GetPlayer(P1).Position.X, Is.EqualTo(2.5f).Within(1e-4f));
        }

        [Test]
        public void PlayerWithoutACommandThisTickStaysStill()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, Vec3.Zero);

            Run(sim, 10, Commands());

            Assert.That(sim.GetPlayer(P1).Position, Is.EqualTo(Vec3.Zero));
            Assert.That(sim.Tick, Is.EqualTo(10));
        }

        [Test]
        public void EachPlayerIsDrivenOnlyByTheirOwnCommands()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, Vec3.Zero);
            sim.AddPlayer(P2, new Vec3(10f, 0f, 0f));

            Run(sim, 60, Commands((P1, new PlayerCommand(1f, 0f)), (P2, new PlayerCommand(0f, -1f))));

            Assert.That(sim.GetPlayer(P1).Position.X, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(sim.GetPlayer(P1).Position.Z, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(sim.GetPlayer(P2).Position.X, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(sim.GetPlayer(P2).Position.Z, Is.EqualTo(-5f).Within(1e-4f));
            CollectionAssert.AreEquivalent(new[] { P1, P2 }, sim.PlayerIds);
        }

        [Test]
        public void SameCommandsProduceIdenticalStateInTwoSimulations()
        {
            var a = new SimulationWorld(Config);
            var b = new SimulationWorld(Config);
            a.AddPlayer(P1, Vec3.Zero);
            b.AddPlayer(P1, Vec3.Zero);
            var script = new[] { new PlayerCommand(1f, 0.3f), new PlayerCommand(-0.2f, 1f), new PlayerCommand(0f, 0f) };

            for (var i = 0; i < 90; i++)
            {
                var cmd = Commands((P1, script[i % script.Length]));
                a.Step(cmd);
                b.Step(cmd);
            }

            Assert.That(a.GetPlayer(P1).Position, Is.EqualTo(b.GetPlayer(P1).Position));
        }

        [Test]
        public void AddingTheSamePlayerTwiceIsRejected()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, Vec3.Zero);
            Assert.Throws<ArgumentException>(() => sim.AddPlayer(P1, Vec3.Zero));
        }

        [Test]
        public void CommandForUnknownPlayerIsRejected()
        {
            var sim = new SimulationWorld(Config);
            Assert.Throws<ArgumentException>(() => sim.Step(Commands((P2, new PlayerCommand(1f, 0f)))));
        }
    }
}
