using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Bridges over water (developer playtest, 2026-11-17): the river flows on under a bridge, and the bridge is
    /// a deck that can be walked above it. Off the deck's side is the river.
    /// </summary>
    public class BridgeDeckTest
    {
        private static readonly PlayerId P = new PlayerId(1);

        /// <summary>A 100 m square tile, flat at 5 m, with a river 10 m wide running north-south through x = 45 to 55.</summary>
        private static TiledGround RiverWithBridge()
        {
            const int cells = 50; // 2 m cells
            var heights = Enumerable.Repeat(5f, (cells + 1) * (cells + 1)).ToArray();
            var kinds = new GroundKind[cells * cells];
            for (var z = 0; z < cells; z++)
            for (var x = 0; x < cells; x++)
                kinds[z * cells + x] = x * 2 >= 45 && x * 2 < 55 ? GroundKind.Water : GroundKind.Walkable;
            var ground = new TiledGround(0f, 0f, 100f, 1, 1)
            {
                Bridges = new[] { new BridgeDeck(new[] { new Vec3(30f, 5f, 50f), new Vec3(42f, 8f, 50f), new Vec3(58f, 8f, 50f), new Vec3(70f, 5f, 50f) }, 3f) },
            };
            ground.SetTile(new TileCoord(0, 0), new HeightmapGround(0f, 0f, 2f, cells, cells, heights, kinds));
            return ground;
        }

        [Test]
        public void TheDeckIsWalkedAboveTheRiverAndTheRiverFlowsBesideIt()
        {
            var ground = RiverWithBridge();

            Assert.That(ground.KindAt(new Vec3(50f, 0f, 50f)), Is.EqualTo(GroundKind.Walkable), "on the deck, over the river");
            Assert.That(ground.HeightAt(new Vec3(50f, 0f, 50f)), Is.EqualTo(8f).Within(1e-3f), "at the deck's height");
            Assert.That(ground.KindAt(new Vec3(50f, 0f, 40f)), Is.EqualTo(GroundKind.Water), "beside the bridge, the river");
            Assert.That(ground.HeightAt(new Vec3(50f, 0f, 40f)), Is.EqualTo(5f).Within(1e-3f), "the river's own height");
            Assert.That(ground.HeightAt(new Vec3(36f, 0f, 50f)), Is.EqualTo(6.5f).Within(1e-3f), "half way up the ramp");
            Assert.That(ground.HeightAt(new Vec3(20f, 0f, 50f)), Is.EqualTo(5f).Within(1e-3f), "off the bridge, the ground");
        }

        [Test]
        public void AHunterCrossesTheRiverByTheBridgeButNotBesideIt()
        {
            var config = new SimulationConfig
            {
                TickRate = 60, PlayerMoveSpeed = 5f, Ground = RiverWithBridge(),
                Hub = new HubDefinition { Centre = new Vec3(10f, 0f, 10f) }, Overworld = new OverworldDefinition(),
            };

            var crossing = new SimulationWorld(config);
            crossing.AddPlayer(P, new Vec3(25f, 0f, 50f));
            var east = new Dictionary<PlayerId, PlayerCommand> { [P] = new PlayerCommand(1f, 0f) };
            for (var i = 0; i < 60 * 10; i++) crossing.Step(east);
            Assert.That(crossing.GetPlayer(P).Position.X, Is.GreaterThan(70f), "over the bridge to the far bank");

            var wading = new SimulationWorld(config);
            wading.AddPlayer(P, new Vec3(25f, 0f, 30f));
            for (var i = 0; i < 60 * 10; i++) wading.Step(east);
            Assert.That(wading.GetPlayer(P).Position.X, Is.LessThan(46f), "the river stops a hunter away from the bridge");
        }
    }
}
