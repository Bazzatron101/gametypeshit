# 31: Cut snapshot bandwidth for ten-player sessions

**What to build:** Make a ten-player session's host upload fit a typical home connection. The 10-bot stress test measured about 880 KB/s (7 Mbit/s) of host upload over a full session, because every client gets a full snapshot 20 times a second (`docs/measurements/ten-bot-stress-test.md`).

**Blocked by:** 14: Bot clients and 10-bot stress test

**Status:** ready-for-human (review)

- [x] Ten-player host upload is measured well under the current 880 KB/s by `BotStressTest` and `scripts/coop-bot-stress.ps1` (a target to agree in triage; compression alone measured about 23% of today's size) (`BotStressTest`: 69 KB/s; built game: 71 KB/s)
- [x] Late join, rejoin and reconciliation still work (the session tests pass)
- [x] The stress test's bandwidth guard is tightened to the new figure

## Comments

**2026-11-12 (agent):** Options, cheapest first:
1. **Compress each snapshot** (Deflate measured 5,129 to 1,180 bytes). It costs one compression on the host per snapshot, shared by every client.
2. **Delta snapshots** against the last snapshot each client acknowledged. Clients already acknowledge by command number, so they would also need to acknowledge the snapshot tick.
3. **Fewer full snapshots:** for example, send deltas every 3 ticks and a full snapshot every second.
4. **Area-of-interest culling** for big raid gates (29).

**2026-11-16 (developer, recorded by agent):** Triaged: build now ("this still feels very basic").

**2026-11-16 (agent):** Built options 1 and 2 together, without acknowledgements. EditMode 677 passed.
- **How** (ADR 0021): every snapshot and welcome is Deflate-compressed. A snapshot's world goes as its XOR with the last world sent to that connection, which is mostly zeros. When the size differs (something arrived or left), the whole world goes, still compressed. The channel is reliable and ordered, so the client always has that last world, and no acknowledgement is needed. In a gate or the hub, clients last sent the same world share one encoded message.
- **Code:** `Wire.Snapshot(snapshot, baseline)` / `Wire.ReadSnapshot(message, baseline)`. `HostSession` keeps each connection's `Baseline`, reset on a new connection and set by the welcome. `ClientSession` rebuilds each snapshot's world from the one before, in order.
- **Measured** (`docs/measurements/ten-bot-stress-test.md`): ten bots together, 854 to 69 KB/s (7.7 KB/s per client); spread out (ticket 44), 51 to 9.8 KB/s; mean snapshot message 391 bytes. Mean host tick 0.18 to 0.26 ms.
- **Guards:** `BotStressTest` 16 KB/s per client (was 128); `SpreadOutStressTest` 32 KB/s (was 128). `InterestManagementTest`'s "under a quarter of everything" no longer means much (both are a few KB/s, mostly overhead), so it now checks that near is less than everything and under 16 KB/s.
- **Tests:** `SnapshotEncodingTest` (5: round trip against a baseline, smaller than compression alone, a size change sends the whole world, a missing or wrong baseline is refused, a compressed welcome), and `NetSessionTest.SnapshotsWrittenAgainstEachClientsLastWorld...` (kills, an arrival and a rejoin mid-run; both clients match the host).

Open:
- If snapshots ever move to an unreliable channel, the baseline must become an acknowledged one (ADR 0021).

**2026-11-16 (agent):** Built game, fresh co-op build, `scripts/coop-bot-stress.ps1 -Seconds 30 -HeadlessHost`: PASS. Host upload 71.1 KB/s (7.9 KB/s per client), download 14.2 KB/s, host frame 2.07 ms mean. Every client's "largest correction" was about 62 m: the bot host now starts in the hub and takes everyone into the first gate, and that move counts as a correction. It is not a misprediction, but the figure no longer measures one; worth excluding moves the host makes (gate entry, rejoin) from it.
