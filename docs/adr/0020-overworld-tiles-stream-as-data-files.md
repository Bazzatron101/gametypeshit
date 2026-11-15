# 0020: Overworld tiles stream as data files

**Status:** Accepted (2026-11-15)

## Context

ADR 0015 says the overworld (about 10.5 × 7.5 km) cannot all be loaded: terrain loads in tiles around each
player, and the host simulates the tiles around every player, up to 10 spread apart. Ticket 40 asked for
one way to load tiles, additive scenes per tile or Addressables, recorded here.

Since ticket 39 a tile is mostly data. Its ground is about 330 KB (heights and kinds at about 1 m), and the
mesh players see is built from that same data when it loads, so the art and the rules cannot drift. There
are no props yet. On 2026-11-15 the developer chose data files over both options in the ticket.

## Decision

- **Each tile's ground is its own file** in `StreamingAssets/Overworld/<world>/tile_<x>_<z>.bytes`, written
  by the bake. A tile is read and decoded off the main thread; the simulation and the shown mesh get it
  on the main thread when it is ready.
- **A coarse map of the whole world stays loaded** (16 m cells, in the overworld asset). Distant ground is
  drawn from it, and it gives the height of ground whose tile is not loaded yet.
- **Ground whose tile is not loaded is "not loaded yet":** nobody can step onto it. A player at the edge of
  the loaded ground waits there until the tile arrives (ticket 40: "they wait, or the tile loads first").
  Someone placed on an unloaded tile (a join, coming out of a gate) stands at the coarse map's height and
  waits too.
- **Who loads what:** a client loads the tiles within a radius of its own player and drops them past a
  larger radius. The host loads the tiles around every player, so its simulation has every player's ground.
  The radius is set for a mount at 16 m/s, not for walking.
- **Props, when they exist, get one additive scene per tile,** loaded beside the tile's data. Not
  Addressables: that adds a package and a content build step to every build, for nothing the data files
  and scenes cannot do.

## Consequences

- Host and clients read the same files from the same build, so their ground agrees and prediction holds
  (ADR 0004). A client and the host may load a tile at slightly different moments; at worst that is a small
  correction at the edge of what is loaded.
- StreamingAssets files are plain files beside the build, not compressed or packed. That is fine on PC,
  which is the only platform.
- The full map is about 1,260 tiles of 250 m: about 400 MB of ground data on disk, but only the tiles
  around players in memory (see `docs/measurements/overworld-streaming.md`).
- The baked files should go into Git LFS before the full map (ticket 45) is baked.
