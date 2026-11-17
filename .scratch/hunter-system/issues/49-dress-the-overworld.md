# 49: Buildings and environment for the overworld

**What to build:** Buildings, props and plant life so the overworld looks good and does not feel empty. From the developer's playtest of the test patch, 2026-11-15: the open world "will need buildings and environment to look good". Today the town has only the board and the ground is bare (ticket 39).

**Blocked by:** 39: Terrain pipeline and a 1 km test patch

**Status:** ready-for-human (walk the test patch: an old forest, and boulders under the ridge and hill)

- [x] Decide where the art comes from (asset store packs, made by hand, or placeholders first) and the art style
- [x] The guild town has buildings
- [x] The test patch has trees, rocks and grass, enough to judge the look and how big the world feels
- [x] A way to dress the rest of the map that scales to ticket 45

## Comments

**2026-11-15 (agent):** Opened from the playtest note on ticket 39. Ticket 36 notes that the map felt big at the current speed; dressing the ground may change that feeling.

**2026-11-16 (developer, recorded by agent):** Triaged: build now.
- **Placeholders first:** nature (trees, rocks, grass) and buildings for the towns, which are empty today.
- **Tag every placeholder art file with the Unity asset label `AI`,** so the developer can find what to replace (in the Project window, search `l:AI`). This applies to any art file an agent makes, not only this ticket's.
- Art style: the low-poly, flat-shaded look of ADR 0002.

**2026-11-16 (developer, recorded by agent):** Triaged: build now ("this still feels very basic").

**2026-11-17 (agent):** Built.

**Where the art comes from:** placeholders first, as triaged, in ADR 0002's low-poly, flat-shaded look. `Hunter System > Overworld > Make Placeholder Environment Art` makes the meshes in code and saves them to `Assets/Art/Environment` (meshes and materials). Every file is labelled `AI` (search `l:AI`). To use real art, put its mesh and materials on the kind in `Assets/Data/Overworld/Environment.asset`; running the menu again never overwrites art that is already set.

What was built:
- **Nature:** pines, leafy trees, bushes, rocks, boulders and grass. Each tile scatters it as the tile loads, off the main thread, from the tile's own ground.
  - It is the same every time and has no seams at tile edges.
  - It grows only on open grass: not on roads, the beach, water, cliffs or steep ground.
  - It grows in patches (woods, meadows) and keeps clear of the town, buildings and gate entrances.
  - It is drawn with GPU instancing, only near the camera (grass about 90 m, trees 600 m).
  - It is for looks only: trees do not block anyone.
  - Numbers per kind (density, cover, patch size, scale, slope, draw distance) are in `Environment.asset`.
- **Buildings:** the bake lays out each town (`TownPlanner`).
  - The guild town gets a guild hall behind the board, an inn, a shop, a well and houses, with doors facing the square.
  - Buildings keep off roads, the square and the board, and only stand on dry, flat ground.
  - **Their footprints are blocked in the baked tiles**, so players, enemies and bots cannot walk through walls. The ground under them is drawn as a dirt plot.
  - Buildings are scene objects, seen from afar.
- **Scales to the continent (ticket 45):** nature needs no placement, so it covers all 1,302 tiles as they stream. The continent's settlements are now village and castle markers: villages get an inn, a well and houses; castles a keep, four towers and houses. Both bakes (scene tiles and recipe) plan and block buildings.
- **Test patch:** baked with 18 buildings in the guild town. Across its 16 tiles the scatter makes trees, bushes, rocks, boulders and grass, and none in town.
- A batch bake that leaves the game content alone: `OverworldTools.BakeSceneBatch`. An older scene with no environment set gets one at its next bake.

Evidence: `EnvironmentTest` (18 tests), all passing.
- **Scatter:** same every time; no seams; stands on the ground; no water, blocked ground or beach; off roads and clear areas; slope limit; cover close to the share of ground.
- **Towns:** hall, inn, shop, well and houses; no overlaps; square and board kept clear; doors to the square; off roads and unbuildable ground; flat ground; castles; same layout every time.
- **Blocking:** a footprint is blocked, and a player walking at it stops at the wall.
- **Content:** the art is labelled AI and draws instanced; the test patch's buildings are blocked and off the road, with the board and square open; the patch grows every kind and none in town.

The full EditMode run, 779 tests: 13 failures are in work in progress from other sessions at the same time (new classes, healing, summons, the event codec, the content list), none in this ticket's files.

Not done:
- **The continent is not rebuilt.** Its scene rebuild also rebuilds the game content, and the in-progress specialty content stopped it. Once that work compiles, run `Hunter System > Overworld > Rebuild Continent Scene` (a few minutes) to lay out its villages and castles.
- **Not looked at in play.** No agent screenshot of the dressed world. The look, the density, and how big the world feels need your eye; tune them in `Environment.asset`.
- Trees do not block movement, and there are no paths through woods.
- Buildings have no interiors, and the hub's shops are not tied to the shop building.

**2026-11-17 (developer playtest, recorded by agent):** "I liked the trees and the town but would like the forest to be taller and possibly denser to make it feel like it's an old forest, and have some large boulders near the mountains."

**2026-11-17 (agent):** Done, on the test patch (the continent's scene has no nature yet):
- **An old forest:**
  - Pines are taller (1.1 to 1.9 times) and denser (110 a hectare).
  - Leafy trees are taller (1.1 to 1.8) and denser (40).
  - More bushes underneath.
  - The woods are bigger (260 m patches), and trees draw from 900 m.
- **Old pines** (new, about 20 m tall) stand over the younger trees in the same woods.
- **Mountain boulders** (new, 7 to 14 m) grow only within 35 m of steep ground (28° or more): under the Spine ridge and Lookout Hill. A new scatter rule, `nearSteepWithin` and `steepDegrees` on a nature kind.
- **New placeholder meshes,** labelled AI: `Env_OldPine_AI`, `Env_MountainBoulder_AI`.
- **Tests:**
  - `EnvironmentTest.MountainBouldersGrowOnlyNearSteepGround`.
  - `EnvironmentTest.TheTestPatchHasAnOldForestAndBouldersUnderItsMountains`: old pines over 15 m fill the woods, and boulders stand under the mountains.

Not checked by eye; denser woods may cost frame time.

**2026-11-17 (developer playtest, recorded by agent):** "Looks amazing, just need to add some mushrooms and flowers to give it a bit of colour."

**2026-11-17 (agent):** Added:
- **Flowers:** four colours (yellow, pink, blue, white) in small clusters, each in its own meadow patches.
- **Mushrooms:** red-capped and brown, in the woods (the same patches as the pines).
- **Draw distance:** both are drawn close up only (110 to 140 m), with no shadows.
- **Placeholder meshes,** labelled AI: `Env_Flowers*_AI`, `Env_RedMushroom_AI`, `Env_BrownMushroom_AI`.

`EnvironmentTest.TheTestPatchHasAnOldForestAndBouldersUnderItsMountains` now also checks that each grows on the patch. **843 EditMode and 45 PlayMode tests pass.** Not checked by eye.
