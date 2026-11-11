# 07: Data-driven skills with the six-slot bar

**What to build:** Skills are defined as data with cooldowns. The player has a six-slot skill bar on keys 1 to 6, three starter skills are usable, and new skills unlock at set levels with a System prompt. Skill definitions carry a tags field for later modifier work.

**Blocked by:** 05: Camera, keyboard and mouse controls, soft lock-on, 06: XP, levels, six stats and the level-up UI

**Status:** ready-for-agent

- [ ] Skills load from data definitions with a name, cooldown, effect, and tags
- [ ] Skills are used from keys 1 to 6, governed only by cooldowns (no mana or stamina)
- [ ] Three starter skills work in combat
- [ ] A skill unlocks at a defined level and triggers an unlock prompt
- [ ] Headless tests cover cooldown behavior and unlock-at-level
