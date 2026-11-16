# 45: Build the full overworld terrain

**What to build:** The whole ~10.5 × 7.5 km map from `docs/maps/overworld-reference.webp`, using the pipeline from ticket 39: generated ground for the six regions, with the guild town, the other towns and castles, the ~25 gates, landmarks, waystones and the road placed by hand (ticket 36's region table).

**Blocked by:** 39: Terrain pipeline and a 1 km test patch, 40: Load the overworld in tiles around players

**Status:** ready-for-human (open Assets/Scenes/Continent.unity, rebuild it from the menu first, press Play and walk; check the layout)

- [ ] Every region's ground generated and baked into tiles, with the sea, the mountain ranges, passes and climbable faces where ticket 36 puts them (ground, sea, ranges, rivers and bridges done; no climbable faces or passes yet)
- [x] Every settlement and gate from ticket 36 placed as a marker (all 26 numbered sites; see ticket 36)
- [ ] The road joins the guild town to the east coast; a crossing measures about 40 minutes walking and about 13 mounted (ticket 36), checked in a build
- [x] Origin at the map's centre (ADR 0015)
- [ ] Hours spent recorded against the estimate from ticket 39

## Notes

- If playtests find the map too close, grow it by scaling the terrain (keep the drawing), or wind the roads more, which is cheaper (ticket 36).

**2026-11-16 (developer, recorded by agent):** Triaged: build now ("this still feels very basic").

**2026-11-16 (agent):** The heights are ready: `Assets/Data/Overworld/ContinentSource.asset` (ticket 39) reads the AI-drawn elevation and water maps from the reference map. What is left here: a continent scene, generating its 1,302 tiles, placing the towns, gates, roads (with bridges where they cross rivers) and landmarks as markers, baking, and walking a crossing.

**2026-11-16 (developer):** Storage: rebuild, don't commit. The baked tiles (415 MB) stay out of git; the recipe, maps, layout and scene are committed, and one menu click rebuilds the rest.

**2026-11-16 (agent):** Built.
- **Rebuild:** Hunter System > Overworld > Rebuild Continent Scene (`GreyboxSceneBuilder.BuildContinent`, about 4 minutes; batch: `-executeMethod HunterSystem.Editor.GreyboxSceneBuilder.BuildContinent`). Makes `Assets/Scenes/Continent.unity`, sets up the recipe (`ContinentSetup`) and bakes 1,302 tiles into `Assets/StreamingAssets/Overworld/Continent/` (gitignored), plus `Continent.asset` and its 1.6 MB coarse map (committed).
- **Bake from the recipe** (`OverworldTools.BakeFromRecipe`, `OverworldSourceAsset.bakeFromRecipe`): no Unity Terrain for the whole map (it would be over a gigabyte). To sculpt by hand, list tiles in the recipe's `sculptTiles`, run Generate Terrain Tiles (only those get a Terrain), sculpt, and rebuild: their sculpting is baked in. Height sampling is prepared once (stamp bounds, the maps' pixels), which made the 86 million samples fast enough.
- **The layout** (`ContinentLayout`, traced from the drawing; a first pass to adjust): the guild town (the hub, on the west island), 16 other towns and castles (ticket 36's names where it has them, placeholders otherwise), 16 gate spots, 15 roads. Only two gates exist in the game (the Goblin Warren at the mill cellar spot, Thornwood Hollow); the other 14 spots are landmarks named "Gate spot: ..." until their gates are made.
- **Bridges:** a new `Bridge` stamp. Wherever a road would cross the sea, a river or a lake, a bridge deck is laid at 7 m: the west bridge to the guild town's island, the south bridge, and river crossings.
- **Fixes on the way:** the guild town and Saltreach stood on sea (their icons on the drawing read as water; land patches added in the script); the Fen lakes sat on Stiltmoor (moved south, the river now passes the town); shores drop to water no steeper than 28°, measured from each river's own height; the main road was moved off a 40° foothill. **A host alone in the overworld spawned at (0, 0)**, which on the continent is a lake; a lone player now starts in the guild town (`JoinPoint`).
- **Measured:** the guild town to Saltreach by road is 14.8 km: **49 minutes walking, 15 mounted** (ticket 36 aimed for about 40 and 13). Shorten by straightening roads or shrinking the map.
- **Tests:** `ContinentCrossingTest` (4: every road walkable end to end at 2 m steps, bridges, towns and gate spots on land, the crossing time), `ContinentSceneTest` (PlayMode: you start in the guild town on land; the road reaches Saltreach; skipped when the tiles are not baked), `OverworldTest.TheFirstPlayerOutInTheWorldStartsInTheGuildTown`. EditMode 670 passed, PlayMode 38 passed.
- **Hours:** about 2 hours of the agent's wall-clock time for this ticket (maps, pipeline, layout, bakes and tests), not a human estimate. The developer's own sculpting time is still to come.

**Not done / to check:**
- Walked in a build: not done; checked by the tests and editor play mode only.
- Mountains are mostly walkable: only 0.14 % of the ground is too steep. Ticket 36 wants most mountains too steep, crossed by passes; steeper ranges are a change to the script's `RIDGES` (narrower, higher) once the developer has walked it.
- No climbable faces yet; no waystones; the nine missing gate spots from ticket 36 still wait on the developer's drawing.
- Towns are empty squares (ticket 49).
- Build size: the continent scene is in the build list, so a player build carries its 415 MB of tiles.

**2026-11-16 (agent):** All 26 numbered gate sites are placed (list on ticket 36); 2 are real gates, 24 are named spots. Heron isle and its causeway are now land. Rebuilt (1,302 tiles in 287 s) and tested: EditMode 671 passed, PlayMode 38 passed.
