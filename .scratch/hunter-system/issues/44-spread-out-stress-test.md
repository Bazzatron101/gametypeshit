# 44: Stress test with ten players spread across the overworld

**What to build:** A version of the 10-bot stress test (`docs/measurements/ten-bot-stress-test.md`, `scripts/coop-bot-stress.ps1`) where the bots spread across the overworld instead of staying together, to measure the worst case ADR 0015 expects: the host simulating tiles around up to 10 players far apart.

**Blocked by:** 40: Load the overworld in tiles around players, 41: Send each client only what is near it

**Status:** ready-for-agent

- [ ] Bots walk and ride to different parts of the map and stay apart for a configured time
- [ ] Measured and recorded: host simulation time per tick, host memory, host upload, and how long tiles take to load on a client at mount speed
- [ ] Guards in the test for each figure, set from the first measurement
- [ ] Results written up in `docs/measurements/`

**2026-11-16 (developer, recorded by agent):** Triaged: build now ("this still feels very basic").
