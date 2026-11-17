# 39: Terrain pipeline and a 1 km test patch

**What to build:** The way overworld terrain gets made, proved on one tile-split patch of about 1 km² before the whole map is built. Most terrain is generated, with hand-placed gates, towns, landmarks and the road on top (ADR 0015, "content cost is the main risk"). The pipeline bakes the simulation's ground data (ticket 37) from the same terrain, so the art and the rules cannot drift.

**Blocked by:** 37: Give the simulation real ground

**Status:** done

- [x] Terrain is split into square tiles (size a setting; about 250 to 500 m to start) in Unity Terrain, URP flat shading per ADR 0002
- [x] Generated height from a source the developer can paint or trace from `docs/maps/overworld-reference.webp`, with hand edits kept when it is regenerated
- [x] An editor step bakes each tile's heightmap and walkability for the simulation; steepness past a configured slope is unwalkable, water is unwalkable, painted climbable faces are marked
- [x] Hand-placed points (gates, town edges, the road, landmarks) are authored as markers the simulation can read, not only as scene objects
- [x] A 1 km² test patch with a hill, a steep ridge with a pass, a climbable face, a stretch of shore, a road and two gate markers, walkable in a build
- [x] ~~Hours spent on the patch are recorded~~ (no longer needed: the continent is built from a recipe, ticket 45) (the agent's time is below; the developer's own sculpting time is still to come), to estimate the full map's cost (ticket 45)

## Notes

- Far tiles need lower-detail versions; the patch should show whether Unity Terrain's own detail settings are enough.

## Comments

**2026-11-15 (developer, recorded by agent):** **Unity Terrain to edit, a mesh to show.** Unity Terrain renders smooth, so it cannot have ADR 0002's flat-shaded look. The tiles stay the editing surface (sculpt them with Unity's brushes), and what players see is a flat-shaded low-poly mesh built from the bake.

**2026-11-15 (agent):** Built.

- **Heights** (`Assets/Scripts/Game/Overworld/HeightSource.cs`): a base height, gentle noise, an optional painted heightmap stretched over the area (to trace from the reference map), and stamps in order: hill, ridge, pass, sea (a shore), flatten (roads and the town square) and climbable faces.
- **The recipe** (`Assets/Editor/Overworld/OverworldSourceAsset.cs`): the height source, the tiles (origin, tile size, count, heightmap resolution, height range), and the bake's rules (steepest walkable slope, sea level, facet sizes near and far).
- **Generate** (Hunter System > Overworld > Generate Terrain Tiles): one Unity Terrain per tile, tagged EditorOnly so builds never carry it. Each tile remembers what was last generated (`Tile_x_z.base.bytes`). Regenerating lays whatever was sculpted since on top of the new heights, so hand edits survive (`GroundBake.KeepHandEdits`). The climbable layer is the terrain's second paint layer; stamps add to what was painted.
- **Bake** (Hunter System > Overworld > Bake): reads every tile into the simulation's ground: under the sea is water, steeper than the limit is blocked, painted faces are climbable (`GroundBake.Classify`), at about 1 m. Every `OverworldMarker` (gate, town, road, landmark) goes into the overworld asset (`OverworldAsset`) as data: gate sites, the town centre and reach, road lines and landmarks. The ground goes in `<name>.ground.bytes` beside it.
- **The shown ground** (`OverworldScene`, `FlatTerrainMesh`): built when the scene loads, from the same baked ground the simulation walks on, so the art and the rules cannot drift. Each tile is a flat-shaded mesh (every triangle its own face normal) at 4 m facets near and 16 m far, switched by a LODGroup. Each triangle gets one material (grass, rock where blocked, the climbable face, sand by the sea, the road), plus the sea and a pillar at each gate. Building it at load keeps tens of megabytes of mesh out of git.
- **The runner** applies an overworld scene's ground and markers to its config (one line in `SimulationRunner.Awake`).
- **The patch** (Hunter System > Overworld > Rebuild Test Patch Scene, `Assets/Scenes/OverworldPatch.unity`): 1 km², 4 × 4 tiles of 250 m. It has the guild town on the west with the board, a road east past the Goblin Warren (known from the start) and through a pass in a steep ridge, a walkable hill (Lookout Hill), a climbable face on the ridge, the sea and its shore to the south, and Thornwood Hollow hidden beyond the ridge. Thornwood Hollow is a new farming gate from ticket 36's Greenmarch list, made of kit rooms, now in the game content. Baked: 1024 × 1024 cells of 0.977 m, about 50,800 blocked, 147,200 water and 2,400 climbable.
- **Tests:** `TerrainPipelineTest` (17: stamps, keeping hand edits, the bake's rules, the bytes, flat-shaded meshes, the asset, the baked patch's features, walking it from the town along the road through the pass to the hidden gate, and the ridge stopping a straight walk) and `OverworldPatchSceneTest` (PlayMode: the scene's simulation is on the baked ground, 16 tiles with near and far versions are shown, the Terrain is hidden, a player on the hilltop stands on it, and walking near the hidden gate finds it).

**Found on the way:** the first patch had its pass floor at 16 m, about 7 m above the land around it, so the pass's mouth was too steep and the bake (rightly) blocked the road. The pass now sits at ground level (10 m) and is wider (30 m). The walking test is what caught it.

**Far tiles:** Unity Terrain's own distance detail does not apply, since the Terrain is not what is shown. Each shown tile has its own far mesh instead (16 m facets, never culled). Whether that is enough at the full map's size is a question for ticket 40 (streaming) and 45.

**Hours:** the agent's work on the pipeline and the patch took roughly half an hour of wall-clock time, plus batch builds and test runs. That is not a human estimate. The useful number for ticket 45 is the developer's own time to sculpt and trace one patch by hand, which has not been spent yet.

**Left for later:**
- "Walkable in a build" is checked in editor play mode and by the tests (EditMode 613 passed, PlayMode 32 passed plus the 2 patch tests after a fix to the test's wait), not in a standalone player build: no build menu makes one for the patch yet, and `CoopPlayerBuild` builds only the co-op scene.
- Offline (no host), the patch scene cannot enter gates: only a host starts one. Walking, finding and the board all work.
- The town has no buildings, only the board: town art is content work.
- The baked ground is one file for the whole area (5 MB for 1 km²). The full map (about 50 km² of land) needs it per tile, read as players move: tickets 40 and 45. The terrain data and base heights are about 12 MB for the patch, not in Git LFS. Consider LFS for `*.bytes` and terrain data before the full map.
- The shown ground has no colliders; nothing needs them, since movement is the simulation's.

**2026-11-15 (developer playtest, recorded by agent):** The open world feels big at the current movement speed, and it will need buildings and environment to look good. Scale is noted on ticket 36; the dressing is ticket 49.

**2026-11-16 (developer, recorded by agent):** Sculpting a tile and regenerating is easy. Next, the developer will make an elevation map of the continent to show the heights. The pipeline already takes one: `HeightSource.paintedHeights`, a greyscale image stretched over the whole area (white adds `paintedHeightScale` metres over black). Best made on top of `docs/maps/overworld-reference.webp`, same framing, saved as `docs/maps/overworld-heights.png`.

**2026-11-16 (agent, developer asked):** A first elevation map of the continent, drawn by the agent from the reference map (AI placeholder; "-ai" in the file names):
- `docs/maps/overworld-heights-ai.png`: 16-bit greyscale, 1818 × 1346, same framing as the reference. Black is 0 m, white 250 m. Sea level 4 m; the coast sits at about 6 m with shallows just offshore.
- `docs/maps/overworld-topo-ai.png`: the same heights as a coloured, shaded map with contours every 25 m and the ranges named, to look at.
- Made by `scripts/make-overworld-heights.py`: land from the drawing's colours; mountains are ridges drawn by hand along the drawing's mountain icons (the list `RIDGES`: name, height, width, points on the 909 × 673 drawing), with noise for rough crests. Lowlands rise from ~6 m at the coast to ~35–50 m inland. Heights: snowy mountains to ~250 m, Brokenstone ~200 m, other ranges 130–170 m, southern hills ~100 m.
- Edit by changing `RIDGES` and rerunning, or paint over the greyscale in an image editor.
- **Not wired in yet.** To use it: import the greyscale with Read/Write on, no compression, format R16; set `paintedHeights` to it, `paintedHeightScale` 250 and `baseHeight` 0, and raise the recipe's `maxHeight` from 120 to at least 260. Roads (flatten stamps) and passes still go on top.

**2026-11-16 (developer, recorded by agent):** "Widen the mountain ranges and add hills if it doesn't feel real, and add some lakes and rivers." Done in the same script:
- **Ranges** drawn 1.5× wider (`WIDEN`), each with a wide skirt of foothills; the lowlands have rolling hills and scattered knolls, so most land sits at 30–100 m.
- **Rivers** (`RIVERS`, winding, about 35 m wide, cutting valleys): Greywater (the Greenmarch ford), the Fen river, the Ember river across the steppe, the north-east river and the Quarry brook. All run to the sea.
- **Lakes** (`LAKES`): the Fen lakes and pool in the mire, the Greenmarch mere, the Tarn between the ranges, the Steppe lake.
- River and lake surfaces sit at 2.5 m, under sea level, so the bake makes them water like the sea. Where a road crosses a river it needs a bridge or a ford (a flatten stamp raised above the water, or a shallows rule), to settle when ticket 45 lays the roads.
- Names of rivers and lakes are placeholders.

**2026-11-16 (developer, recorded by agent):** Rivers should be joined up: flowing down from the mountains and through the lakes. Done:
- Every river now starts high in a range, runs through the lake near it, and ends in the sea or another river: Greywater (north-west mountains → Greenmarch mere → the bay), Fen river (west-coast range → Fen lakes → south coast), Fen beck (inner spur → Fen pool → Fen river), Ember river (snowy east arm → Steppe lake → east coast), North-east river (north-east mountains → north-east bay), Quarry brook (Brokenstone → south bay), Tarn beck (snowy mountains → the Tarn → Quarry brook).
- Each river's bed only ever goes down from source to mouth, cut into the land as a V-shaped valley: a mountain stream in a gully up high, at the water level in the lowlands, in lakes and at the mouth.
- **New file:** `docs/maps/overworld-water-ai.png`, every river and lake in white. The high stretches of rivers are above sea level, so today's bake would leave them as dry gullies; making them water needs the bake to read this mask (a later change, with ticket 45).

**2026-11-16 (agent, developer said "wire it in"):** The elevation and water maps are wired into the pipeline.
- **Water above sea level:** `HeightSource.paintedWater` (white = river or lake) and `IsPaintedWater`; the bake (`GroundBake.Classify`, new `water` argument) makes those cells water even high in the hills. Where a road (flatten stamp) crosses, it is not water: roads make their own bridges and fords.
- **Drawn:** a new `TerrainSurface.Water` for rivers and lakes above the sea; `OverworldScene` uses its water material for it when the scene's surface list stops short.
- **Fix:** painted maps are read from the red channel. A one-channel texture (R16, R8) reports green and blue as 1, so `.grayscale` read 70 % too high.
- **The continent recipe:** Hunter System > Overworld > Set Up Continent Recipe (`ContinentSetup`) copies `docs/maps/overworld-heights-ai.png` and `overworld-water-ai.png` to `Assets/Art/Maps/` (readable, linear, uncompressed; heights R16, water R8; **labelled AI**) and makes `Assets/Data/Overworld/ContinentSource.asset`: 42 × 31 tiles of 250 m (10.5 × 7.75 km), centred on the origin, max height 260 m, base 0, painted heights × 250 m, sea level 4 m.
- Tests: `ContinentMapsTest` (4). EditMode 665 passed; PlayMode 36 passed.
- **Not done:** generating and baking the continent's tiles, a scene for it, roads, gates and towns on it: ticket 45.

**2026-11-17 (agent, asked by the developer to close what can be closed):** Closed: you walked the patch and found sculpting and regenerating easy. The one open box, your sculpting hours to estimate the full map's cost, no longer matters: ticket 45 built the continent from a recipe, not by hand.
