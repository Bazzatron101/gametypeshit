# 14: Bot clients and 10-bot stress test

**What to build:** Scripted bot clients that join a session and fight, so ten-player sessions can be tested by one developer.

**Blocked by:** 13: Two-player Steam co-op

**Status:** ready-for-agent

- [x] Bots join through the same path as real clients and perform scripted actions
- [x] A session with ten bots runs on the M1 slice
- [x] Frame time and bandwidth are recorded, and any scaling problems are written up with measurements

## Comments

**2026-11-12 (agent):** Built and measured. Write-up: `docs/measurements/ten-bot-stress-test.md`.

What was built:
- **`BotBrain`** (engine-free). It spends stat points, dodges telegraphed hits, walks to the nearest enemy, attacks, uses ready skills and follows the party when idle.
- **`BotPlayer`** puts a bot in the built game with `-bot [SEED]`. Its input goes through the normal command path, so a bot client joins like anyone else.
- **`BotStressTest`** runs ten players headless with real content.
- **`scripts/coop-bot-stress.ps1`** runs ten built copies over local UDP.

Measurements with ten players:
- **Host frame:** 6.6 to 11.2 ms mean over three runs, 12 to 21 ms at the 99th percentile (windowed, rendering, sharing the CPU with nine copies).
- **Host tick:** 0.2 ms.
- **Host upload: about 880 KB/s** over a full session. This is the scaling problem. It is now ticket 31, where compression measured 23% of the size.

The bots also found a bug: stat choices sent in a command were dropped by the input buffer, so a bot that levelled up froze. It is fixed and covered by tests.

**"Ten bots" here means a ten-player session:** the host plus nine bot clients. That is the session limit.

Not covered: the Steam relay, real packet loss, and a weaker GPU.

**2026-11-13 (agent), after review:**
- **Fixed:**
  - A bot asked twice in one tick could undo its dodge's direction. Bots now decide once per tick.
  - Any int is now a safe seed.
  - The built-game host measures only while all nine clients are connected.
  - Clients stop measuring when the host leaves, and report the median round trip.
  - The headless test counts bandwidth only once all ten have joined, and checks that corrections stay under 0.5 m.
  - The enemy top-up is now one shared helper.
  - `-bot` with no session now says why it was ignored.
- **Not covered:**
  - Bots only send honest commands. Authority against hostile input is covered by the session tests.
  - Built-game corrections reached 1 m under CPU load. Not investigated; it is noted in the write-up.

