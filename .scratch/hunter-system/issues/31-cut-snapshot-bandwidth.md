# 31: Cut snapshot bandwidth for ten-player sessions

**What to build:** Make a ten-player session's host upload fit a typical home connection. The 10-bot stress test measured about 880 KB/s (7 Mbit/s) of host upload over a full session, because every client gets a full snapshot 20 times a second (`docs/measurements/ten-bot-stress-test.md`).

**Blocked by:** 14: Bot clients and 10-bot stress test

**Status:** needs-triage

- [ ] Ten-player host upload is measured well under the current 880 KB/s by `BotStressTest` and `scripts/coop-bot-stress.ps1` (a target to agree in triage; compression alone measured about 23% of today's size)
- [ ] Late join, rejoin and reconciliation still work (the session tests pass)
- [ ] The stress test's bandwidth guard is tightened to the new figure

## Comments

**2026-11-12 (agent):** Options, cheapest first:
1. **Compress each snapshot** (Deflate measured 5,129 to 1,180 bytes). It costs one compression on the host per snapshot, shared by every client.
2. **Delta snapshots** against the last snapshot each client acknowledged. Clients already acknowledge by command number, so they would also need to acknowledge the snapshot tick.
3. **Fewer full snapshots:** for example, send deltas every 3 ticks and a full snapshot every second.
4. **Area-of-interest culling** for big raid gates (29).
