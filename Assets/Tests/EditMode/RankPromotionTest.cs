using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Rank promotions (ticket 25). Ranks are guild licences tied to level bands, which are configuration. When the
    /// host's character reaches the next band, the host's world is promoted: the rank's regions open, its gates are
    /// licensed, and its promotion scene plays once the party is home. Guests' levels never promote the host's
    /// world, but a character's own rank is its level's band, and the D-rank brings the first mount.
    /// </summary>
    public class RankPromotionTest
    {
        private static readonly PlayerId Host = new PlayerId(1);
        private static readonly PlayerId Guest = new PlayerId(2);

        private static readonly Vec3 Town = new Vec3(-80f, 0f, 0f);

        private static GateDefinition OneRoomGate(string id, string rank = null) => new GateDefinition
        {
            Id = id, Name = id, Rank = rank,
            Rooms = { new RoomDefinition { Width = 10f, Length = 10f } },
        };

        /// <summary>Small bands so the tests show they come from config: D at 3, C at 5, B at 7.</summary>
        private static RankDefinition[] Ranks() => new[]
        {
            new RankDefinition { Id = "E", FirstLevel = 1 },
            new RankDefinition { Id = "D", FirstLevel = 3, OpensRegions = new[] { "marsh" }, GrantsMount = true },
            new RankDefinition { Id = "C", FirstLevel = 5 },
            new RankDefinition { Id = "B", FirstLevel = 7, OpensRegions = new[] { "steppe" } },
        };

        private static SimulationConfig Config()
        {
            var overworld = new OverworldDefinition { EntranceRadius = 3f, FindRadius = 1f };
            overworld.StartingRegions.Add("green");
            overworld.Gates.Add(new GateSite { GateId = "mill", Entrance = new Vec3(-60f, 0f, 0f), Region = "green", KnownFromStart = true });
            overworld.Gates.Add(new GateSite { GateId = "bog", Entrance = new Vec3(0f, 0f, 60f), Region = "marsh", KnownFromStart = true });
            overworld.Gates.Add(new GateSite { GateId = "old-mill", Entrance = new Vec3(-60f, 0f, 20f), Region = "green", KnownFromStart = true });
            return new SimulationConfig
            {
                TickRate = 60,
                Progression = new ProgressionDefinition { Ranks = Ranks() },
                Gates = new[] { OneRoomGate("mill", "E"), OneRoomGate("bog", "D"), OneRoomGate("old-mill", "C") },
                Hub = new HubDefinition { Centre = Town, Width = 20f, Length = 16f, BoardOffset = new Vec3(0f, 0f, 6f), ReturnCountdownSeconds = 2f },
                Overworld = overworld,
                StoryScenes = new[]
                {
                    new StorySceneDefinition { Id = "promotion-d", Trigger = StoryTrigger.RankPromotion, RankId = "D" },
                    new StorySceneDefinition { Id = "promotion-c", Trigger = StoryTrigger.RankPromotion, RankId = "C" },
                },
            };
        }

        /// <summary>A hosted world: the host's own player owns it, as <c>HostSession.AddLocalPlayer</c> sets.</summary>
        private static SimulationWorld HostedWorld(int players = 1)
        {
            var world = new SimulationWorld(Config());
            for (var i = 1; i <= players; i++) world.AddPlayer(new PlayerId(i), world.HubJoinPosition(new PlayerId(i)));
            world.SetWorldOwner(Host);
            return world;
        }

        private static List<SimulationEvent> Step(SimulationWorld world, int ticks = 1)
        {
            var all = new List<SimulationEvent>();
            for (var i = 0; i < ticks; i++)
            {
                world.Step(new Dictionary<PlayerId, PlayerCommand>());
                all.AddRange(world.Events);
            }
            return all;
        }

        private static IEnumerable<string> Scenes(IEnumerable<SimulationEvent> events) => events.OfType<StorySceneStarted>().Select(s => s.SceneId);

        // --- Bands are configuration -------------------------------------------------------------------

        [Test]
        public void ACharactersRankIsTheBandItsLevelIsIn()
        {
            var world = HostedWorld();
            Step(world);
            Assert.That(world.RankOf(Host), Is.EqualTo("E"), "a new hunter");

            world.RaiseToLevel(Host, 4);
            Step(world);
            Assert.That(world.RankOf(Host), Is.EqualTo("D"), "level 4 is in D's band, which starts at 3 in this config");

            world.RaiseToLevel(Host, 9);
            Step(world);
            Assert.That(world.RankOf(Host), Is.EqualTo("B"), "past the last band's start");
        }

        [Test]
        public void TheSpecsProvisionalBandsAreTheDefault()
        {
            var progression = new ProgressionDefinition();

            Assert.That(progression.Ranks.Select(r => (r.Id, r.FirstLevel)),
                Is.EqualTo(new[] { ("E", 1), ("D", 16), ("C", 36), ("B", 56), ("A", 76), ("S", 96) }));
            Assert.That(progression.Ranks.Single(r => r.Id == "D").OpensRegions, Is.EqualTo(new[] { "fenward" }), "ticket 36: D opens Fenward mire");
            Assert.That(progression.Ranks.Single(r => r.Id == "B").OpensRegions, Is.EqualTo(new[] { "ashen" }), "ticket 36: B opens the Ashen steppe");
            Assert.That(progression.RankIndexAt(15), Is.EqualTo(0));
            Assert.That(progression.RankIndexAt(16), Is.EqualTo(1));
        }

        // --- The promotion and its scene ----------------------------------------------------------------

        [Test]
        public void CrossingABandPromotesTheWorldAndPlaysItsSceneOnce()
        {
            var world = HostedWorld();
            Step(world);
            Assert.That(world.WorldRank, Is.EqualTo("E"));

            world.RaiseToLevel(Host, 3);
            var events = Step(world, 3);
            world.RaiseToLevel(Host, 4); // more levels in the same band: no second promotion
            events.AddRange(Step(world, 3));

            Assert.That(world.WorldRank, Is.EqualTo("D"));
            Assert.That(events.OfType<RankPromoted>().Select(e => (e.Rank, e.Player)), Is.EqualTo(new[] { ("D", Host) }));
            Assert.That(Scenes(events), Is.EqualTo(new[] { "promotion-d" }));
            Assert.That(world.SeenStoryScenes, Has.Member("promotion-d"), "the host's world remembers it");
        }

        [Test]
        public void APromotionInAGateIsToldOnceThePartyIsHome()
        {
            var world = HostedWorld();
            Step(world);
            world.StartGate("mill");
            Step(world);

            world.RaiseToLevel(Host, 3);
            var inGate = Step(world, 5);
            Assert.That(inGate.OfType<RankPromoted>().Count(), Is.EqualTo(1), "promoted at once");
            Assert.That(world.IsRegionOpen("marsh"), Is.True, "and the region opens at once");
            Assert.That(Scenes(inGate), Is.Empty, "but the scene waits");

            world.LeaveGate();
            var home = Step(world, 2);
            Assert.That(Scenes(home), Is.EqualTo(new[] { "promotion-d" }));
        }

        [Test]
        public void AHostPastSeveralBandsIsPromotedOneRankAtATimeAndSeesEachScene()
        {
            // A host bringing a level-6 character to a new world (or an old world saved before ranks).
            var world = HostedWorld();
            world.RaiseToLevel(Host, 6);
            var events = Step(world, 3);

            Assert.That(events.OfType<RankPromoted>().Select(e => e.Rank), Is.EqualTo(new[] { "D", "C" }));
            Assert.That(Scenes(events), Is.EqualTo(new[] { "promotion-d", "promotion-c" }), "in order, one after another");
            Assert.That(world.WorldRank, Is.EqualTo("C"));
        }

        [Test]
        public void AWorldLoadedAtItsRankIsNotPromotedAgain()
        {
            var world = new SimulationWorld(Config());
            world.SetWorldRank("D");
            world.SetOpenRegions(new[] { "marsh" });
            world.SetSeenStoryScenes(new[] { "promotion-d" });
            world.AddPlayer(Host, world.HubJoinPosition(Host));
            world.SetWorldOwner(Host);
            world.RaiseToLevel(Host, 4);

            var events = Step(world, 3);

            Assert.That(events.OfType<RankPromoted>(), Is.Empty);
            Assert.That(Scenes(events), Is.Empty);
            Assert.That(world.WorldRank, Is.EqualTo("D"));
        }

        // --- The map opens ------------------------------------------------------------------------------

        [Test]
        public void APromotionOpensItsRegionsAndTheirGatesComeOnTheBoard()
        {
            var world = HostedWorld();
            Step(world);
            Assert.That(world.IsOnBoard("mill"), Is.True, "an E-rank gate in the starting region");
            Assert.That(world.IsOnBoard("bog"), Is.False, "the marsh is not open to an E-rank world");

            world.RaiseToLevel(Host, 3);
            Step(world);

            Assert.That(world.OpenRegions, Is.EqualTo(new[] { "marsh" }));
            Assert.That(world.IsOnBoard("bog"), Is.True, "the D promotion opens the marsh");
            Assert.That(world.IsGateLicensed("bog"), Is.True, "and the guild gives full XP there");
        }

        [Test]
        public void AHigherRankGateInAKnownRegionIsLicensedOnlyAtItsRank()
        {
            // Like A-rank, which opens no region: its gates open in regions the party already knows.
            var world = HostedWorld();
            world.RaiseToLevel(Host, 3);
            Step(world);
            Assert.That(world.IsOnBoard("old-mill"), Is.False, "a C-rank gate, at D");
            Assert.That(world.TryStartGateFromHub("old-mill", out _), Is.False);
            Assert.That(world.IsRegionOpen("green"), Is.True, "its region is open; the rank is what is missing");

            world.RaiseToLevel(Host, 5);
            Step(world);
            Assert.That(world.IsOnBoard("old-mill"), Is.True, "at C");
        }

        // --- Co-op: the map and promotion are the host's world's ----------------------------------------

        [Test]
        public void AGuestsLevelNeverPromotesTheHostsWorld()
        {
            var world = HostedWorld(players: 2);
            Step(world);

            world.RaiseToLevel(Guest, 6);
            var events = Step(world, 3);

            Assert.That(world.RankOf(Guest), Is.EqualTo("C"), "the guest's own licence is theirs");
            Assert.That(world.WorldRank, Is.EqualTo("E"), "but the host's world stays at the host's rank");
            Assert.That(events.OfType<RankPromoted>(), Is.Empty);
            Assert.That(Scenes(events), Is.Empty);
            Assert.That(world.IsOnBoard("bog"), Is.False, "and its map does not open");
        }

        [Test]
        public void AWorldWithoutAHostIsNeverPromoted()
        {
            // The test arena and other offline scenes have no host's world to promote.
            var world = new SimulationWorld(Config());
            world.AddPlayer(Host, world.HubJoinPosition(Host));
            world.RaiseToLevel(Host, 6);

            var events = Step(world, 3);

            Assert.That(events.OfType<RankPromoted>(), Is.Empty);
            Assert.That(world.WorldRank, Is.EqualTo("E"));
        }

        [Test]
        public void SnapshotsCarryTheWorldsRankAndOwnerToGuests()
        {
            var world = HostedWorld(players: 2);
            world.RaiseToLevel(Host, 3);
            Step(world, 2);

            var guest = SimulationWorld.FromSnapshot(Config(), world.CaptureSnapshot());

            Assert.That(guest.WorldRank, Is.EqualTo("D"));
            Assert.That(guest.WorldOwner, Is.EqualTo(Host));
            Assert.That(guest.IsOnBoard("bog"), Is.True, "the guest's board shows the host's map");
            Assert.That(guest.SeenStoryScenes, Has.Member("promotion-d"), "so a guest's prediction never plays it again");

            var events = Step(guest, 3);
            Assert.That(events.OfType<RankPromoted>(), Is.Empty, "a guest's prediction does not promote again");
        }

        // --- The first mount ----------------------------------------------------------------------------

        [Test]
        public void EveryCharacterReachingDRankGetsTheirFirstMount()
        {
            var world = HostedWorld(players: 2);
            Step(world);
            Assert.That(world.GetPlayer(Host).HasMount || world.GetPlayer(Guest).HasMount, Is.False, "E-rank hunters walk");

            world.RaiseToLevel(Guest, 3);
            var events = Step(world, 2);

            Assert.That(world.GetPlayer(Guest).HasMount, Is.True, "a guest's own D-rank brings their mount (ADR 0015)");
            Assert.That(world.GetPlayer(Host).HasMount, Is.False, "the E-rank host still walks");
            Assert.That(events.OfType<MountGranted>().Select(e => e.Player), Is.EqualTo(new[] { Guest }));
        }
    }
}
