# 64: Goblin archer (ranged enemy)

**What to build:** The first enemy that fights from range. The goblin archer keeps its distance from its target, backs away if a player gets close, and fires a telegraphed shot the player can dodge, jump or step out of. Split from ticket 62.

**Blocked by:** 63: Enemy races as data, with weaknesses

**Status:** ready-for-agent

- [ ] A "ranged" behaviour on the enemy definition: preferred distance, shot speed, damage, windup, cooldown, all data
- [ ] The archer keeps to its preferred distance and backs off when a player closes in
- [ ] The shot is telegraphed (windup glow plus a line or marker toward its target) and travels as a projectile, reusing the player projectile code where it fits
- [ ] A dodge, jump or sidestep avoids it; the shot can hit summons (ticket 57)
- [ ] The Goblin Archer enemy asset, in the Goblins race, with a placeholder look labelled "AI"
- [ ] Party scaling (ticket 27), snapshots and co-op (host decides hits; snapshots carry shots in flight) all work
- [ ] Bot teammates go after archers rather than ignore them
- [ ] Tests cover keeping distance, the shot hitting, the shot being dodged, and snapshots mid-shot
