# Ten bots in the first raid (ticket 29)

**Date:** 2026-11-17. **Raid:** the Goblin King's Throne (`goblin-king-raid`), real content. Rules: ADR 0023.

## Headless, through the session flow (`RaidBotTest`)

A host bot plus nine bot clients over the simulated network, 100 ms each way. They gather in the hub, ready up, and
the host takes all ten into the raid. They walk in, clear the barracks, reach both checkpoints, split for the
crystals, shelter from the blasts, beat the King and come home. Times are from the raid's start.

| Measure | Value |
|---|---|
| Crystals | 4 (ten players, one per three) |
| Groups at the crystals | 3, 3, 2, 2 players |
| Shield broken | 27.6 s, with no regrow first |
| Final phase | 61.7 s |
| Blasts | 2 wound up; 0 of 20 player-blasts caught outside the shelter |
| Player defeats | 0 |
| Cleared | 87.0 s |
| Host tick, mean / p99 / max | 0.28 / 2.0 / 14.4 ms |
| Host upload per client, raid start to clear | 4.4 KB/s |
| Largest client correction, entrance to clear | under 0.5 m (asserted) |

Against the targets recorded earlier:

| Target | From | Raid |
|---|---|---|
| Host tick mean under 4 ms (guard) | ticket 14: 0.2 ms | 0.28 ms |
| Host upload per client under 16 KB/s (guard) | ticket 31 / ADR 0021: 7.7 KB/s with the party together | 4.4 KB/s |
| Corrections under 0.5 m | ticket 14 | held through the fight |

The numbers are rewritten by every run to `TestResults/raid-bots.md`; the table above is one run.

The session's own `LargestCorrection` reads 62 m over the whole run: that is the host placing everyone at the raid's
entrance and later at home, not a misprediction. The test measures from just after the entrance to the clear.

## Built game, ten copies over local UDP (`scripts/coop-gate-test.ps1 -Clients 9 -Gate goblin-king-raid`)

The built co-op player as a bot host plus nine bot clients on one PC (Fish-Net Tugboat). From the hub into the raid,
cleared, home. **PASS** on every check: all nine joined; all ten worlds agree (cleared, 6/6 rooms, checkpoint 2,
attempt 1); every client saw the checkpoints and the boss's attacks; every player was rewarded and keeps it; no
unreadable events; no player defeats.

| Measure (host headless, 144 s with all ten in) | Value |
|---|---|
| Host frame time, mean / p99 / max | 2.60 / 9.29 / 79.6 ms |
| Host upload | 29.7 KB/s total, 3.3 KB/s per client |

**Windowed host.** On 2026-11-17 the first two windowed runs ended about 90 s in: the host quit without a result or a
crash report, though the nine clients had cleared the raid. A third run, after ticket 61 and a fresh build, passed
every check:

| Measure (host windowed 1280x720, nine headless clients, 144 s) | Value |
|---|---|
| Host frame time, mean / p99 / max | 15.0 / 56.2 / 213.7 ms |
| Host upload | 29.0 KB/s total, 3.2 KB/s per client |

The mean is held near 60 fps by this machine: the ticket-14 windowed script measured 16.4 ms mean and 35 ms p99 on the
same day. So these numbers are not directly comparable with ticket 14's 6.6 to 11.2 ms mean and 12 to 21 ms p99. The
p99 is worse than either. Ten copies share one laptop's CPU and an integrated GPU. A host with nine remote clients is
the real measure. Why the first two windowed hosts quit is still unknown; it did not happen again.

**Rerun after ticket 61** (more gear, rarities): the headless test was unchanged, with the same timings, 4.4 KB/s
per client and no corrections. Ten built copies with a headless host passed again: 2.5 ms frames, 3.4 KB/s per client.
The raid's pool now holds gear, so the network test's reward check also looks in the bag (`localGear`).

## Not covered

- **Frame time on a host whose nine clients are other machines** (see above).
- **People.** Bots split, hold and shelter perfectly. Whether the window, the blast and the King's health feel right
  is for playing.
- The Steam relay and real packet loss, as in ticket 14.
