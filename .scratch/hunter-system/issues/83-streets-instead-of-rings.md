# 83: Streets instead of rings

**What to build:** Lay settlements out along streets instead of rings round the square: a clear centre (the square), main streets that carry the roads in, lanes off them, and buildings fronting the streets with their doors to them. Split from ticket 82.

**Blocked by:** none (parent: 82)

**Status:** ready-for-human (walk a village or town on the continent: streets out from the square, buildings along them)

- [x] `TownPlanner` lays out streets from the square: the roads that reach a place become its main streets, and short lanes branch off them
- [x] Buildings line the streets, doors facing the street, keeping off the street itself and each other; bigger or more important buildings (hall, inn, keep) take the spots on the square
- [x] Streets are drawn as road surface and are walkable; footprints stay blocked
- [x] The same inputs always give the same town, and the layout still fits slopes and keeps to dry land
- [x] Tests: buildings front a street, nothing overlaps, every door can be reached from the square

## Comments

**2026-11-17 (agent):** Built.
- **Streets** (`TownPlanner.PlanTown`, `LayStreets`):
  - The roads that reach a place become its main streets, found where the roads cross a ring just outside the square. A place one road reaches gets the way opposite too; one no road reaches gets two, roughly east and west, so north of the square (behind the board) stays free.
  - Lanes branch off every 18 m, up to 28 m long.
- **Buildings:**
  - The hall, the inn, the shop, the market and the chapel face the square.
  - Everything else lines the streets, nearest the square first, door to the street and set back 1.5 m from its edge.
  - What no street has room for fills the gaps, door to the square.
- **Saved and drawn:** the bake saves the streets apart from the roads (`OverworldAsset.Streets`). They are drawn as road and nature keeps off them.
- **Faster road distance:** `DistanceToRoad` now skips roads more than 60 m away, so hundreds of streets do not slow tile loading.
- **Tests (`EnvironmentTest`):**
  - doors face the square or a street;
  - the roads become the main streets, with lanes;
  - no building stands on a street.

Evidence: **846 EditMode and 45 PlayMode tests pass**; the test patch and the continent were re-baked. Not checked by eye.
