# 27: Party-size enemy scaling

**What to build:** Enemies scale in three tiers by party size (solo, 2 to 4, 5 to 10) through health and damage multipliers and spawn counts. Player power is never scaled, and every normal gate stays soloable.

**Blocked by:** 13: Two-player Steam co-op, 17: Gate runtime

**Status:** ready-for-agent

- [ ] Three tiers apply multipliers and spawn counts from data
- [ ] Player stats are not reduced for over-leveled players and rewards are not reduced
- [ ] A normal gate is still clearable solo
- [ ] Headless tests verify the multipliers at each tier boundary
