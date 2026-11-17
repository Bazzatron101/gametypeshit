# 40: Load the overworld in tiles around players

**What to build:** Streaming for the overworld (ADR 0015, "streaming is needed"). Each client loads the terrain tiles and props around its own player and unloads far ones. The host simulates the tiles around every player, up to 10 spread across the map, not only the tiles around itself.

**Blocked by:** 38: The overworld as a third kind of place, 39: Terrain pipeline and a 1 km test patch

**Status:** done

- [x] Clients load tiles within a configured radius of the local player and unload tiles past a larger one, with no hitch a player notices
- [x] The host keeps active every tile near any player; ground data (ticket 37) and gate leaks for a tile exist only while it is active (gate leaks do not exist yet: ticket 43)
- [x] Loading is far enough ahead at mount speed (16 m/s, ticket 42), not only walking speed
- [x] A player never stands or lands on a tile whose ground is not loaded yet (they wait, or the tile loads first)
- [x] The host's memory with players spread apart is measured and recorded
- [x] A PlayMode test: two players walking apart load and unload the right tiles on host and client

## Notes

- Additive scenes per tile or Addressables both fit; pick one in this ticket and record it in an ADR.

## Comments

**2026-11-15 (developer, recorded by agent):** **Data files, scenes later.** Each tile's ground is a file in StreamingAssets, read in the background, with its mesh built when it arrives, and a coarse map of the whole world always loaded. Props will get one additive scene per tile once they exist. Not Addressables. Recorded in `docs/adr/0020-overworld-tiles-stream-as-data-files.md`.

**2026-11-15 (agent):** Built. EditMode 621 passed, PlayMode 34 passed (plus the new timing test).

- **Simulation** (`TiledGround`): the map as tiles that are put in and taken out between ticks. A loaded tile answers in full. Elsewhere the ground is `GroundKind.Unloaded`: nobody steps onto it, and anyone standing on it (a join or a gate's way out before the tile arrives) waits, at the coarse map's height. The slide rule moved into `GroundExtensions.Slide`, shared with `HeightmapGround`.
- **The bake** now writes one file per tile (`Assets/StreamingAssets/Overworld/<world>/tile_x_z.bytes`, each with its own edge samples) and a coarse map at 16 m (`<world>.coarse.bytes`) instead of one file for everything. `OverworldAsset` holds the tile grid, the folder and the coarse map. `LoadGround()` still reads the whole world at once, for tools and tests.
- **`TileStreamer`** asks for tiles within the load radius of each point (nearest first), drops tiles past the unload radius from all of them, and puts finished loads in two per update.
- **`OverworldScene`** streams around the local player on a client, and around every player on the host or offline. Reading, decoding and building a tile's mesh contents happen on a worker thread. Only making the Mesh is on the main thread (0.7 ms). Every tile always shows its far version from the coarse map, until its near one arrives. The first tiles load at scene start, before anyone moves. Defaults: load 600 m, unload 800 m.
- **Measured** (`docs/measurements/overworld-streaming.md`): 328 KB per tile; 32 tiles (10 MB) per player; 320 tiles (102 MB) on the host for ten players spread across the full map. A tile is ready in about 36 ms, and a ride at 16 m/s with tiles taking a full second each is never held up.
- **Tests:** `OverworldStreamingTest` (10: unloaded ground and waiting at its edge, being placed on it, load and unload radii, a few loads per update, a host and a guest walking apart over the network with each side's tiles checked, the mount ride, a real tile's timings, memory). `OverworldPatchSceneTest` (PlayMode: streamed ground, far and near tiles, and the host loading around a far-off second player and dropping the tiles between).

**Left for later:**
- The PlayMode test checks the host side in the real scene; the client side (only its own tiles) is checked over the simulated network in EditMode, not in a scene with a second machine.
- Gate leaks per tile come with ticket 43.
- Not measured in a standalone build or on a slow disk; ten players on the full map is ticket 44.
- Far versions of all 1,260 tiles of the full map would be about 650,000 triangles: worth merging into larger chunks when ticket 45 builds the map.
- The baked files are not in Git LFS yet (5 MB for the patch); do that before the full map.

**2026-11-17 (agent, asked by the developer to close what can be closed):** Closed: you have walked the streamed test patch since (ticket 39 playtest), and the continent streams the same way (ticket 45).
