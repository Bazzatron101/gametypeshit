# 41: Send each client only what is near it

**What to build:** Interest management for snapshots in the overworld (ADR 0015, "snapshots need interest management"). Each client gets other players, enemies and events only when they are near its own player. Today every client gets the full snapshot 20 times a second (ticket 31: about 880 KB/s host upload with ten players), and spreading out over a 10 km map would only add to that.

**Blocked by:** 38: The overworld as a third kind of place

**Status:** needs-triage

- [ ] A client's snapshot holds only entities within a configured radius of its player, plus its party's players at a lower rate so the map can show them
- [ ] An entity coming into range arrives complete; one leaving range is removed cleanly on the client
- [ ] Prediction and reconciliation still work at the edge of range (ADR 0004)
- [ ] Late join and rejoin still work (the session tests pass)
- [ ] Host upload with ten players spread across the overworld is measured (ticket 44)

## Notes

- Shares its work with 31: Cut snapshot bandwidth for ten-player sessions; do them together or let one build on the other.
- In gates and the hub the party is together, so the range may simply cover everyone there.
