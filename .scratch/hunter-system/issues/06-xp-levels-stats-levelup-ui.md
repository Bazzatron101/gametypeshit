# 06: XP, levels, six stats and the level-up UI

**What to build:** Killing enemies grants XP, levels follow a configurable curve, and the player allocates stat points across six stats (Strength, Agility, Vitality, Intelligence, Sense, Luck). A System-style level-up toast and a stat screen make the growth visible.

**Blocked by:** 03: Light attack and one enemy

**Status:** ready-for-agent

- [ ] XP gain and the level curve come from data, and the level cap is a configuration value
- [ ] Each level grants a few free stat points, and the player can allocate them on a stat screen
- [ ] Stats measurably affect the simulation (for example damage, health, speed)
- [ ] A level-up toast in the System's voice appears on level-up
- [ ] Headless tests cover XP-to-level progression and the cap
