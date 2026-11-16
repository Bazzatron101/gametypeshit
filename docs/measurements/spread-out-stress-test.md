# Spread-out stress test (ticket 44)

**Measured 2026-11-16, Unity 6000.6.8f1, one Windows 11 PC, headless (`SpreadOutStressTest`, EditMode).**
The worst case ADR 0015 expects: ten players far apart on the real continent (ticket 45), each riding at
mount speed, so the host simulates and streams tiles round all ten (tickets 40 and 41).

## How to repeat

- Build the continent's tiles first: Hunter System > Overworld > Rebuild Continent Scene (about 4 minutes). They
  are not in git; without them the test is skipped.
- Run `SpreadOutStressTest` (EditMode), or `powershell -File scripts/test.ps1 -Filter SpreadOutStressTest`. It
  takes about two minutes and writes `TestResults/spread-stress.md`.

## Setup

- The host plus nine clients over the simulated network, 100 ms each way, with the game's real content.
- Ten starting places on the continent's roads, spread as far from each other and the guild town as the roads
  allow. The riders start already spread out rather than riding out from town.
- Each player gets a mount at 0.5 s and rides its own road at 16 m/s for 60 s, back and forth at a road's end,
  steering round anything in the way.
- The host streams tiles round all ten players; each client round its own (600 m to load, 800 m to drop).
  Tiles are read from the real files on worker threads.
- Gates' leaks (ticket 43) are on, so monsters appear wherever a rider passes a gate.

## Result

Five full runs on 2026-11-16: two before the streaming fix below, three after it.

| Measure | Before the fix | After the fix (last run) |
|---|---|---|
| Distance each player rode | 950 to 954 m (of 960 at 16 m/s) | 950 to 954 m |
| Closest two players at the end | 883 m apart | 883 m apart |
| Host tick (simulation and snapshots), mean / p99 | 0.22 / 1.7 ms | 0.31 / 1.7 ms |
| Host streaming per tick, mean / p99 | 1.3 / 8.8 ms | 0.06 / 0.43 ms |
| Tiles loaded on the host, mean / most | 332 / 361 | 332 / 362 |
| Managed memory grown, host and nine clients in one process | 229 MB | 229 MB |
| Host upload with all ten in | 51 KB/s (5.7 KB/s per client) | 51 KB/s |
| Longest tile load on a client, from asking to putting in | 41 to 76 ms | 74 to 498 ms over three runs (232 on the last) |
| Ticks any rider waited for ground to load | 0 | 0 |
| Largest client correction | 0 m | 0 m |

The worst single tick varies from run to run: the host tick's worst was 34 to 121 ms, and streaming's 37 to 100 ms, often on
the same run. With ten sessions and about 230 MB in one process this looks like whole-process pauses (garbage collection).
That is not proven.

## Findings

- **The simulation is not the cost.** Ten spread-out players cost the host about a quarter of a millisecond a tick.
- **Streaming was the host's biggest per-tick cost, and is now small.** At first `TileStreamer` worked out the wanted
  tiles for every player every tick: 1.3 ms mean, 8.8 ms p99. It now does so only when a player has moved a quarter of
  the load radius or 25 m (whichever is less), a player came or went, or a load failed, and at least twice a second.
  Every tick it only puts in finished loads. Now 0.06 ms mean and 0.43 ms p99. The twice-a-second refresh matters: a
  rider waiting at the edge of loaded ground does not move, and without it would never be given the next tile.
- **Occasional long ticks remain**, probably whole-process pauses: see above. Worth checking in a built host once one
  plays on the continent.
- **Memory:** about 360 tiles on the host (about 118 MB of ground at 328 KB a tile, as ticket 40 predicted) and about 35 on
  each client. The 229 MB above is all ten in one process.
- **Upload is small when spread out:** 51 KB/s for nine clients, against 880 KB/s with the party together and no
  interest filtering (ticket 14). Interest management (ticket 41) does the work.
- **Loading keeps ahead of a mount.** A tile is asked for about 37 s of riding before it is reached, and the slowest took
  half a second (counting the queue to be put in and the pauses). No rider ever waited.

## Guards in the test

Set from these numbers with room for a slower PC or disk:
- host tick mean under 1 ms
- host streaming mean under 0.5 ms
- fewer than 450 tiles on the host
- host upload under 128 KB/s
- longest client tile load under 1 s
- no rider waiting

## Not measured

- A built game over a real network (the ten-bot script `scripts/coop-bot-stress.ps1` is for players together).
- Rendering: the host here draws nothing.
- What causes the occasional long ticks.
