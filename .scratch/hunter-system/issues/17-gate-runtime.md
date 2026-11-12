# 17: Gate runtime

**What to build:** A gate assembled from a modular room kit, with checkpoints and the death rules. Solo players get three checkpoint revives then restart at the entrance. Co-op players who die choose between waiting for a checkpoint revive or respawning at the closest checkpoint.

**Blocked by:** 04: Dodge, enemy damage and player defeat, 13: Two-player Steam co-op

**Status:** ready-for-agent

- [ ] Rooms from a reusable kit chain into one playable gate
- [ ] Checkpoints are reached and saved during a run
- [ ] Solo: three revives per gate, then entrance restart
- [ ] Co-op: a dead player can choose a checkpoint revive or an entrance respawn
- [ ] A wipe costs progress inside the gate only, never the character
- [ ] Headless tests cover the revive counting and respawn choices

## Comments

**2026-11-13 (agent):** From ticket 11 / ADR 0003:
- The gate runtime must track the latest activated checkpoint and place a joining or rejoining player there on request.
- Revives are per player, by player id, on the host's world, and survive a disconnect and rejoin. A late joiner starts with none used.
- A solo run that a friend joins switches to the co-op death rule from that moment; revives already used do not come back.
- Decide here whether co-op "respawn at the entrance" becomes "respawn at the latest checkpoint" (the ADR recommends the checkpoint).

**2026-11-13 (developer, recorded by agent):** Decided: in co-op, a fallen player who chooses to respawn comes back at the **closest checkpoint**, not the gate entrance. Recorded as the activated checkpoint nearest the party members still alive, so they rejoin the fight fastest; if the whole party is down (a wipe), the latest activated checkpoint. Solo is unchanged: three checkpoint revives, then restart at the entrance.
