# 66: Armoured boar (shielded brute)

**What to build:** A slow, tough enemy whose armoured front blocks hits. Players flank it or break its guard with heavy attacks. The second Beast enemy. Split from ticket 62.

**Blocked by:** 63: Enemy races as data, with weaknesses

**Status:** ready-for-agent

- [ ] A "guard" on the enemy definition: the arc it blocks from the front, how much damage it blocks, and how much heavy damage breaks the guard (and for how long), all data
- [ ] Hits into its front are blocked (shown clearly, for example a spark and "Blocked"); hits from the side or back land in full
- [ ] Heavy attacks wear down the guard; a broken guard leaves it open for a few seconds
- [ ] It charges or gores with a long, readable windup
- [ ] The Armoured Boar enemy asset, in the Beasts race, with a placeholder look labelled "AI"
- [ ] Party scaling, snapshots and co-op work; bot teammates try to get behind it
- [ ] Tests cover blocking from the front, landing from behind, the guard breaking, and snapshots

## Comments

**2026-11-17 (developer, recorded by agent):** From ticket 81's triage: hunters are now Australian animal-folk, so gate creatures should read clearly as monsters (for example dire, corrupted or crystal-grown), not ordinary animals.
