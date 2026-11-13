# 27: Party-size enemy scaling

**What to build:** Enemies scale in three tiers by party size (solo, 2 to 4, 5 to 10) through health and damage multipliers and spawn counts. Player power is never scaled, and every normal gate stays soloable.

**Blocked by:** 13: Two-player Steam co-op, 17: Gate runtime

**Status:** ready-for-agent

- [x] Three tiers apply multipliers and spawn counts from data
- [x] Player stats are not reduced for over-leveled players and rewards are not reduced
- [x] A normal gate is still clearable solo
- [x] Headless tests verify the multipliers at each tier boundary

## Comments

**2026-11-13 (agent):** From ticket 11 / ADR 0003: the party can grow mid-gate (joins at checkpoints, rejoins). Recompute the tier when the party changes, but apply it only to enemies spawned afterwards; enemies already alive keep their stats.

**2026-11-14 (agent):** Built; the rules are in `docs/adr/0010-party-size-scaling.md`.

What was built:
- **Tiers by players present:** solo, 2 to 4, and 5 to 10. Each sets health, damage and spawn percentages, from the `PartyScalingAsset` in the content. Solo is always the gate as authored.
- **Applied when enemies spawn,** in room fills and a boss's adds, so a friend joining changes only later rooms. Each enemy keeps its own scaled health and damage, which travel in snapshots.
- **Never scaled:** bosses are not copied. Players and XP per kill are untouched.

Starting numbers, to tune by playing:

| Tier | Health | Damage | Enemies |
|---|---|---|---|
| 2 to 4 players | 160% | 115% | 150% |
| 5 to 10 players | 250% | 130% | 200% |

Evidence:
- **17 headless tests:** every tier boundary (1, 2, 4, 5, 10), damage rounding, the boss and its adds, away players, a mid-fight join, unscaled players and XP, no scaling data, and snapshots. Breaking the tier count, damage or spawn counts fails them.
- **Solo is still clearable:** one bot still clears Goblin Warren solo (`GatePlaythroughTest`, 50 s, unchanged).
- **Parties:** three bots need 41 s (was 32 s), and ten bots do the hub round trip in 32 s.
- **Built copies:** a built host and guest cleared the scaled gate over local UDP (29 enemies instead of 19).
- **Content check:** no tier is easier than the one below it.

Not done: whether the numbers feel right. That is for the M3 feel review (ticket 21).

**2026-11-14 (agent), after review:**
- **Fixed:**
  - Rooms spawn in their authored order, bosses included, so solo is exactly the gate as authored.
  - Extra enemies are spread across the whole authored mix and kept inside their own room.
  - A game with no scaling data no longer crashes.
- **Tested:** the boss's XP and the clear reward are the same in any party.
- **Recorded in ADR 0010:** a room filled for a party keeps that strength if the party shrinks mid-fight.
- **Evidence:** 426 EditMode and 24 PlayMode tests pass. The built host and guest gate test and the two-copy test pass.

