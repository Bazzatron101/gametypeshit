# 12: Netcode spike

**What to build:** The M1 slice running across two instances over Steam, tried with the front-runner library (Fish-Net) and at least one alternative, ending in a recorded library decision. Current library capabilities and versions are checked, not assumed.

**Blocked by:** 10: M1 feel review

**Status:** ready-for-agent

- [x] Two instances play the same slice over Steam transport with a relay, using the front-runner library
- [x] At least one alternative is tried or ruled out with reasons
- [x] Prediction and reconciliation are demonstrated or their absence documented
- [x] Library choice recorded, with the rationale and any version constraints

## Comments

**2026-11-13 (agent):** Requirement from ticket 11 / ADR 0003: the chosen library must support a client joining a running session and receiving the full gate state (late join), then predicting normally. Include a late-join test in the spike and weigh it in the library decision.

**2026-11-13 (agent):** Spike done; decision in `docs/adr/0004-fishnet-transport-with-own-prediction.md`.
- **Decision:** Fish-Net 4.7.4 for connections and delivery (FishyFacepunch 4.1.0 + Facepunch.Steamworks 2.5.2 for Steam; Tugboat for local). Prediction and reconciliation run in our own engine-free code (`HunterSystem.Netcode`) on top of world snapshots.
  - Fish-Net's own prediction was ruled out: it predicts per networked object, and our rules live in one simulation.
  - Netcode for GameObjects was ruled out: its prediction is a first iteration released 2026-11-05.
  - Mirror and PurrNet were not tried: we need nothing from them beyond a transport.
- **Built:**
  - `SimulationWorld.CaptureSnapshot`/`RestoreSnapshot` (byte-identical continuation).
  - `HostSession`/`ClientSession` with numbered commands, acks, replay, and join/rejoin by key.
  - `SimulatedNetwork` for tests.
  - Fish-Net adapters, `SimulationRunner` host/client modes, `OnlineViews`.
  - The `Coop` scene with `CoopLauncher` (menu plus command line), `CoopPlayerBuild`, and `scripts/coop-local-test.ps1`.
- **Shown:**
  - Headless session tests at 100 ms latency, with loss and misprediction.
  - Two built copies over local UDP: the client's prediction matched the host's to under a millimetre after 32 m of play.
  - A Steam host starts on app 480.
- **Open (first checkbox):** a second Steam account joining from another PC. Steps for the developer:
  1. Copy `Builds/Coop` to PC 2. Each PC runs Steam on its own account, and the accounts are friends.
  2. PC 1: run `HunterSystem.exe`, click Host on Steam, then Copy my SteamID.
  3. PC 2: paste it and click Join on Steam, or use Join Game from the Steam friends list.
  4. Play; report the round trip, the largest correction and how it feels.
- **For ticket 13:**
  - Send or deduplicate events on clients (replays re-run ticks).
  - Interpolate remote players.
  - Measure bandwidth (full snapshots every 3 ticks, about 15 KB/s per client now).
  - Consider unreliable commands with redundancy.
  - Use a real Steam app ID before release.

**2026-11-13 (agent), after review:**
- **Hardened:**
  - malformed or truncated client messages are counted and ignored
  - non-finite movement and impossible stats are neutralised
  - on Steam the host identifies players by the connection's verified SteamID, so a client cannot claim another's character
  - command backlogs above 6 shed stale movement-only commands
  - a full session sends `Refused`
  - clients notice a closed connection
  - Steam's Join Game passes `+connect <id>`
- **ADR 0004 corrected:**
  - Status Proposed until a Steam client joins.
  - Events on clients are lost on replay, not duplicated.
  - Late commands still apply.
  - Snapshots exclude queued operations.
  - Fish-Net's interest culling is given up.

**2026-11-13 (developer test, recorded by agent):** Two PCs on two Steam accounts played together over Steam: the client joined by SteamID, and both fought Goblins together (24 killed).
- Round trip about 100 ms, largest correction 0, snapshots 2,783 bytes. The timing looked good.
- Both PCs were in the same room on one network (host on Wi-Fi, client on Ethernet).

ADR 0004 is now Accepted.
