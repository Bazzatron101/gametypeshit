# 25: Rank promotions and map opening

**What to build:** Reaching a rank's level band triggers an authored promotion scene and opens the next region on the hub map. Ranks run E to A in the main game, then S as the end story.

**Blocked by:** 06: XP, levels, six stats and the level-up UI, 20: Mission board and text-only story scenes, 21: M3 feel review and kill-switch

**Status:** ready-for-human (review: confirm whose level promotes the world, and write the four promotion scenes)

- [x] Level bands per rank are configuration values
- [x] A promotion scene plays when the player crosses a band
- [x] New regions and gates become available on the hub map after promotion
- [x] The map and promotion belong to the host's world in co-op

## Comments

**2026-11-16 (agent):** Built. The promotion scenes are blank markers; the words are the developer's (ADR 0009).

**A choice for the developer to confirm:** the **host's own character's level** promotes the host's world. A guest's level never does (spec story 71: guests do not advance the host's campaign). There is one world save and one character per machine, so "the host's level" is clear. A host who brings an already high-level character to a new world (or to a world saved before ranks) is promoted one rank at a time on the first tick, and each promotion scene plays in order. Each character still holds its own rank, the band its level is in. Say if the world should follow someone else's level, for example the highest in the party.

What was built:
- **Ranks are configuration** (`ProgressionDefinition.Ranks`, edited on `Assets/Data/Progression.asset`). Each rank has an id, the first level of its band, the regions it opens and whether it brings the first mount. The defaults are the spec's provisional bands (E 1, D 16, C 36, B 56, A 76, S 96) and ticket 36's map: D opens Fenward mire (`fenward`), B opens the Ashen steppe (`ashen`), C and A open no region. S stays past the main-game cap of 95 until the S story raises it.
- **The world has a rank** (the host's world's), kept in the world save (format 4; format 3 and older still load, at the first rank) and carried to guests in snapshots with its owner. When the owner's level enters the next band, the world is promoted (`RankPromoted` event): the rank's regions open at once, even mid-gate.
- **Promotion scenes:** a new story trigger, `RankPromotion`, with the rank on the text scene asset. Like a gate-clear scene, it waits until the party is home. There are four scenes, `PromotionD` to `PromotionA` in `Assets/Data/Scenes`, with three blank lines each, so the writing list now has 19 slots. The S promotion belongs to the S story and has no scene yet.
- **Gates have a rank** (`rank` on `GateDefinitionAsset`; Goblin Warren and Thornwood Hollow are E). The board offers a found gate once its region is open **and** the world has reached the gate's rank. That is how A-rank, which opens no region, puts new gates on the board in regions the party already knows. A gate above the world's rank can still be walked into at its entrance, as before (ADR 0015).
- **The gate board** now shows the world's licence ("The guild licenses this party at D-rank").
- **The first mount comes with D-rank** (ADR 0015), for every character at its own D-rank, guests included; F2 stays as a development shortcut.
- Content checks: the ranks rise E to S from level 1, A is within the cap, one rank brings the mount, no rank opens a starting region, and every gate's and promotion scene's rank exists.

Evidence:
- `RankPromotionTest` (12 tests): bands come from config; crossing a band promotes once and plays its scene once; a promotion in a gate is told at home; a host past several bands is promoted rank by rank with each scene in order; a loaded world is not promoted again; a promotion opens its region and its gates come on the board; a higher-rank gate in a known region waits for its rank; a guest's level never promotes the host's world or opens its map; a world with no host (the arena) is never promoted; snapshots carry the rank and owner to guests without promoting again; a guest reaching D gets their own mount.
- A world-save test for the rank, the codec covers `RankPromoted`, and the new content check.
- **696 EditMode and 40 PlayMode tests pass.**

Not done here:
- There is no drawn map screen. The "hub map" is the gate board plus the open regions; the continent's markers do not yet show which regions are open.
- Brokenstone hills (a C-rank story beat) and the Riftlands (the S story) open by story, not by rank. Story beats that open regions are not built.
- Quest-gated promotions (spec: "some promotions gated by quests") are ticket 26's.
