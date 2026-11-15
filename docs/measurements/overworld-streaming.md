# Overworld streaming: memory and load times

Ticket 40, ADR 0020. Measured 2026-11-15 in the editor (EditMode tests in `OverworldStreamingTest`),
on the developer's PC, with the 1 km test patch (ticket 39) and the full map's tile grid (ADR 0015).

## Setup

- Tiles of 250 m, 257 × 257 height samples (about 1 m), so 256 × 256 ground kinds.
- Load radius 600 m, unload radius 800 m (`OverworldScene`'s defaults). A tile loads when its square comes
  within 600 m of a player, and is dropped once it is more than 800 m from every player.
- Full map: 42 × 30 = 1,260 tiles (about 10.5 × 7.5 km).

## Memory (the simulation's ground)

| | Tiles | Ground in memory |
| --- | --- | --- |
| One tile | 1 | 328 KB |
| One player | 32 | 10.2 MB |
| Ten players spread apart (host) | 320 | 102.4 MB |
| The whole map, if all loaded | 1,260 | about 400 MB |

A client holds one player's tiles (about 10 MB of ground). The host holds every player's, so ten players
spread across the map cost it about 100 MB, a quarter of loading everything. The coarse map (16 m) for
the whole map is about 1.5 MB more on every machine.

The shown meshes of loaded tiles add to this on clients (about 24,000 vertices per near tile at 4 m
facets). The host draws them too when it is also a player; a dedicated host would not need them.

## Load times (one tile)

| Step | Where | Worst of three tiles |
| --- | --- | --- |
| Read and decode the file | Worker thread | 4.9 ms |
| Build the shown mesh's contents | Worker thread | 31.0 ms |
| Make the Mesh | Main thread | 0.7 ms |

The main thread spends under a millisecond per tile, and at most two tiles go in per frame, so loading
does not cause a hitch. A tile is ready about 36 ms after it is asked for.

## Fast enough for a mount

`LoadingKeepsAheadOfAMountAt16MetresASecond` rides 5 km at 16 m/s while every tile takes a full second
to arrive, about 28 times the measured time, and is never held up. A tile is asked for 600 m ahead,
about 37 s of riding. With the next tile only asked for at its edge, the same ride is held up at every tile.

## Not measured

- A standalone build, rather than the editor.
- A slow hard disk; these numbers are from the developer's PC.
- Ten real players on a full-size map: that is ticket 44's stress test, once the full map exists (ticket 45).
