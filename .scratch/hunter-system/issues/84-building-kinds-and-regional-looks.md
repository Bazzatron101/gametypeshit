# 84: More building kinds, and a look per region

**What to build:** More kinds of building, so places are fuller and differ, and a look per region, so a Fenward village looks unlike a Brokenstone one. Split from ticket 82.

**Blocked by:** none (parent: 82)

**Status:** ready-for-human (walk places in different regions on the continent)

- [x] New kinds, each with a role: smithy, chapel, stables, barn, warehouse, workshop, market hall, and the trade buildings tickets 85 and 86 need (placeholder meshes, labelled AI)
- [x] Sizes vary within a kind (a cottage, a long house, a two-storey house) so streets do not repeat
- [x] Each region has a look: its own wall and roof materials (for example thatch and timber in the Greenmarch, stone and slate in the Brokenstone hills, stilts and reed in the Fenward mire, grey stone in the snowy Riftlands), picked by where the place stands
- [x] Settlements are bigger: a village has about 10 to 20 buildings, a town 30 to 60 (numbers on the environment asset)
- [x] Tests: every kind has a mesh and a role; each region's places use that region's look

## Comments

**2026-11-17 (agent):** Built.
- **New kinds** (placeholders, labelled AI):
  - smithy (a chimney and an anvil);
  - chapel (a bell tower);
  - stables, barn, warehouse;
  - workshop (a lean-to);
  - market hall (an open roof on posts);
  - two-storey house and small cottage (more house sizes).
- **Who gets what:**
  - The guild town adds a market, chapel, smithy, stables, warehouse and workshop.
  - A village adds a chapel, smithy, stables, barn and workshop.
  - A castle adds a market, chapel, smithy, stables and warehouse.
- **A look per region** (`EnvironmentAsset.RegionLook`, `MaterialsFor`) swaps wall and roof materials:
  - greenmarch: plaster, thatch and red tiles (the kinds' own);
  - brokenstone: stone and slate;
  - saltreach: whitewash and blue tiles;
  - fenward: dark timber and reed;
  - ashen: sandstone;
  - riftlands: grey stone and snowy slate.

  Each place's region is its nearest gate spot's (`ContinentLayout.RegionNear`), saved on each building.
- **Bigger places:**
  - Villages reach 48 m and castles 58 m (was 32 and 40), with bigger flattened squares (35 and 45 m).
  - Up to 12 houses in a village, 18 in a castle, 34 in the guild town.
  - The continent went from 177 buildings to 436.
- **Tests (`EnvironmentTest`):**
  - a town has 30 to 60 buildings and a village 10 to 20;
  - each region's house looks different;
  - every kind has a mesh and a role;
  - the baked continent's buildings know their region.

Evidence: **846 EditMode and 45 PlayMode tests pass**; the test patch and the continent were re-baked. Not checked by eye.

Not done here:
- The test patch's guild town is still small (20 buildings, 40 m reach); ticket 89 grows it.
- Only 3 of the 9 castles have room for their keep (0 before); ticket 88.
- No Fenward stilts.
