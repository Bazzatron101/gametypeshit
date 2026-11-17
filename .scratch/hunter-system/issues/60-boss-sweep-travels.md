# 60: The Warlord's sweep travels around its arc

**What to build:** Make the Goblin Warlord's sweep move. Today the whole marked arc is hit at once. Instead, the hit should travel around the boss through 180°, from one end of the arc to the other, so a player at the far end sees it coming and can still dodge, jump or move out before it reaches them. From the developer's playtest, 2026-11-17: the boss "felt fun to play but I think we need some sweeping attacks".

**Blocked by:** 18: Boss and clear reward (done)

**Status:** ready-for-agent

- [ ] The sweep covers 180° round the boss, and its hit travels from one end of the arc to the other over time
- [ ] A player is hit only when the travelling hit passes where they stand; one at the far end can dodge or step out before it arrives (jumping does not clear it)
- [ ] The floor marking shows where the sweep starts, which way it goes, and where the hit is now
- [ ] The sweep's speed and arc are data on the boss (`BossDefinition`), not code constants
- [ ] It works in co-op (host decides the hits; snapshots carry the sweep's progress) and still hits summons (ticket 57)
- [ ] Tests cover the travelling hit, the dodge at the far end, and snapshots mid-sweep

## Notes

The developer: "have the damage point move around the boss in 180 degrees so you can still dodge the move if you're at the end of the arc".

## Comments

**2026-11-17 (developer, recorded by agent):** Decided in triage:
- **Speed:** the hit takes **1 second** to travel the full 180° (starting value, as data on the boss).
- **Direction:** **always the same way** round the boss, so it can be learned. Which way is data on the boss.
- **Jumping does not clear it:** it is a high swing. Only the dodge's invulnerability or getting out of the arc avoids it. (The slam's shockwave stays jumpable.)
