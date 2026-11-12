# 13: Two-player Steam co-op

**What to build:** A host starts a session, a friend joins by Steam invite, and both fight the same enemy. The host is authoritative over enemies, damage, and loot, with prediction so each player's own character feels instant.

**Blocked by:** 11: Decide mid-gate joining, 12: Netcode spike

**Status:** ready-for-agent

- [ ] Invite and join flow works through Steam with no manual network setup
- [ ] Both players see and damage the same enemy, with the host authoritative
- [ ] Own-character movement and attacks feel immediate for the guest
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

