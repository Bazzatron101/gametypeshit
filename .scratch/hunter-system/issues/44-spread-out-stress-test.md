# 44: Stress test with ten players spread across the overworld

**What to build:** A version of the 10-bot stress test (`docs/measurements/ten-bot-stress-test.md`, `scripts/coop-bot-stress.ps1`) where the bots spread across the overworld instead of staying together, to measure the worst case ADR 0015 expects: the host simulating tiles around up to 10 players far apart.

**Blocked by:** 40: Load the overworld in tiles around players, 41: Send each client only what is near it

**Status:** ready-for-human (review)

- [x] Bots walk and ride to different parts of the map and stay apart for a configured time
- [x] Measured and recorded: host simulation time per tick, host memory, host upload, and how long tiles take to load on a client at mount speed
- [x] Guards in the test for each figure, set from the first measurement
- [x] Results written up in `docs/measurements/`

**2026-11-16 (developer, recorded by agent):** Triaged: build now ("this still feels very basic").

**2026-11-16 (agent):** Built and measured: `docs/measurements/spread-out-stress-test.md`.

- `SpreadOutStressTest` (EditMode) puts the host and nine clients (simulated network, 100 ms each way, real content) on the real continent. They start at ten places on the roads spread as far apart as the roads allow, and each rides its own road at 16 m/s for 60 s, steering round obstacles. The host streams tiles round all ten, each client round its own, from the real tile files. It is skipped where the continent's tiles have not been built (they are not in git).
- **Measured:**
  - host tick 0.22 ms mean
  - host streaming 1.3 ms mean, 8.8 ms p99, 100 ms worst
  - 361 tiles at most on the host
  - 51 KB/s host upload
  - longest client tile load 41 to 76 ms
  - no rider ever waited for ground
  - no client correction
- **Guards** from the numbers: host tick under 1 ms, streaming under 0.5 ms, under 450 tiles, upload under 128 KB/s, client load under 1 s, no waiting.
- **The riders start already spread** rather than riding out from the guild town. A crossing from the town is ticket 45's `ContinentCrossingTest`.
- **Two mistakes of mine on the way, both in the test, not the game:**
  - the riders first checked a kilometre of road at 10 cm steps every tick, which made the run crawl
  - the test's tile loader awaited onto Unity's main thread while the main thread waited for it: a deadlock

**Finding, fixed (developer: "Yes, fix them"):** the host's tile streaming was its biggest per-tick cost (1.3 ms mean, 8.8 ms p99), because `TileStreamer` worked out the wanted tiles for every player every tick. It now does so only when someone has moved a quarter of the load radius or 25 m, a player came or went, or a load failed, and at least twice a second (a rider waiting at an edge does not move). Every tick it only puts in finished loads. Streaming is now 0.06 ms mean and 0.43 ms p99, and all tests pass (EditMode 671, PlayMode 38).

**Still open:** the worst single tick varies between runs (host tick 34 to 121 ms). It looks like whole-process pauses with ten sessions in one process, but that is not proven. Worth checking in a built host on the continent.
