# Snapshot interest: host upload with ten players spread out

> **Since ticket 31 (2026-11-16):** snapshots go compressed, as the difference from the last world each client was
> sent (ADR 0021). The same measurement is now about 14 KB/s sending everything and 8 KB/s within 200 m. Both are mostly
> per-message overhead, so the test guards the near figure (under 16 KB/s) rather than its share of everything.

Ticket 41. Measured 2026-11-16 in the editor (`InterestManagementTest.MeasureTheHostsUploadWithTenPlayersSpreadAcrossTheOverworld`),
over the simulated network, with no compression.

## Setup

- Ten players (the host and nine clients) 500 m apart on a line in the overworld, each walking north.
- Five enemies around each player (50 in all), standing in for gate leaks (ticket 43).
- A snapshot every 3 ticks (20 a second), as in the game. Far party members' positions ride along once a second.
- Measured over 10 s, after everyone has joined.

## Result

| Each client is sent | Host upload |
| --- | --- |
| Everything (as before ticket 41) | 1,639 KB/s |
| Only what is within 200 m of its player | 178 KB/s (11 %) |

Spread out, each client's world holds itself and the five enemies by it instead of ten players and fifty
enemies. The cut grows with the party's spread: in a gate or the hub, where the party is together, every
client is sent everything, as before.

## What it does not cover

- **The party together.** In a gate or the hub nothing is cut, so ten players there still cost about what
  the ten-bot stress test measured (880 KB/s, `ten-bot-stress-test.md`). Compression and delta snapshots
  are ticket 31's.
- **178 KB/s is still about 1.4 Mbit/s** for nine clients. It has not been broken down yet; the likely
  bulk is each client's own player record (skills, gear, stats) sent in full 20 times a second.
  Ticket 31's compression or deltas would cut that.
- Real networks, real machines and the full map: ticket 44.
