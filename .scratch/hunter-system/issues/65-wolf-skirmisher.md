# 65: Wolf (fast skirmisher)

**What to build:** A fast enemy that darts in, bites, and backs off before the player can answer, and hunts in packs. The first Beast enemy. Split from ticket 62.

**Blocked by:** 63: Enemy races as data, with weaknesses

**Status:** ready-for-agent

- [ ] A "skirmisher" behaviour on the enemy definition: lunge distance and speed, how far it backs off, how long it circles before the next lunge, all data
- [ ] The lunge is telegraphed (a crouch or glow before it springs) so a player can dodge it
- [ ] Wolves spread around their target rather than all lunging from the same side
- [ ] The Wolf enemy asset, in the Beasts race, with a placeholder look labelled "AI"
- [ ] Party scaling, snapshots and co-op work; bot teammates can fight it
- [ ] Tests cover the lunge, the back-off, the dodge, and snapshots mid-lunge

## Comments

**2026-11-17 (developer, recorded by agent):** From ticket 81's triage: hunters are now Australian animal-folk, so gate creatures should read clearly as monsters (for example dire, corrupted or crystal-grown), not ordinary animals.
