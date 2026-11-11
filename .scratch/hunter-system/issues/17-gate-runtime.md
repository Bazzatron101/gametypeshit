# 17: Gate runtime

**What to build:** A gate assembled from a modular room kit, with checkpoints and the death rules. Solo players get three checkpoint revives then restart at the entrance. Co-op players who die choose between waiting for a checkpoint revive or respawning at the entrance.

**Blocked by:** 04: Dodge, enemy damage and player defeat, 13: Two-player Steam co-op

**Status:** ready-for-agent

- [ ] Rooms from a reusable kit chain into one playable gate
- [ ] Checkpoints are reached and saved during a run
- [ ] Solo: three revives per gate, then entrance restart
- [ ] Co-op: a dead player can choose a checkpoint revive or an entrance respawn
- [ ] A wipe costs progress inside the gate only, never the character
- [ ] Headless tests cover the revive counting and respawn choices
