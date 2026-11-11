# 06: XP, levels, six stats and the level-up UI

**What to build:** Killing enemies grants XP, levels follow a configurable curve, and the player allocates stat points across six stats (Strength, Agility, Vitality, Intelligence, Sense, Luck). A System-style level-up toast and a stat screen make the growth visible.

**Blocked by:** 03: Light attack and one enemy

**Status:** ready-for-agent

- [x] XP gain and the level curve come from data, and the level cap is a configuration value
- [x] Each level grants a few free stat points, and the player can allocate them on a stat screen
- [x] Stats measurably affect the simulation (for example damage, health, speed)
- [x] A level-up toast in the System's voice appears on level-up
- [x] Headless tests cover XP-to-level progression and the cap

## Comments

**2026-11-12 (agent):** Implemented.
- Data: `Assets/Data/Progression.asset` holds the XP curve (94 entries, 50 XP for level 2 rising to about 70,000 for level 95), the level cap (95), 3 free points per level, starting stats of 10 and the stat effects. Each enemy definition carries `XpReward` (Goblin 25). Curve entries must be positive and number at least cap minus 1.
- Rules (simulation): every player in the world gets the full XP for every kill, with no split (stories 70 and 77). Leftover XP carries over, a big gain can grant several levels (one `PlayerLeveledUp` per level), XP past the cap is discarded, and level-up restores full health. Events: `XpGained`, `PlayerLeveledUp`, `StatPointAllocated`.
- Stats: allocation is a command (`PlayerCommand.AllocateStat`, one point per tick, spent before that tick's actions). Strength gives +5% damage per point (exact integer per-mille maths, halves round up), Vitality +10 max health per point (the new health arrives filled; a defeated player's max health still updates), and Agility +2% move speed per point.
- UI: `SystemHud` (IMGUI) shows a status panel (level, HP and XP bars, unspent points), a "[ SYSTEM ] LEVEL UP" announcement, and a stat screen on Tab with + buttons. The cursor is free while it is open; Tab or Escape closes it, and the cursor is re-captured only if it was captured before. No input reaches the game while the cursor is free. It replaced the placeholder `CombatHud`.
- Greybox: three Goblins (75 XP), enough for level 2 in one fight.
- Tests: `SimulationProgressionTest` (XP to level, carry-over, multiple levels, the cap, curve and cap from config, validation, full XP for all players, health restore, allocation, the three stat effects, rounding); `PlayerCommandBufferTest` (allocations queue one per tick); `GreyboxProgressionTest` (PlayMode: clear the Goblins, level 2, announcement shown, points spent through the runner).

Open notes for later tickets:
- **Not checked by a human:** the look of the HUD and stat screen, and the Tab/Escape/cursor flow.
- **Intelligence, Sense, Luck:** no effect yet. They are planned for 07 (skills) and 22 (loot odds); the stat screen marks them "coming soon".
- **25 (rank promotions):** rank bands (E 1-15, D 16-35, ...) are not modelled yet. Add them as configuration next to the curve.
- **Structure:** `SimulationWorld` now holds movement, combat, enemy AI and progression. Split progression (and later skills) into their own types before ticket 07 grows it further.
- **Duplicated defaults:** asset fields and definitions both state the same defaults. This is consistent with the existing pattern; revisit when tuning moves fully into data (07).
