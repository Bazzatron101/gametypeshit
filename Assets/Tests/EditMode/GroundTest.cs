using System.Collections.Generic;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The ground (ticket 37): walking, jumping and landing follow its height; nobody walks onto a
    /// cliff, the sea or off the edge of the map, and walking into one slides along it; a climbable
    /// face can be walked onto; a straight line stops where the ground does.
    /// </summary>
    public class GroundTest
    {
        private static readonly PlayerId P = new PlayerId(1);

        /// <summary>
        /// A 20 × 20 m hill of 1 m cells from (-10, -10): it rises 0.2 m per metre east, so the height
        /// at x is 0.2 × (x + 10). A cliff runs north–south at x 5–6, the sea is south of z -7, and the
        /// row z 7–8 is a climbable face.
        /// </summary>
        internal static HeightmapGround Hill()
        {
            const int size = 20;
            var heights = new float[(size + 1) * (size + 1)];
            for (var row = 0; row <= size; row++)
                for (var column = 0; column <= size; column++)
                    heights[row * (size + 1) + column] = 0.2f * column;
            var kinds = new GroundKind[size * size];
            for (var row = 0; row < size; row++)
                for (var column = 0; column < size; column++)
                {
                    var kind = GroundKind.Walkable;
                    if (row < 3) kind = GroundKind.Water;
                    else if (column == 15) kind = GroundKind.Blocked;
                    else if (row == 17) kind = GroundKind.Climbable;
                    kinds[row * size + column] = kind;
                }
            return new HeightmapGround(-10f, -10f, 1f, size, size, heights, kinds);
        }

        private static float HillHeight(float x) => 0.2f * (x + 10f);

        private static SimulationConfig Config() => new SimulationConfig
        {
            TickRate = 60, PlayerMoveSpeed = 5f, JumpHeight = 0.9f, JumpSeconds = 0.6f, Ground = Hill(),
        };

        private static SimulationWorld World(Vec3 start)
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P, start);
            return world;
        }

        private static void Step(SimulationWorld world, PlayerCommand command, int ticks = 1)
        {
            for (var i = 0; i < ticks; i++) world.Step(new Dictionary<PlayerId, PlayerCommand> { [P] = command });
        }

        private static Vec3 Where(SimulationWorld world) => world.GetPlayer(P).Position;

        // --- The heightmap ------------------------------------------------------------------------

        [Test]
        public void HeightIsBlendedBetweenTheSamples()
        {
            var ground = new HeightmapGround(0f, 0f, 2f, 1, 1, new[] { 0f, 1f, 2f, 3f });

            Assert.That(ground.HeightAt(new Vec3(0f, 0f, 0f)), Is.EqualTo(0f));
            Assert.That(ground.HeightAt(new Vec3(2f, 0f, 2f)), Is.EqualTo(3f));
            Assert.That(ground.HeightAt(new Vec3(1f, 0f, 1f)), Is.EqualTo(1.5f).Within(1e-5f), "the middle of the cell");
            Assert.That(ground.HeightAt(new Vec3(1f, 0f, 0f)), Is.EqualTo(0.5f).Within(1e-5f), "halfway along the south edge");
            Assert.That(ground.HeightAt(new Vec3(-5f, 0f, -5f)), Is.EqualTo(0f), "off the grid: the height at its edge");
        }

        [Test]
        public void OffTheGridIsBlocked()
        {
            var ground = Hill();

            Assert.That(ground.KindAt(new Vec3(-10.01f, 0f, 0f)), Is.EqualTo(GroundKind.Blocked));
            Assert.That(ground.KindAt(new Vec3(0f, 0f, 10f)), Is.EqualTo(GroundKind.Blocked), "the far edge is outside");
            Assert.That(ground.KindAt(new Vec3(0f, 0f, 0f)), Is.EqualTo(GroundKind.Walkable));
        }

        [Test]
        public void AStraightLineStopsAtTheCliff()
        {
            var reach = Hill().ReachAlong(new Vec3(0f, 0f, 0f), new Vec3(1f, 0f, 0f), 10f);

            Assert.That(reach, Is.LessThan(5f).And.GreaterThan(4.7f), "the cliff starts 5 m east");
            Assert.That(Hill().ReachAlong(new Vec3(0f, 0f, 0f), new Vec3(0f, 0f, 1f), 3f), Is.EqualTo(3f), "nothing in the way");
        }

        // --- Walking ------------------------------------------------------------------------------

        [Test]
        public void WalkingUphillFollowsTheGround()
        {
            var world = World(new Vec3(0f, 0f, 0f));

            Step(world, new PlayerCommand(1f, 0f), 36); // 3 m east

            Assert.That(Where(world).X, Is.EqualTo(3f).Within(1e-3f), "the slope does not slow you");
            Assert.That(Where(world).Y, Is.EqualTo(HillHeight(3f)).Within(1e-3f));
        }

        [Test]
        public void ACliffStopsYou()
        {
            var world = World(new Vec3(0f, 0f, 0f));

            Step(world, new PlayerCommand(1f, 0f), 120);

            Assert.That(Where(world).X, Is.LessThan(5f).And.GreaterThan(4.85f), "right up against it");
            Assert.That(Where(world).Y, Is.EqualTo(HillHeight(Where(world).X)).Within(1e-3f));
        }

        [Test]
        public void WalkingIntoACliffSlidesAlongIt()
        {
            var world = World(new Vec3(0f, 0f, 0f));

            Step(world, new PlayerCommand(1f, 1f), 120); // north-east, into the cliff

            Assert.That(Where(world).X, Is.LessThan(5f), "not through it");
            Assert.That(Where(world).Z, Is.GreaterThan(6f), "but on along its edge rather than stuck where it was met");
        }

        [Test]
        public void TheSeaStopsYou()
        {
            var world = World(new Vec3(0f, 0f, 0f));

            Step(world, new PlayerCommand(0f, -1f), 180);

            Assert.That(Where(world).Z, Is.GreaterThanOrEqualTo(-7f).And.LessThan(-6.85f), "stopped at the water's edge");
        }

        [Test]
        public void TheEdgeOfTheMapStopsYou()
        {
            var world = World(new Vec3(0f, 0f, 0f));

            Step(world, new PlayerCommand(-1f, 0f), 180);

            Assert.That(Where(world).X, Is.GreaterThanOrEqualTo(-10f).And.LessThan(-9.85f));
        }

        [Test]
        public void AClimbableFaceCanBeWalkedOnto()
        {
            var world = World(new Vec3(0f, 0f, 6.5f));

            Step(world, new PlayerCommand(0f, 1f), 12); // 1 m north, onto the face at z 7–8

            Assert.That(Hill().KindAt(Where(world)), Is.EqualTo(GroundKind.Climbable));
            Assert.That(Where(world).Z, Is.EqualTo(7.5f).Within(1e-3f));
        }

        [Test]
        public void SomeonePutOnGroundTheyCannotStandOnCanWalkOff()
        {
            var world = World(new Vec3(0f, 0f, -8f)); // in the sea

            Step(world, new PlayerCommand(0f, 1f), 60);

            Assert.That(Where(world).Z, Is.EqualTo(-3f).Within(1e-3f), "not trapped");
        }

        // --- Jumping, landing and placing ---------------------------------------------------------

        [Test]
        public void AJumpRisesFromTheGroundAndLandsBackOnIt()
        {
            var world = World(new Vec3(0f, 0f, 0f));
            Step(world, PlayerCommand.None); // settle on the hill
            Assert.That(Where(world).Y, Is.EqualTo(HillHeight(0f)).Within(1e-4f));

            Step(world, new PlayerCommand(0f, 0f, jump: true));
            Step(world, PlayerCommand.None, 17); // halfway through the 36-tick jump

            Assert.That(world.GetPlayer(P).IsAirborne, Is.True);
            Assert.That(Where(world).Y - HillHeight(0f), Is.EqualTo(0.9f).Within(0.01f), "the full peak above the ground");

            Step(world, PlayerCommand.None, 30);

            Assert.That(world.GetPlayer(P).IsAirborne, Is.False);
            Assert.That(Where(world).Y, Is.EqualTo(HillHeight(0f)).Within(1e-4f));
        }

        [Test]
        public void AJumpUphillLandsHigherUp()
        {
            var world = World(new Vec3(0f, 0f, 0f));

            Step(world, new PlayerCommand(1f, 0f, jump: true));
            Step(world, new PlayerCommand(1f, 0f), 35); // the whole 36-tick jump, running east: 3 m

            Assert.That(world.GetPlayer(P).IsAirborne, Is.False);
            Assert.That(Where(world).X, Is.EqualTo(3f).Within(1e-3f));
            Assert.That(Where(world).Y, Is.EqualTo(HillHeight(3f)).Within(1e-3f), "on the ground where they came down, 0.6 m higher");
        }

        [Test]
        public void APlacedPlayerStandsOnTheGround()
        {
            var world = World(new Vec3(0f, 0f, 0f));

            world.PlacePlayer(P, new Vec3(3f, 0f, 0f));

            Assert.That(Where(world).Y, Is.EqualTo(HillHeight(3f)).Within(1e-4f));
        }

        [Test]
        public void EnemiesStandOnTheGroundToo()
        {
            var world = World(new Vec3(0f, 0f, 0f));
            var enemy = world.SpawnEnemy(new EnemyDefinition { Name = "Stone", MaxHealth = 10, MoveSpeed = 0f, AttackRange = 0f }, new Vec3(2f, 0f, 0f));

            Step(world, PlayerCommand.None);

            Assert.That(world.GetEnemy(enemy).Position.Y, Is.EqualTo(HillHeight(2f)).Within(1e-4f));
        }

        [Test]
        public void EnemiesCannotWalkThroughACliff()
        {
            var world = World(new Vec3(7f, 0f, 0f)); // beyond the cliff
            var enemy = world.SpawnEnemy(new EnemyDefinition { Name = "Hound", MaxHealth = 10, MoveSpeed = 5f, AttackRange = 0.5f, StopDistance = 0.5f }, new Vec3(3f, 0f, 0f));

            Step(world, PlayerCommand.None, 120);

            Assert.That(world.GetEnemy(enemy).Position.X, Is.LessThan(5f), "it chases, but the cliff holds it");
        }

        // --- Flat floors still work through the same seam ------------------------------------------

        [Test]
        public void WithNoGroundTheWorldIsOpenAndFlat()
        {
            var world = new SimulationWorld(new SimulationConfig { TickRate = 60, PlayerMoveSpeed = 5f });
            world.AddPlayer(P, new Vec3(0f, 0f, 0f));

            Step(world, new PlayerCommand(1f, 0f), 600);

            Assert.That(world.Ground, Is.SameAs(OpenGround.Instance));
            Assert.That(Where(world).X, Is.EqualTo(50f).Within(1e-2f));
            Assert.That(Where(world).Y, Is.EqualTo(0f));
        }

        [Test]
        public void TheHubIsFlatGroundInsideWalls()
        {
            var hub = new HubDefinition { Centre = new Vec3(0f, 0f, 0f), Width = 10f, Length = 10f };
            var world = new SimulationWorld(new SimulationConfig { TickRate = 60, PlayerMoveSpeed = 5f, Hub = hub, Ground = Hill() });
            world.AddPlayer(P, new Vec3(0f, 0f, 0f));

            Step(world, new PlayerCommand(1f, 0f), 120);

            Assert.That(world.Ground, Is.InstanceOf<FloorGround>(), "the hub's floor, not the world's ground");
            Assert.That(Where(world).X, Is.EqualTo(5f).Within(1e-4f), "stopped at its wall");
            Assert.That(Where(world).Y, Is.EqualTo(0f));
        }
    }
}
