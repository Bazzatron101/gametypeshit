# 13: Two-player Steam co-op

**What to build:** A host starts a session, a friend joins by Steam invite, and both fight the same enemy. The host is authoritative over enemies, damage, and loot, with prediction so each player's own character feels instant.

**Blocked by:** 11: Decide mid-gate joining, 12: Netcode spike

**Status:** ready-for-human (the two-PC Steam test)

- [ ] Invite and join flow works through Steam with no manual network setup
- [x] Both players see and damage the same enemy, with the host authoritative
- [x] Own-character movement and attacks feel immediate for the guest
- [ ] The mid-gate joining decision is honored

## Comments

**2026-11-13 (agent):** From ticket 11 / ADR 0003: a friend may join a session already in a gate (appearing at the latest checkpoint), and a disconnected friend may rejoin the same run. Build the join path as a full-state snapshot from the host plus a host-side "player arrives at checkpoint" operation, so join and rejoin share one mechanism.

**2026-11-13 (agent):** From the netcode spike (ADR 0004), carry these into this ticket:
- **Events:** send the host's events with each snapshot; clients' replayed and remote events are currently lost, so remote kills, hits and level-ups never show.
- **Remote players:** interpolate them between snapshots (they jump now).
- **Spawn:** joiners and rejoiners should appear at the latest activated checkpoint (17), not on a fixed line.
- **Rejoin:** rejoin without restarting the game. Listen for Steam join requests while at the menu.
- **Structure:** consider splitting `SimulationRunner` and `CoopLauncher` into offline and online parts.
- **Steam app:** move off test app 480 before release.

**2026-11-12 (agent):** Built:
- **Events:** each snapshot now carries the host's events. A client shows its own swings, dodges and skills at once (predicted) and everything else as the host reports it, each once.
- **Remote players:** they glide between snapshots instead of jumping.
- **Joining:** joiners and rejoiners appear beside the party.
- **Rejoin:** a Rejoin button reconnects without a restart.
- **Steam:** starts at the menu, so a friend's invite or "Join Game" reaches a copy that is already running.

Evidence:
- Session tests cover:
  - a kill, a hit, a level-up and a swing are each seen once by both clients
  - the attacker sees their own swing on the tick they press
  - a late joiner is not shown earlier events
  - remote players glide and arrive where the host has them
  - joiners and rejoiners appear beside the party
- Breaking each of these fails its test.
- The two-copy local test now has both players fight one target:
  - both copies saw both players' damage
  - the client saw its own damage exactly as the host counted it (220 = 220)
  - prediction matched the host to under a millimetre

Still open:
- **Box 1 (Steam invite and join):** needs the two-PC Steam test that ticket 12 also waits on.
- **Box 4 (join at the latest checkpoint):** joining and rejoining a running session work, and the rejoin rule holds. Appearing at the latest checkpoint waits for checkpoints (17); until then players appear beside the party.
- **Hit numbers on your own attacks** arrive a round trip after the swing. The host decides damage.
- **Not done:** loot (no loot exists yet) and splitting the runner and launcher.


**2026-11-12 (agent), after review:**
- **Fixed:**
  - Own swings are shown once per command, not by tick number, which can step back when the host sheds a backlog.
  - Glides last as many ticks as the host ran between snapshots, and jumps over 2 m snap instead of gliding.
  - Departed players' glides are dropped.
  - A second connection with the same key takes over without teleporting the player.
  - Unreadable host event lists are skipped and counted.
  - Rejoin waits for the old connection to stop, and the new transport ignores the old connection's late "stopped" signal.
  - Steam join requests need a numeric SteamID, and their failures show in the status.
  - The Steam command-line paths respect "Steam unavailable".
  - The autotest swings on time, not frame count.
- **Left as is (judgement calls):**
  - The codec's two switches and the client's own-action switch
  - The snapshot helpers widened to internal for the codec
  - `CoopLauncher` doing several jobs (splitting it was already "consider")
  - A defeated player who rejoins comes back still defeated (ADR 0003 keeps the run's state; revive rules arrive with gates)

**2026-11-13 (developer test, recorded by agent):** Steam on two PCs: joining by SteamID from inside the game works. Both players fought Goblins together (24 killed) with a round trip of about 100 ms and no corrections.

**Box 1 stays open.** On test app 480, accepting an invite or using "Join Game" makes Steam launch Valve's Spacewar, the game it has installed for 480, instead of ours. The co-op panel now says so.
- **To close box 1:** get the game its own Steam app ID (Steamworks, $100 fee) and test invites and Join Game again.
- **Still untried:** whether "Join Game" reaches a copy of our game that is already running.
- **Also untried:** play across the internet. The test above was on one home network.
