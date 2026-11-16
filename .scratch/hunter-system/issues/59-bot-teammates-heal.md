# 59: Bot teammates heal hurt allies

**What to build:** Bot teammates (ticket 33, ADR 0014) use their healing skills (ticket 56). Today a bot Cleric fights with its smites but never casts a heal: `CompanionBrain` treats a heal as hitting nothing. A bot with a heal should cast it when someone near is hurt:
- **A heal bolt** (`Effect == Projectile`, `Healing > 0`) is aimed at the most hurt living ally in range. It flies to the first ally along its path, so the bot picks an aim whose first ally is one that needs it.
- **A heal area** (`Effect == Area`) is cast when hurt allies, or the bot itself, are within its radius.

**Blocked by:** 56: The Cleric, a support class that heals

**Status:** ready-for-human (play a gate beside a bot Cleric)

- [x] A bot with a heal bolt heals a hurt ally, and never wastes it on someone at full health
- [x] The heal bolt goes to the most hurt ally in range
- [x] A bot with a heal area casts it when it or an ally in the area is hurt
- [x] A bot Cleric from the real content heals a hurt ally in a gate

## Comments

**2026-11-17 (agent):** Opened as the follow-up ticket 56 left: bots skipped healing skills.

**2026-11-17 (agent):** Built in `CompanionBrain` (`TryHeal`). A bot checks its heals right after dodging and before it attacks, so it heals in a fight and after one.
- **Heal bolt:** aimed at the most hurt standing ally within 90% of its range (a rookie picks the nearest). It fires only if the first ally along that path needs healing, so a bolt is never spent on someone at full health who stands in the way.
- **Heal area:** cast when the bot or an ally within 90% of the radius is hurt.
- **"Hurt":** a rookie heals any missing health and thinks about it every 40 ticks or so. A veteran thinks every tick and waits until about 60% of the heal's base amount is missing.
- Not done: a bot does not walk toward a hurt ally who is out of range, and it never charges a heal.

Evidence: `CompanionHealingTest` (6 tests: heals a hurt ally with the bolt; heals nobody at full health; the most hurt ally in range first; never fires through an ally who is fine; the area for itself and an ally inside it, not one outside; a real-content bot Cleric heals a hurt Warrior in the first gate). **789 EditMode tests pass** (`scripts/test.ps1`), the `CompanionTest` gate playthroughs among them.
