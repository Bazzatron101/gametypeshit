# 67: Goblin shaman (healer/buffer)

**What to build:** An enemy that stays back and heals or strengthens nearby allies, so players learn to kill it first. Split from ticket 62.

**Blocked by:** 63: Enemy races as data, with weaknesses

**Status:** ready-for-agent

- [ ] A "support" behaviour on the enemy definition: heal amount, buff (for example more damage or speed), range, cast time and cooldown, all data
- [ ] The shaman keeps back from players and casts on the most hurt ally in range (or buffs one if none are hurt)
- [ ] The cast is telegraphed and can be interrupted by hitting the shaman during it
- [ ] Healed and buffed enemies show it clearly (a heal number, a glow while buffed)
- [ ] The Goblin Shaman enemy asset, in the Goblins race, with a placeholder look labelled "AI"
- [ ] Party scaling, snapshots and co-op work; bot teammates go for shamans first
- [ ] Tests cover healing, buffing, interrupting, and snapshots mid-cast
