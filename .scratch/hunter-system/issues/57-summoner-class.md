# 57: The Summoner, a class that calls creatures

**What to build:** A Summoner base class that calls creatures to fight, with its own skills, weapon, stats per level and a full specialty tree with D and B trials (ADR 0022). Nothing can be summoned yet (the `summon` tag exists, but no effect uses it), so this ticket builds summons.

**Blocked by:** 54: Fill the four classes' specialty trees

**Status:** ready-for-human (play and rename: working names; trial words blank)

- [x] A skill can summon a creature that fights for its summoner and then goes
- [x] Summons work in co-op: they are in snapshots, and enemies and party scaling treat them sensibly
- [x] The Summoner can be picked when a character is made, with its own skills, stats per level and weapon
- [x] The Summoner has a full tree of six specialties and its two trials
- [x] The Summoner solos the first gate, like every class (ADR 0012)

## Notes

**To settle before building:**
- **How long a summon lasts:** a fixed time, until it falls, or both? How many at once?
- **What a summon does:** attack the nearest enemy, the summoner's target, or draw enemies away (a decoy)?
- **Can enemies attack summons?**
- **What scales them:** Intelligence or Sense?

## Comments

**2026-11-17 (developer, recorded by agent):** Chose the Monk, Cleric and Summoner as new classes (tickets 55 to 57).

**2026-11-17 (developer, recorded by agent):** Decided:
- **Summons are timed and attack the nearest enemy.** Each lasts a set time or until it falls, enemies can hit it, and a summoner has at most three at once.
- **Intelligence and Sense each level:** Intelligence raises summons' damage, and Sense raises their health.

**2026-11-17 (agent):** Built. The Summoner gains +1 Intelligence and +1 Sense each level.
- **Summons** (`SkillEffectKind.Summon`, `SimulationWorld.Summons.cs`):
  - A summon skill calls creatures beside the summoner. Each lasts a set time or until it falls, and goes for the nearest enemy within 20 m of its summoner; with none, it keeps by the summoner.
  - It hits for the skill's damage, raised by Intelligence. Its health is raised by Sense. Kills and XP are the summoner's.
  - A summoner keeps at most three; calling another sends the oldest away.
  - Ordinary enemies take on whichever is nearer, a hunter or a summon. A boss's sweeps and slams hit summons standing in them.
  - Summons go when a gate starts, starts again or ends. They are in snapshots, with three new events, and are drawn as greybox capsules in their skill's colour (`SummonView`).
- **Weapon:** Binding Staff, with a placeholder look labelled "AI".
- **Skills:** Call Imp and Spirit Lash from the start; Call Wolves (3, two wolves), Soul Burst (5), Call Golem (10), Call Wraith (15, hits from range).
- **Tree:** Beastcaller and Spiritbinder at D; Packmaster and Golemancer under Beastcaller; Necromancer and Soulweaver under Spiritbinder.
- **Bots:** a bot Summoner calls its summons when an enemy is within 10 m.

Evidence: `SummonTest` (10 tests: appearing, the nearest enemy and the summoner's kill, lifetime, the cap of three, enemies felling a summon, Sense, snapshots, gates, content rules, the wire), and the solo-gate playthrough passes for the Summoner. **779 EditMode and 40 PlayMode tests pass**, and the solo-gate playthrough passes for all eight classes.

Not done here:
- A boss's charge does not hit summons; only its sweeps and slams do. Enemies tied to a gate's home never go for summons.
- Summons are capsules, not creatures.
