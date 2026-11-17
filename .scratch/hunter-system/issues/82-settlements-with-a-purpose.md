# 82: Towns, castles and villages with a purpose

**What to build:** Make the overworld's settlements feel like real places. Each has a trade that suits where it stands, buildings that match it, streets instead of rings, more buildings, variety between places, and the life around them. From the developer's playtest, 2026-11-17 (recorded by agent): the towns, castles and villages "need to be worked on". "We need more buildings that have a purpose, like having a town for lumber or mining or fishing, and having buildings that match their locations." Also: too small and sparse, the layout looks random (rings round a square), they all look the same, and they are missing details.

**Blocked by:** none (builds on 49: Dress the overworld, and 45: Build the full overworld)

**Status:** split into 83-89 (this ticket is done when they are)

**Child tickets:**
- 83: Streets instead of rings
- 84: More building kinds, and a look per region
- 85: Each place has a trade from where it stands (lumber, mining, fishing, farming)
- 86: Farms and fields round villages
- 87: The life round the buildings (fences, stalls, carts, crates, lamps, signs)
- 88: Castles with walls, a gatehouse and a town inside
- 89: A bigger guild town with streets, walls and a market square

## Decided (developer, 2026-11-17)

- **Every settlement has a purpose:** a lumber town by the forest, a mining town by the mountains, a fishing village by the water. Its buildings match its trade and its place.
- **Castles:** stone walls and a gatehouse, a town inside the walls, a bigger keep, and houses and farms outside the walls.
- **Villages:** farms and fields, docks by water, and more buildings.
- **The guild town (the hub):** bigger, with streets, town walls and a market square.

## Notes for whoever builds it

- **Buildings block their footprints** in the baked tiles (ticket 49), so walls are walls to the simulation. Gates and gatehouses must leave the road open.
- **Placeholder art first,** labelled AI (memory rule), made by `EnvironmentArtSetup`. Real art swaps the mesh in `Environment.asset`.
- **The continent bakes in about 3 to 7 minutes** (`GreyboxSceneBuilder.BuildContinent`). The test patch has the guild town only.
- **Mind frame time:** 177 buildings today. Many more pieces means instancing per kind, as the nature does.
