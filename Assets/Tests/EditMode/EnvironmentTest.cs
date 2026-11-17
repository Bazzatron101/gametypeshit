using System.Collections.Generic;
using System.Linq;
using HunterSystem.Editor;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Dressing the overworld (ticket 49). Nature is scattered on each tile as it loads: the same every time, with
    /// no seams, only on open grass, and clear of roads, towns and gates. The bake lays out towns' buildings off
    /// the roads and the square, never overlapping, and blocks their footprints so nobody walks through a wall.
    /// The placeholder art is labelled AI.
    /// </summary>
    public class EnvironmentTest
    {
        private const string PatchPath = "Assets/Data/Overworld/TestPatch.asset";

        /// <summary>Flat open grass at 10 m, 200 × 200 m from the origin, in 2 m cells.</summary>
        private static HeightmapGround Flat(float height = 10f, GroundKind[] kinds = null) =>
            new HeightmapGround(0f, 0f, 2f, 100, 100, Enumerable.Repeat(height, 101 * 101).ToArray(), kinds);

        private static EnvironmentScatter.World World(params EnvironmentScatter.Rules[] kinds) => new EnvironmentScatter.World
        {
            Seed = 7, Kinds = kinds, SeaLevel = 4f, SandAboveSea = 1f,
        };

        /// <summary>Everywhere, densely: what the rules allow is all that stops it.</summary>
        private static EnvironmentScatter.Rules Everywhere(float clearance = 0f) =>
            new EnvironmentScatter.Rules(400f, 1f, 50f, 0, 1f, 1f, 30f, clearance, 0f, 1);

        private static IEnumerable<Vector3> Positions(Matrix4x4[] list) => list.Select(m => new Vector3(m.m03, m.m13, m.m23));

        // --- Scatter ---------------------------------------------------------------------------------------

        [Test]
        public void ATileGrowsTheSameEveryTime()
        {
            var world = World(new EnvironmentScatter.Rules(60f, 0.5f, 40f, 1, 0.8f, 1.2f, 30f, 1f, 0.2f, 3));

            var first = EnvironmentScatter.Scatter(Flat(), 0f, 0f, 100f, world);
            var again = EnvironmentScatter.Scatter(Flat(), 0f, 0f, 100f, world);

            Assert.That(first[0].Length, Is.GreaterThan(0));
            Assert.That(again[0], Is.EqualTo(first[0]));
        }

        [Test]
        public void NeighbouringTilesMeetWithoutGapsOrDoubles()
        {
            var world = World(Everywhere());
            var ground = Flat();

            var west = Positions(EnvironmentScatter.Scatter(ground, 0f, 0f, 100f, world)[0]).ToList();
            var east = Positions(EnvironmentScatter.Scatter(ground, 100f, 0f, 100f, world)[0]).ToList();
            var both = Positions(EnvironmentScatter.Scatter(ground, 0f, 0f, 200f, world)[0]).Where(p => p.z < 100f).ToList(); // the same strip, in one go

            Assert.That(west.All(p => p.x >= 0f && p.x < 100f), "each piece on its own tile");
            Assert.That(east.All(p => p.x >= 100f && p.x < 200f));
            Assert.That(west.Count + east.Count, Is.EqualTo(both.Count), "two halves are the whole: nothing lost or doubled at the seam");
        }

        [Test]
        public void NatureStandsOnTheGround()
        {
            var pieces = EnvironmentScatter.Scatter(Flat(12f), 0f, 0f, 100f, World(Everywhere()))[0];

            Assert.That(pieces.All(m => Mathf.Approximately(m.m13, 12f)));
        }

        [Test]
        public void NothingGrowsOnWaterBlockedGroundOrTheBeach()
        {
            var kinds = new GroundKind[100 * 100];
            for (var r = 0; r < 100; r++)
            for (var c = 0; c < 100; c++)
                kinds[r * 100 + c] = c < 25 ? GroundKind.Water : c < 50 ? GroundKind.Blocked : GroundKind.Walkable;
            var ground = new HeightmapGround(0f, 0f, 2f, 100, 100, Enumerable.Range(0, 101 * 101).Select(i => i % 101 < 75 ? 10f : 4.5f).ToArray(), kinds);

            var pieces = Positions(EnvironmentScatter.Scatter(ground, 0f, 0f, 200f, World(Everywhere()))[0]).ToList();

            Assert.That(pieces, Is.Not.Empty);
            Assert.That(pieces.All(p => p.x >= 100f), "not on water (x < 50) or blocked ground (x < 100)");
            Assert.That(pieces.All(p => p.y >= 5f), "not on the beach, just above the sea");
        }

        [Test]
        public void NothingGrowsOnRoadsOrInClearAreas()
        {
            var world = World(Everywhere(clearance: 2f));
            world.DistanceToRoad = (x, z) => Mathf.Abs(z - 100f) - 3f; // a road along z = 100, 6 m wide
            world.Clear = new[] { new EnvironmentScatter.ClearArea(50f, 50f, 20f) };

            var pieces = Positions(EnvironmentScatter.Scatter(Flat(), 0f, 0f, 200f, world)[0]).ToList();

            Assert.That(pieces.All(p => Mathf.Abs(p.z - 100f) >= 5f), "off the road and its clearance");
            Assert.That(pieces.All(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(50f, 50f)) >= 22f), "out of the clear area");
        }

        [Test]
        public void NothingGrowsOnGroundSteeperThanItsKindAllows()
        {
            // A slope of 1 in 1 (45 degrees) everywhere.
            var heights = new float[101 * 101];
            for (var r = 0; r <= 100; r++)
            for (var c = 0; c <= 100; c++)
                heights[r * 101 + c] = 10f + c * 2f;
            var steep = new HeightmapGround(0f, 0f, 2f, 100, 100, heights);

            Assert.That(EnvironmentScatter.Scatter(steep, 10f, 10f, 180f, World(Everywhere()))[0], Is.Empty, "30 degrees at most"); // inside the grid's edges
        }

        [Test]
        public void MountainBouldersGrowOnlyNearSteepGround()
        {
            // Flat ground for the west half; a 45 degree mountainside rising from x = 100 m.
            var heights = new float[101 * 101];
            for (var r = 0; r <= 100; r++)
            for (var c = 0; c <= 100; c++)
                heights[r * 101 + c] = c * 2f <= 100f ? 10f : 10f + (c * 2f - 100f);
            var ground = new HeightmapGround(0f, 0f, 2f, 100, 100, heights);
            var boulder = new EnvironmentScatter.Rules(400f, 1f, 50f, 0, 1f, 1f, 30f, 0f, 0f, 1, nearSteepWithin: 30f, steepDegrees: 28f);

            var pieces = Positions(EnvironmentScatter.Scatter(ground, 0f, 0f, 200f, World(boulder))[0]).ToList();

            Assert.That(pieces, Is.Not.Empty, "at the mountain's foot");
            Assert.That(pieces.All(p => p.x >= 100f - 30f - 2f), Is.True, "only within 30 m of the mountainside");
        }

        [Test]
        public void TheTestPatchHasAnOldForestAndBouldersUnderItsMountains()
        {
            var environment = UnityEditor.AssetDatabase.LoadAssetAtPath<EnvironmentAsset>("Assets/Data/Overworld/Environment.asset");
            var patch = UnityEditor.AssetDatabase.LoadAssetAtPath<OverworldAsset>(PatchPath);
            Assume.That(environment != null && patch != null && patch.IsBaked);
            var oldPine = environment.props.FirstOrDefault(k => k.id == "old-pine");
            var boulders = environment.props.FirstOrDefault(k => k.id == "mountain-boulder");
            Assert.That(oldPine, Is.Not.Null, "old pines");
            Assert.That(oldPine.mesh.bounds.size.y * oldPine.minScale, Is.GreaterThan(15f), "old pines stand over 15 m");
            Assert.That(boulders, Is.Not.Null, "mountain boulders");
            Assert.That(boulders.nearSteepWithin, Is.GreaterThan(0f), "only under the mountains");
            Assert.That(boulders.mesh.bounds.size.y * boulders.minScale, Is.GreaterThan(3f), "large");

            var ground = patch.LoadGround();
            var world = OverworldScene.ScatterWorld(environment, patch);
            var index = System.Array.FindIndex(environment.props, k => k.id == "mountain-boulder");
            var pines = System.Array.FindIndex(environment.props, k => k.id == "old-pine");
            var placed = Enumerable.Range(0, patch.TilesX * patch.TilesZ).Select(t =>
                EnvironmentScatter.Scatter(ground, patch.Origin.x + (t % patch.TilesX) * patch.TileSize, patch.Origin.y + (t / patch.TilesX) * patch.TileSize, patch.TileSize, world)).ToList();
            Assert.That(placed.Sum(p => p[index].Length), Is.GreaterThan(5), "boulders stand under the ridge and the hill");
            Assert.That(placed.Sum(p => p[pines].Length), Is.GreaterThan(100), "old pines fill the woods");
            foreach (var colour in new[] { "flowers-yellow", "flowers-pink", "flowers-blue", "flowers-white", "red-mushroom", "brown-mushroom" })
            {
                var kind = System.Array.FindIndex(environment.props, k => k.id == colour);
                Assert.That(kind, Is.GreaterThanOrEqualTo(0), colour);
                Assert.That(placed.Sum(p => p[kind].Length), Is.GreaterThan(50), $"{colour} give the ground colour");
            }
        }

        [Test]
        public void CoverIsRoughlyTheShareOfGroundAKindGrowsOn()
        {
            foreach (var cover in new[] { 0.25f, 0.5f, 0.75f })
            {
                var inside = 0;
                const int samples = 40000;
                for (var i = 0; i < samples; i++)
                    if (EnvironmentScatter.Patch(3, 0, (i % 200) * 0.37f, (i / 200) * 0.37f) <= cover) inside++;
                Assert.That(inside / (float)samples, Is.EqualTo(cover).Within(0.15f), $"cover {cover}");
            }
        }

        // --- Towns ---------------------------------------------------------------------------------------------

        private static readonly TownPlanner.Kind[] Kinds =
        {
            new TownPlanner.Kind("hall", "hall", new Vector2(12f, 9f)),
            new TownPlanner.Kind("inn", "inn", new Vector2(9f, 7f)),
            new TownPlanner.Kind("shop", "shop", new Vector2(6f, 6f)),
            new TownPlanner.Kind("well", "well", new Vector2(2.4f, 2.4f)),
            new TownPlanner.Kind("house", "house", new Vector2(6f, 5f)),
            new TownPlanner.Kind("cottage", "house", new Vector2(5f, 4.5f)),
            new TownPlanner.Kind("tower", "tower", new Vector2(5.5f, 5.5f)),
            new TownPlanner.Kind("keep", "keep", new Vector2(11f, 11f)),
        };

        private static TownPlanner.Site Town(TownStyle style = TownStyle.GuildTown) => new TownPlanner.Site
        {
            Centre = new Vector2(100f, 100f), Radius = 45f, Style = style, SquareRadius = 12f,
            KeepClear = { (new Vector2(100f, 107f), 4f) }, HeightAt = (x, z) => 10f,
        };

        private static bool Overlap(TownPlanner.Placement a, TownPlanner.Placement b) =>
            TownPlanner.Corners(a, 0f).Any(c => TownPlanner.Contains(b.Centre, b.Yaw, b.Footprint, c.x, c.y, -0.01f)) ||
            TownPlanner.Corners(b, 0f).Any(c => TownPlanner.Contains(a.Centre, a.Yaw, a.Footprint, c.x, c.y, -0.01f)) ||
            TownPlanner.Contains(a.Centre, a.Yaw, a.Footprint, b.Centre.x, b.Centre.y);

        [Test]
        public void TheGuildTownGetsItsHallInnShopWellAndHouses()
        {
            var placed = TownPlanner.Plan(Town(), Kinds);

            Assert.That(placed.Select(p => p.Id), Does.Contain("hall").And.Contain("inn").And.Contain("shop").And.Contain("well"));
            Assert.That(placed.Count(p => p.Id == "house" || p.Id == "cottage"), Is.GreaterThanOrEqualTo(6));
            Assert.That(placed.Single(p => p.Id == "hall").Centre.y, Is.GreaterThan(110f), "the hall stands behind the board, north of the square");
        }

        [Test]
        public void BuildingsNeverOverlapAndKeepTheSquareAndTheBoardClear()
        {
            var placed = TownPlanner.Plan(Town(), Kinds);

            for (var i = 0; i < placed.Count; i++)
            for (var j = i + 1; j < placed.Count; j++)
                Assert.That(Overlap(placed[i], placed[j]), Is.False, $"{placed[i].Id} and {placed[j].Id}");
            foreach (var p in placed.Where(p => p.Id != "well"))
            {
                Assert.That(TownPlanner.Corners(p, 0f).All(c => Vector2.Distance(c, new Vector2(100f, 100f)) > 11.9f), $"{p.Id} keeps off the square");
                Assert.That(TownPlanner.Contains(p.Centre, p.Yaw, p.Footprint, 100f, 107f, 4f), Is.False, $"{p.Id} keeps off the board");
            }
            Assert.That(placed.All(p => TownPlanner.Corners(p, 0f).All(c => Vector2.Distance(c, new Vector2(100f, 100f)) <= 45.01f)), "inside the town");
        }

        [Test]
        public void DoorsFaceTheSquareOrAStreet()
        {
            var layout = TownPlanner.PlanTown(Town(), Kinds);
            foreach (var p in layout.Buildings.Where(p => p.Id != "well"))
            {
                TownPlanner.Axes(p.Yaw, out _, out var forward);
                var door = p.Centre + forward * (p.Footprint.y / 2f + 0.5f);
                var toSquare = Vector2.Dot(forward, (new Vector2(100f, 100f) - p.Centre).normalized) > 0.99f;
                var toStreet = layout.Streets.Any(st => TownPlanner.DistanceToSegment(door, st.From, st.To) <= st.HalfWidth + TownPlanner.StreetSetback + 0.6f &&
                                                        TownPlanner.DistanceToSegment(door, st.From, st.To) < TownPlanner.DistanceToSegment(p.Centre, st.From, st.To));
                Assert.That(toSquare || toStreet, Is.True, $"{p.Id} at {p.Centre}: its door faces the square or a street");
            }
        }

        // --- Streets (ticket 83) and more buildings (ticket 84) ------------------------------------------

        private static readonly TownPlanner.Kind[] MoreKinds = Kinds.Concat(new[]
        {
            new TownPlanner.Kind("market-hall", "market", new Vector2(9f, 7f)),
            new TownPlanner.Kind("chapel", "chapel", new Vector2(6f, 11f)),
            new TownPlanner.Kind("smithy", "smithy", new Vector2(7f, 6f)),
            new TownPlanner.Kind("stables", "stables", new Vector2(11f, 5f)),
            new TownPlanner.Kind("warehouse", "warehouse", new Vector2(10f, 8f)),
            new TownPlanner.Kind("workshop", "workshop", new Vector2(7f, 5f)),
            new TownPlanner.Kind("barn", "barn", new Vector2(9f, 7f)),
            new TownPlanner.Kind("two-storey-house", "house", new Vector2(6f, 6f)),
        }).ToArray();

        [Test]
        public void TheRoadsThatReachAPlaceBecomeItsMainStreetsWithLanesOffThem()
        {
            var site = Town();
            site.Radius = 60f;
            site.DistanceToRoad = (x, z) => Mathf.Abs(z - 100f) - 3f; // one road east-west through the middle
            var layout = TownPlanner.PlanTown(site, MoreKinds);

            var mains = layout.Streets.Where(s => s.Main).ToList();
            Assert.That(mains.Count, Is.EqualTo(2), "east and west, along the road");
            foreach (var main in mains)
                Assert.That(Mathf.Abs((main.To - main.From).normalized.y), Is.LessThan(0.1f), "along the road");
            Assert.That(layout.Streets.Count(s => !s.Main), Is.GreaterThan(0), "lanes branch off them");
        }

        [Test]
        public void BuildingsNeverStandOnAStreetAndVillagesAndTownsAreFuller()
        {
            var town = Town();
            town.Radius = 60f;
            var layout = TownPlanner.PlanTown(town, MoreKinds);
            foreach (var p in layout.Buildings.Where(p => p.Id != "well" && p.Id != "keep"))
            foreach (var c in TownPlanner.Corners(p, 0f).Append(p.Centre))
            foreach (var st in layout.Streets)
                Assert.That(TownPlanner.DistanceToSegment(c, st.From, st.To), Is.GreaterThanOrEqualTo(st.HalfWidth + 1f), $"{p.Id} keeps off the street");
            for (var i = 0; i < layout.Buildings.Count; i++)
            for (var j = i + 1; j < layout.Buildings.Count; j++)
                Assert.That(Overlap(layout.Buildings[i], layout.Buildings[j]), Is.False, $"{layout.Buildings[i].Id} and {layout.Buildings[j].Id}");
            Assert.That(layout.Buildings.Count, Is.InRange(30, 60), "a town");
            Assert.That(layout.Buildings.Select(b => b.Id), Does.Contain("market-hall").And.Contain("chapel").And.Contain("smithy"));

            var village = Town(TownStyle.Village);
            village.Radius = 48f;
            village.SquareRadius = 7f;
            village.KeepClear.Clear();
            var houses = TownPlanner.PlanTown(village, MoreKinds).Buildings;
            Assert.That(houses.Count, Is.InRange(10, 20), "a village");
        }

        [Test]
        public void EachRegionHasItsOwnLook()
        {
            var environment = UnityEditor.AssetDatabase.LoadAssetAtPath<EnvironmentAsset>("Assets/Data/Overworld/Environment.asset");
            var house = environment.Building("house");
            Assert.That(house, Is.Not.Null);
            var looks = new[] { "greenmarch", "brokenstone", "saltreach", "fenward", "ashen", "riftlands" }
                .Select(r => string.Join(",", environment.MaterialsFor(house, r).Select(m => m.name))).ToList();
            Assert.That(looks.Distinct().Count(), Is.EqualTo(6), "a house looks different in each region");
            foreach (var kind in environment.buildings)
            {
                Assert.That(kind.mesh, Is.Not.Null, kind.id);
                Assert.That(string.IsNullOrEmpty(kind.role), Is.False, kind.id);
            }

            var continent = UnityEditor.AssetDatabase.LoadAssetAtPath<OverworldAsset>("Assets/Data/Overworld/Continent.asset");
            Assume.That(continent != null && continent.IsBaked);
            Assert.That(continent.Streets.Count, Is.GreaterThan(continent.Roads.Count), "the places have streets of their own, apart from the roads between them");
            Assert.That(continent.Buildings.All(b => !string.IsNullOrEmpty(b.region)), "every building knows its region");
            Assert.That(continent.Buildings.Select(b => b.region).Distinct().Count(), Is.GreaterThanOrEqualTo(5), "places across the regions");
        }

        [Test]
        public void BuildingsKeepOffRoadsAndUnbuildableGround()
        {
            var site = Town();
            site.DistanceToRoad = (x, z) => Mathf.Abs(z - 100f) - 3f; // a road east-west through the middle
            site.RoadClearance = 3f;
            site.Buildable = (x, z) => x < 130f; // a lake to the east

            var placed = TownPlanner.Plan(site, Kinds);

            Assert.That(placed, Is.Not.Empty);
            foreach (var p in placed)
            foreach (var c in TownPlanner.Corners(p, 0f))
            {
                Assert.That(Mathf.Abs(c.y - 100f), Is.GreaterThanOrEqualTo(6f - 0.01f), $"{p.Id} off the road");
                Assert.That(c.x, Is.LessThan(130f), $"{p.Id} out of the lake");
            }
        }

        [Test]
        public void BuildingsKeepToFlatGround()
        {
            var site = Town();
            site.HeightAt = (x, z) => x > 115f ? 10f + (x - 115f) : 10f; // a steep rise to the east

            foreach (var p in TownPlanner.Plan(site, Kinds))
            {
                var heights = TownPlanner.Corners(p, 0f).Select(c => site.HeightAt(c.x, c.y)).ToList();
                Assert.That(heights.Max() - heights.Min(), Is.LessThanOrEqualTo(site.MaxRise), p.Id);
            }
        }

        [Test]
        public void ACastleTownHasAKeepInTheMiddleAndTowersRoundIt()
        {
            var site = Town(TownStyle.Castle);
            site.KeepClear.Clear(); // only the guild town has the board
            var placed = TownPlanner.Plan(site, Kinds);

            Assert.That(placed.Single(p => p.Id == "keep").Centre, Is.EqualTo(new Vector2(100f, 100f)));
            Assert.That(placed.Count(p => p.Id == "tower"), Is.EqualTo(4));
        }

        [Test]
        public void TheSameTownIsLaidOutTheSameEveryTime()
        {
            var first = TownPlanner.Plan(Town(), Kinds);
            var again = TownPlanner.Plan(Town(), Kinds);

            Assert.That(again.Select(p => (p.Id, p.Centre, p.Yaw)), Is.EqualTo(first.Select(p => (p.Id, p.Centre, p.Yaw))));
        }

        // --- Blocking -----------------------------------------------------------------------------------------------

        [Test]
        public void ABuildingsFootprintIsBlockedAndAPlayerCannotWalkIntoIt()
        {
            var kinds = Enumerable.Repeat(GroundKind.Walkable, 100 * 100).ToArray();
            var house = new OverworldAsset.BuildingData { kindId = "house", position = new Vector3(100f, 10f, 120f), yaw = 30f, footprint = new Vector2(6f, 5f) };
            var blocked = OverworldTools.BlockFootprints(kinds, 100, 100, 0f, 0f, 2f, new[] { house });
            var ground = Flat(10f, kinds);

            Assert.That(blocked, Is.InRange(5, 12), "about 30 m² of 4 m² cells");
            Assert.That(ground.KindAt(new Vec3(100f, 0f, 120f)), Is.EqualTo(GroundKind.Blocked));
            Assert.That(ground.KindAt(new Vec3(100f, 0f, 110f)), Is.EqualTo(GroundKind.Walkable));

            var world = new SimulationWorld(new SimulationConfig { TickRate = 60, Ground = ground });
            var p1 = new PlayerId(1);
            world.AddPlayer(p1, new Vec3(100f, 0f, 108f));
            var closest = float.MaxValue;
            for (var i = 0; i < 180; i++) // 2 m cells: the wall is where the blocked cells start, within a cell of the footprint
            {
                world.Step(new Dictionary<PlayerId, PlayerCommand> { [p1] = new PlayerCommand(0f, 1f) });
                var at = world.GetPlayer(p1).Position;
                Assert.That(TownPlanner.Contains(new Vector2(100f, 120f), 30f, new Vector2(6f, 5f), at.X, at.Z, -1.5f), Is.False, $"tick {i}: inside the house at {at}");
                closest = Mathf.Min(closest, Vector2.Distance(new Vector2(at.X, at.Z), new Vector2(100f, 120f)));
            }
            Assert.That(closest, Is.LessThan(5f), "it walked right up to the wall");
        }

        // --- The built content -----------------------------------------------------------------------------------------

        [Test]
        public void ThePlaceholderArtIsLabelledAIAndDrawsInstanced()
        {
            var environment = AssetDatabase.LoadAssetAtPath<EnvironmentAsset>(EnvironmentArtSetup.EnvironmentPath);
            Assert.That(environment, Is.Not.Null, "made by Hunter System > Overworld > Make Placeholder Environment Art");
            Assert.That(environment.props.Select(p => p.id), Does.Contain("pine").And.Contain("rock").And.Contain("grass"));
            Assert.That(environment.buildings.Select(b => b.role), Does.Contain("hall").And.Contain("house"));

            var art = environment.props.SelectMany(p => p.materials.Cast<Object>().Append(p.mesh))
                .Concat(environment.buildings.SelectMany(b => b.materials.Cast<Object>().Append(b.mesh))).Distinct().ToList();
            foreach (var asset in art)
            {
                Assert.That(asset, Is.Not.Null);
                Assert.That(AssetDatabase.GetLabels(asset), Does.Contain("AI"), asset.name);
            }
            foreach (var kind in environment.props)
            {
                Assert.That(kind.materials.Length, Is.EqualTo(kind.mesh.subMeshCount), kind.id);
                Assert.That(kind.materials.All(m => m.enableInstancing), kind.id);
            }
        }

        [Test]
        public void TheTestPatchsTownHasBuildingsThatAreBlockedAndOffTheRoad()
        {
            var patch = AssetDatabase.LoadAssetAtPath<OverworldAsset>(PatchPath);
            Assert.That(patch.Buildings.Count, Is.GreaterThanOrEqualTo(8), "rebuild the test patch scene after making the art");
            Assert.That(patch.Buildings.Select(b => b.kindId), Does.Contain("guild-hall"));

            var ground = patch.LoadGround();
            foreach (var b in patch.Buildings)
            {
                Assert.That(Vector3.Distance(b.position, patch.TownCentre), Is.LessThan(patch.TownRadius), b.kindId);
                Assert.That(ground.KindAt(new Vec3(b.position.x, 0f, b.position.z)), Is.EqualTo(GroundKind.Blocked), $"{b.kindId} blocks its ground");
                Assert.That(patch.DistanceToRoad(b.position.x, b.position.z), Is.GreaterThan(0f), $"{b.kindId} is off the road");
                Assert.That(patch.IsUnderBuilding(b.position.x, b.position.z), Is.True);
            }
            var board = patch.TownCentre + new Vector3(0f, 0f, 7f);
            Assert.That(ground.KindAt(new Vec3(board.x, 0f, board.z - 2f)), Is.EqualTo(GroundKind.Walkable), "the board can still be reached");
            Assert.That(ground.KindAt(new Vec3(patch.TownCentre.x, 0f, patch.TownCentre.z)), Is.EqualTo(GroundKind.Walkable), "the square is open");
        }

        [Test]
        public void TheTestPatchGrowsTreesRocksAndGrassButNotInTown()
        {
            var patch = AssetDatabase.LoadAssetAtPath<OverworldAsset>(PatchPath);
            var environment = AssetDatabase.LoadAssetAtPath<EnvironmentAsset>(EnvironmentArtSetup.EnvironmentPath);
            var world = OverworldScene.ScatterWorld(environment, patch);
            var counts = new int[environment.props.Length];
            var inTown = 0;
            for (var z = 0; z < patch.TilesZ; z++)
            for (var x = 0; x < patch.TilesX; x++)
            {
                var minX = patch.Origin.x + x * patch.TileSize;
                var minZ = patch.Origin.y + z * patch.TileSize;
                var props = EnvironmentScatter.Scatter(patch.LoadTile(new TileCoord(x, z)), minX, minZ, patch.TileSize, world);
                for (var k = 0; k < props.Length; k++)
                {
                    counts[k] += props[k].Length;
                    inTown += Positions(props[k]).Count(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(patch.TownCentre.x, patch.TownCentre.z)) < patch.TownRadius);
                }
            }

            for (var k = 0; k < counts.Length; k++)
                Assert.That(counts[k], Is.GreaterThan(0), environment.props[k].id);
            TestContext.WriteLine(string.Join(", ", environment.props.Select((p, k) => $"{p.id} {counts[k]}")));
            Assert.That(inTown, Is.EqualTo(0));
        }
    }
}
