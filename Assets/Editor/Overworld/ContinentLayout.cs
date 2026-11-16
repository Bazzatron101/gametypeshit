using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HunterSystem.Editor
{
    /// <summary>
    /// Where things are on the continent (tickets 36 and 45), traced from docs/maps/overworld-reference.webp:
    /// towns and castles, gate spots, and roads, in pixels on the 909 × 673 drawing. A first pass by the
    /// agent; names without one in ticket 36 are placeholders, and gate spots for gates not yet in the game
    /// are landmarks. Move a point here and rebuild the continent to change the map.
    /// </summary>
    public static class ContinentLayout
    {
        public const float DrawingWidth = 909f;
        public const float DrawingHeight = 673f;

        public readonly struct Place
        {
            public readonly string Name;
            public readonly Vector2 Pixel;
            public readonly bool Castle;
            public Place(string name, float x, float y, bool castle = false) { Name = name; Pixel = new Vector2(x, y); Castle = castle; }
        }

        public readonly struct GateSpot
        {
            public readonly string Name;
            public readonly Vector2 Pixel;
            public readonly string Region;
            public readonly bool Known;
            /// <summary>The gate asset's id when the gate is in the game; null for a spot only.</summary>
            public readonly string GateId;
            public GateSpot(string name, float x, float y, string region, bool known, string gateId = null)
            {
                Name = name; Pixel = new Vector2(x, y); Region = region; Known = known; GateId = gateId;
            }
        }

        public readonly struct Road
        {
            public readonly string Name;
            public readonly Vector2[] Pixels;
            public Road(string name, params float[] xy)
            {
                Name = name;
                Pixels = Enumerable.Range(0, xy.Length / 2).Select(i => new Vector2(xy[2 * i], xy[2 * i + 1])).ToArray();
            }
        }

        /// <summary>The guild town: the hub, on the west island.</summary>
        public static readonly Place GuildTown = new Place("Guild town", 95f, 270f);

        public static readonly Place[] Settlements =
        {
            new Place("North-west castle (the best guild's seat)", 150f, 116f, castle: true),
            new Place("Westkeep", 270f, 357f, castle: true),
            new Place("Stiltmoor", 210f, 470f),
            new Place("Quarrygate", 495f, 215f, castle: true),
            new Place("Saltreach", 805f, 195f, castle: true),
            new Place("Ember crossing", 730f, 385f, castle: true),
            new Place("Southport", 525f, 490f),
            new Place("Farwatch", 760f, 505f, castle: true),
            new Place("Island port", 672f, 560f),
            new Place("West island village", 175f, 288f),
            new Place("Bay town", 247f, 232f),
            new Place("Greenmarch village", 302f, 292f),
            new Place("North road village", 385f, 115f),
            new Place("North-east town", 700f, 72f, castle: true),
            new Place("Midland town", 432f, 333f),
            new Place("Snowfoot village", 445f, 475f),
            new Place("Peninsula castle", 375f, 560f, castle: true),
        };

        public static readonly GateSpot[] Gates =
        {
            // Greenmarch
            new GateSpot("Mill cellar", 80f, 305f, "greenmarch", known: true, gateId: "goblin-warren"),
            new GateSpot("Barrow", 230f, 85f, "greenmarch", known: true),
            new GateSpot("Bridge gate", 108f, 222f, "greenmarch", known: false),
            new GateSpot("Thornwood hollow", 368f, 76f, "greenmarch", known: false, gateId: "thornwood-hollow"),
            // Fenward mire
            new GateSpot("Drowned chapel", 137f, 557f, "fenward", known: true),
            new GateSpot("Causeway gate", 270f, 417f, "fenward", known: false),
            // Brokenstone hills
            new GateSpot("Great quarry", 410f, 255f, "brokenstone", known: true),
            new GateSpot("Deep mine", 370f, 180f, "brokenstone", known: true),
            new GateSpot("Watchtower", 485f, 200f, "brokenstone", known: false),
            new GateSpot("Cairns", 365f, 445f, "brokenstone", known: false),
            // Ashen steppe
            new GateSpot("Stone ring", 700f, 230f, "ashen", known: true),
            new GateSpot("Battlefield", 575f, 210f, "ashen", known: true),
            // Saltreach coast
            new GateSpot("Lighthouse", 800f, 92f, "saltreach", known: true),
            // Riftlands
            new GateSpot("Snow pass", 555f, 395f, "riftlands", known: true),
            new GateSpot("South gate", 485f, 525f, "riftlands", known: false),
            new GateSpot("The Rift", 775f, 490f, "riftlands", known: true),
        };

        public static readonly Road[] Roads =
        {
            new Road("West island road", 95, 270, 130, 285, 170, 287, 178, 272, 160, 257),
            new Road("West bridge", 160, 257, 112, 230),
            new Road("North-west road", 112, 230, 105, 207, 113, 184, 138, 160, 150, 125),
            new Road("North road", 150, 125, 195, 126, 250, 132, 300, 128, 345, 127, 400, 128, 445, 133, 505, 113, 560, 113, 600, 118, 635, 125, 675, 108, 700, 78),
            new Road("Greenmarch road", 250, 132, 245, 160, 262, 190, 247, 232, 255, 258, 285, 282, 302, 292, 298, 325, 285, 350, 270, 357),
            new Road("Main road", 270, 357, 320, 368, 370, 372, 405, 350, 432, 335, 465, 322, 485, 300, 510, 285, 532, 270, 548, 245, 575, 240, 605, 258, 640, 255, 690, 272, 725, 268, 760, 245, 790, 225, 805, 198),
            new Road("Quarrygate road", 445, 133, 470, 170, 495, 215),
            new Road("Mire road", 270, 357, 262, 385, 248, 415, 230, 442, 210, 470, 170, 470, 150, 463),
            new Road("Snowfoot road", 465, 322, 450, 350, 460, 400, 472, 425, 470, 450, 445, 475, 480, 505, 522, 490),
            new Road("Peninsula road", 522, 490, 515, 520, 520, 550, 500, 563, 440, 562, 375, 560, 345, 567),
            new Road("South bridge", 345, 567, 305, 552),
            new Road("South island road", 305, 552, 295, 600),
            new Road("East coast road", 725, 268, 735, 300, 760, 330, 790, 350),
            new Road("Ember road", 760, 330, 730, 385),
            new Road("Island road", 672, 560, 700, 540, 730, 520, 760, 505),
        };

        /// <summary>The way a crossing is measured: the guild town to Saltreach on the east coast.</summary>
        public static readonly string[] CrossingRoads = { "West island road", "West bridge", "North-west road", "North road", "Greenmarch road", "Main road" };

        /// <summary>World X/Z for a pixel on the drawing, for an area from <paramref name="areaMin"/> spanning <paramref name="areaSize"/> (north is up).</summary>
        public static Vector2 World(Vector2 pixel, Vector2 areaMin, Vector2 areaSize) =>
            new Vector2(areaMin.x + pixel.x / DrawingWidth * areaSize.x, areaMin.y + (1f - pixel.y / DrawingHeight) * areaSize.y);

        /// <summary>The crossing's way through the roads, in world X/Z, in order.</summary>
        public static List<Vector2> CrossingPath(Vector2 areaMin, Vector2 areaSize)
        {
            var path = new List<Vector2>();
            foreach (var name in CrossingRoads)
            {
                var road = Roads.First(r => r.Name == name);
                var points = road.Pixels.Select(p => World(p, areaMin, areaSize)).ToList();
                if (name == "Greenmarch road")
                {
                    // The north road is followed only as far as the Greenmarch road's start.
                    var join = points[0];
                    var cut = path.FindIndex(p => Vector2.Distance(p, join) < 1f);
                    if (cut >= 0) path.RemoveRange(cut + 1, path.Count - cut - 1);
                    points.RemoveAt(0);
                }
                else if (path.Count > 0 && Vector2.Distance(path[path.Count - 1], points[0]) < 1f) points.RemoveAt(0);
                path.AddRange(points);
            }
            return path;
        }
    }
}
