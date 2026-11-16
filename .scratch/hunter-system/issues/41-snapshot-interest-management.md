# 41: Send each client only what is near it

**What to build:** Interest management for snapshots in the overworld (ADR 0015, "snapshots need interest management"). Each client gets other players, enemies and events only when they are near its own player. Today every client gets the full snapshot 20 times a second (ticket 31: about 880 KB/s host upload with ten players), and spreading out over a 10 km map would only add to that.

**Blocked by:** 38: The overworld as a third kind of place

**Status:** ready-for-human (review)

- [x] A client's snapshot holds only entities within a configured radius of its player, plus its party's players at a lower rate so the map can show them
- [x] An entity coming into range arrives complete; one leaving range is removed cleanly on the client
- [x] Prediction and reconciliation still work at the edge of range (ADR 0004)
- [x] Late join and rejoin still work (the session tests pass)
- [x] Host upload with ten players spread across the overworld is measured (ticket 44)

## Notes

- Shares its work with 31: Cut snapshot bandwidth for ten-player sessions; do them together or let one build on the other.
- In gates and the hub the party is together, so the range may simply cover everyone there.

## Comments

**2026-11-16 (agent):** Built. EditMode 632 passed.

- **Simulation** (`SimulationWorld.Interest.cs`): `InterestOf(viewer, radius)` picks, out in the overworld, the viewer's own player and every player, enemy and projectile within the radius on the floor. Whatever those point at comes too (an enemy's target, a bolt's thrower), so a cut-down snapshot always restores whole. In a gate or the hub it is everything. `CaptureSnapshot(interest)` writes only what the interest includes.
- **Events:** a client is told about an event when it names the client's own player, or when every player and enemy it names is in its interest. Events naming no one (a gate cleared, a story scene) always go. So a fight 500 m away is not sent, but the XP it gives you is.
- **Host** (`HostSession`): in the overworld, each client gets its own snapshot. In a gate or the hub, one snapshot is shared as before. The welcome for a joiner is cut down the same way. `InterestRadius` is 200 m. A cut-down snapshot carries only its own client's acknowledged command.
- **Far party members:** about once a second (`FarPlayersEverySnapshots` = 20), a snapshot also carries where every left-out player stands, for a map. `ClientSession.FarPlayers` keeps the latest; a player who comes near is in the world instead. Nothing draws a map yet.
- **Entering and leaving range** needs nothing special: each snapshot restores the client's world, so whatever comes into range arrives whole (an enemy keeps the damage it took far away) and whatever leaves is gone.
- **Measured** (`docs/measurements/snapshot-interest.md`): ten players 500 m apart with five enemies each cost the host 1,639 KB/s sending everything and 178 KB/s within 200 m (11 %).
- **Tests:** `InterestManagementTest` (7): only near players are sent, far ones for the map; the hub sends everything; an enemy coming into range arrives whole and leaving is gone; far fights are not news but your XP is; prediction holds while another player crosses the range edge three times; a far late joiner is welcomed with only what is near; the upload measurement.

**Left for later:**
- With the party together (a gate, the hub), nothing is cut: ticket 31's compression or deltas.
- Even spread out, 178 KB/s is still about 1.4 Mbit/s. Not broken down yet; the likely bulk is each client's own full player record (skills, gear, stats) sent 20 times a second. Compression or deltas (ticket 31) would cut it.
- Remote enemies near the edge of a client's range may move a little differently in its prediction than on the host (they cannot see a player it cannot see). That is corrected by the next snapshot and never affects the client's own player.
- A map screen to show `FarPlayers`.
