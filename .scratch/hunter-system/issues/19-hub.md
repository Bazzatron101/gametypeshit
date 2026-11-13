# 19: Hub

**What to build:** A small, shared, host-owned hub where players gather, pick a gate from the gate board, enter it, and return. The hub doubles as the lobby.

**Blocked by:** 13: Two-player Steam co-op, 17: Gate runtime

**Status:** ready-for-human (play the hub with a friend)

- [x] Players can walk around a small hub together
- [x] A gate board lists available gates and starts one for the party
- [x] Returning from a gate puts the party back in the hub
- [x] The hub holds several players without breaking, with the ten-bot test used to check scale

## Comments

**2026-11-13 (developer, recorded by agent):** Decided:
- **Coming home:** a portal opens after the clear, and the host can leave at any time.
- **Starting:** only the host starts a gate.
- **Going in:** everyone marks themselves ready first.

The rules are in `docs/adr/0008-the-hub-is-the-lobby.md`.

**2026-11-13 (agent):** Built.

What was built:
- **The hub in the simulation:** a walled floor away from the gates, and per-player readiness sent as a command. The host starts a gate only when everyone present is ready. After a clear a portal opens, and walking in or a 30 s countdown brings the party home. `LeaveGate` lets the host leave a run. Joiners appear in the hub. Readiness and the countdown travel in snapshots.
- **Content:** a `HubAsset` in the game content. Only the co-op scene uses it (`useHub`).
- **On screen:**
  - `HubView` draws the hub floor, walls and board.
  - `GateBoard` (press E at the board) shows the gates, with cleared ones marked, and the party's readiness, with buttons for ready and for the host to enter.
  - `GateView` draws the portal.
  - `GateHud` shows the countdown home and the host's "Leave gate" button.
- **Host world:** clears are recorded in the host's world save.
- **Bots** ready up in the hub and walk into the portal after a clear. The bot host starts once the expected party is all there.

Evidence:
- 14 hub rule tests; breaking the ready check, the portal, the countdown or the walls fails them.
- A session test: a guest's ready mark reaches the host, the host starts, and the host leaves.
- **Ten-bot round trip, headless:** all ten gathered and readied by 0.3 s, cleared Goblin Warren and were home, rewarded, at 28.6 s.
- **Built copies:**
  - Host and guest went hub, gate, home over local UDP.
  - Ten copies did the same while measured: the host's window averaged 7.0 ms per frame, 13 ms at the 99th percentile; host upload was 736 KB/s. All nine clients came home with rewards.
- A scene test covers the hub view, the board, the portal and leaving.

Limits:
- **Not played by a person yet.**
- **No kick:** one idle player can block the ready check.
- **Not in the hub yet:** storage and the shop (stories 50 and 51) and the board's intel (ticket 20).
- **Correction figures include the teleport** between hub and gate.

**2026-11-13 (agent), after review:**
- **Fixed:**
  - **The portal** takes only players who walk in. Anyone standing on its spot when the boss falls must step out first, so the clear and its reward are seen; the reward notice also survives the trip home.
  - **Guests see cleared gates.** The simulation holds the host's world's clears (loaded from its save when hosting) and sends them in snapshots. `GateCleared` now names its gate.
  - **Ready marks** are dropped when a connection drops.
  - **A gate cannot be started over one still being run.**
  - Small tidy-ups: one join rule, a board-reach check in the simulation, and a tunable portal radius.
- **Left as is:**
  - The `-gate` command-line flag still skips the ready check (automated runs only).
  - No kick for an idle player.
  - Story 48's "showing where gates are" is a list on the board, not a map.
- **Evidence:** 397 EditMode and 23 PlayMode tests pass. The hub-to-gate-to-home network test and the two-copy fight pass.

