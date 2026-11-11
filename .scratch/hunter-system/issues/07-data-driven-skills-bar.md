# 07: Data-driven skills with the six-slot bar

**What to build:** Skills are defined as data with cooldowns. The player has a six-slot skill bar on keys 1 to 6, three starter skills are usable, and new skills unlock at set levels with a System prompt. Skill definitions carry a tags field for later modifier work.

**Blocked by:** 05: Camera, keyboard and mouse controls, soft lock-on, 06: XP, levels, six stats and the level-up UI

**Status:** ready-for-agent

- [x] Skills load from data definitions with a name, cooldown, effect, and tags
- [x] Skills are used from keys 1 to 6, governed only by cooldowns (no mana or stamina)
- [x] Three starter skills work in combat
- [x] A skill unlocks at a defined level and triggers an unlock prompt
- [x] Headless tests cover cooldown behavior and unlock-at-level

## Comments

**2026-11-12 (agent):** Implemented. Preceded by a no-behaviour-change refactor that split `SimulationWorld` into topic files (68f575c).
- Data: `SkillDefinition` (id, name, cooldown, unlock level, tags, scaling stat, effect: Cone, Area or Projectile, plus numbers) authored as `SkillDefinitionAsset`s in `Assets/Data/Skills/`. The world validates every skill at start: unique ids, names, sane numbers, an unlock level within the cap, and no more starters than slots.
- Bar: six slots on keys 1 to 6 (`Player/Skill1`-`Skill6`). Starters fill the bar in data order. Each skill has only its own cooldown: no mana, no stamina, no shared cooldown. A skill takes the tick (no attack with it), and skills are blocked mid-dodge, mid-heavy-windup and while defeated. Cone and projectile skills use soft lock-on.
- Starters: Rending Slash (cone, 30, 4 s), Ground Slam (area, 20, 6 s), Fire Bolt (projectile, 25, 2 s, scales with Intelligence, which now has an effect). Flame Burst (fire area, 35, 8 s) unlocks at level 3 into the first free slot with a `SkillUnlocked` event and a "[ SYSTEM ] NEW SKILL ACQUIRED" prompt. Announcements queue, so a level-up and an unlock play back to back.
- Projectiles fly from the tick after launch, hit the first live enemy along their path, and fizzle at range. Their damage, speed, range and radius are fixed at launch.
- One damage path (`DamageEnemy`) now serves attacks, skills and projectiles, and `DamageDealt.Skill` says which skill dealt it.
- Tests: `SimulationSkillsTest` (loading, effects, cooldowns, unlock at level including multi-level jumps and a full bar, blocking, validation, launch-time numbers); `AnnouncementQueueTest`; `PlayerCommandBufferTest` (skill latch); `GreyboxSkillsTest` (PlayMode: assets fill the bar, a Fire Bolt is visible in flight).

Open notes for later tickets:
- **Not checked by a human:** keys 1 to 6, the skill bar's look and the unlock prompt. The Greybox gives 75 XP, so level 3 is not reachable there; ticket 09 (test arena) should allow it.
- **08 (tag modifiers):** tags are free-form strings, so a typo matches nothing; consider a known-tags list. There is no per-player "resolved skill" step yet: modifiers need one, rather than mutating the shared `SkillDefinition`. Light and heavy attacks carry no tags, so a "melee" modifier cannot reach them yet.
- **Loadout:** a skill learned with a full bar is in `PlayerState.KnownSkills` but cannot be slotted. A rearrange-slots command and screen are needed once more than six skills exist.
- **Small:** dodge and a skill pressed on the same tick drop the skill (dodge wins). An enemy crossing a projectile's path between ticks can slip through, which is negligible at current speeds.
