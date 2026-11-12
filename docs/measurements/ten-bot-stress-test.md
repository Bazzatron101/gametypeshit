# Ten-bot stress test (ticket 14)

**Measured 2026-11-12/13, Unity 6000.6.8f1, one Windows 11 PC.** A ten-player session (the limit):
the host's own player plus nine bot clients, every one a `BotBrain`. They fight Goblins and Hobgoblins
from the game's real content; 8 living enemies are kept topped up and defeated ones cleared.

## How to repeat

- **Headless, in the tests:** `BotStressTest` (EditMode).
  - Host bot plus 9 bot clients over the simulated network, 100 ms each way, for 30 s.
  - Bots are sampled twice a tick through the game's own input buffer.
  - Bandwidth is counted from 2 s in, once all ten have joined.
  - It writes `TestResults/bot-stress.md`.
- **Built game, real transport:** `powershell -File scripts/coop-bot-stress.ps1`. Build first; the script says how.
  - It runs ten built copies over local UDP (Fish-Net Tugboat).
  - The host runs in a window at 1280x720, so its frame time includes rendering ten players. The nine clients are headless.
  - The host measures for 60 s, starting once all nine clients are connected and after a 3 s warm-up. It stops early if any client leaves.

## Results

**Bandwidth.** All three runs were measured over a full session.

| | Headless test | Built, run 1 | Built, run 2 | Built, run 3 |
|---|---|---|---|---|
| Host upload, total | 854 KB/s | 882 KB/s | 888 KB/s | 888 KB/s |
| Host upload, per client | 95 KB/s | 98 KB/s | 99 KB/s | 99 KB/s |
| Host download, total | 11 KB/s | 11 KB/s | 11 KB/s | 11 KB/s |
| Snapshot size | 4.7 KB mean, 6.0 KB max | 5.9 KB last | 5.7 KB last | 5.6 KB last |

**Frame and tick times.**

| | Headless test | Built, run 1 | Built, run 2 | Built, run 3 |
|---|---|---|---|---|
| Host simulation tick, mean / p99 | 0.18 / 2.6 ms | (inside the frame) | | |
| Host frame (windowed), mean / p99 / max | | 6.6 / 12.5 / 30 ms | 9.0 / 17.6 / 226 ms | 11.2 / 21.3 / 32 ms |
| Client tick or frame, mean / worst p99 | 0.19 / 1.7 ms per tick | 2.2 / 5.4 ms per frame | 3.0 / 16 ms | 4.5 / 28 ms |

**Session.**

| | Headless test | Built, run 1 | Built, run 2 | Built, run 3 |
|---|---|---|---|---|
| Enemies defeated / player defeats | 114 in 30 s / 0 | 252 / 0 | 241 / 0 | 255 / 0 |
| Largest client correction | 0 m | 0.44 m | 0.97 m | 0.60 m |
| Round trip, median | 12 ticks (100 ms each way + up to 3 ticks to the next snapshot) | | | 9 ticks or less |

A note on the runs:
- Built run 1 was taken before the client round-trip figure was fixed. It reported the value after the host had quit, so it is left out.
- All ten copies share one CPU, so the built frame times include the nine headless clients' load. The runs vary with what else the PC was doing.

Where a snapshot's bytes go (same content, worlds built for the measurement):

| Part | Bytes |
|---|---|
| Empty world | 36 |
| Each player, fresh | 219 |
| Each enemy | 45 |
| The session's last world snapshot | 5,129 |
| The same, compressed with Deflate (fastest) | 1,180 (23%) |

## Findings

1. **Bandwidth is the scaling problem.** The host sends every client a full snapshot 20 times a second, so upload grows with the number of players times the size of the world.
   - For ten players that is about 880 KB/s, or 7 Mbit/s, measured over a full session. That is more than many home connections can upload while also playing, and Steam's relay adds its own overhead.
   - Players make up most of the snapshot. Their skills, cooldown tables and modifiers are written in full, by id, every time, and they grow as players level up.
   - The cheapest fix is to compress snapshots. Deflate alone takes them to 23%, about 200 KB/s in total. It costs one compression per snapshot on the host, shared by every client, and one decompression per client.
   - The next steps are delta snapshots against the last one a client acknowledged, so unchanged players and enemies cost almost nothing, and then area-of-interest culling for large raid gates.
   - This is recorded as ticket 31.
2. **Frame time is acceptable, but this setup cannot fully measure it.**
   - The host simulation tick averages 0.2 ms with ten players.
   - The windowed host averaged 6.6 to 11.2 ms per frame, with p99 12 to 21 ms, while sharing the CPU with nine other copies. That is around or inside a 60 Hz frame.
   - There were occasional hitches, up to 226 ms once. They have not been profiled; snapshot allocation and garbage collection are the likely suspects.
   - A host on its own PC, with clients elsewhere, is the real test.
3. **Clients keep up, but corrections were larger in the built game.**
   - Predicting and replaying about 12 commands costs a client about 0.2 ms a tick.
   - Headless, predictions never needed correcting. In the built runs the largest single correction was 0.4 to 1.0 m.
   - Not investigated. The likely causes are clients' frames stalling under CPU load, which makes the host repeat their last movement and shed backlogged commands, and enemies the host moved between snapshots. This is worth checking once the copies run on separate PCs.
4. **Found by the bots:** the game's input buffer dropped stat choices made through a command, as opposed to the stats menu. A bot that levelled up stood still forever. This is fixed: the buffer now queues them like menu choices.
   - The headless test now feeds bots through that same buffer at two samples a tick. It checks that every bot that levels up spends its points.
   - A second frame-versus-tick mistake was also fixed. Asked again in the same tick, a bot could undo its dodge's direction. Bots now decide once per tick.

## Not measured

- **The Steam relay.** Everything here ran over loopback; the two-PC Steam test is still pending (tickets 12 and 13).
- **Packet loss and jitter on a real network.**
- **A host with the PC to itself,** a weaker GPU, or real art instead of greybox.
- **Authority under attack.** The bots only send honest commands. Hostile input (garbage, impossible values, identity claims, backlogs) is covered by `NetSessionTest`, not by bots.
