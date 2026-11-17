# 62: New enemy types and enemy races

**What to build:** More kinds of enemies to fight. From the developer, 2026-11-17: "can we add new enemy types and new enemy races into the game". Two parts:

- **Enemy types (how an enemy fights):** today every enemy walks up and swings. The types differ only in their numbers (Goblin is quick and weak, Hobgoblin is slow and hits hard). New types should *play* differently, for example an archer that keeps its distance and shoots, a fast skirmisher that darts in and out, a shielded brute, or a caster that heals or buffs its allies.
- **Enemy races (what an enemy is):** today there is one race, goblins. A race is a family of enemies with a shared look and theme (for example undead, beasts, cultists), and each race fields several types (an undead archer, an undead brute, and so on). Gates would draw their enemies from one race, or a few.

**Blocked by:** none (builds on 03: Light attack one enemy, 18: Boss and clear reward, 27: Party-size enemy scaling, all done)

**Status:** split into 63-70 (this ticket is done when they are)

**Child tickets:**
- 63: Enemy races as data, with weaknesses
- 64: Goblin archer (ranged)
- 65: Wolf (fast skirmisher)
- 66: Armoured boar (shielded brute)
- 67: Goblin shaman (healer/buffer)
- 68: Elite enemies
- 69: Beast gates
- 70: Beast boss (needs triage: its attacks)

- [ ] Four new enemy behaviours: ranged archer, fast skirmisher, shielded brute, healer/buffer
- [ ] One new race, Beasts, alongside the Goblins (who become the first race); each race fields its own signature types, not every type
- [ ] Elites: a tougher, named version of a type that turns up now and then
- [ ] Weaknesses: each race takes more or less damage from certain tags (for example fire, holy)
- [ ] Beasts get their own gate(s) and their own boss
- [ ] A race is data (`EnemyDefinitionAsset` names its race; races are their own assets), not code
- [ ] Each type's behaviour and its numbers are data on the enemy definition, not code constants
- [ ] Every new attack is telegraphed, so the player can dodge it like today's windup (ticket 04)
- [ ] Gates can say which race(s) they spawn; existing goblin gates keep working
- [ ] Party scaling (ticket 27), snapshots and co-op (host decides; snapshots carry new enemy state) and summons (ticket 57) all work with the new types
- [ ] Bot teammates (tickets 33, 59) fight the new types sensibly (for example they go after archers)
- [ ] New looks are placeholders labelled "AI" (memory rule), and the content check holds every new enemy and race to the rules
- [ ] Tests cover each new behaviour, its telegraph and dodge, and snapshots mid-attack

## Notes

**What exists today:**
- `EnemyDefinition` (`Assets/Scripts/Simulation/Definitions.cs`) is a stat block: health, speed, stop distance, one melee attack (damage, range, windup, cooldown), XP, height, and an optional `Boss`.
- Only three enemy assets exist: Goblin, Hobgoblin, Shield Crystal (`Assets/Data/Enemies/`).
- `EnemyView` shows any enemy as a greybox body that glows on windup and flashes on hit. There is no per-race look yet.
- Player projectiles already exist in the simulation, so a ranged enemy can likely reuse them.

**To settle in triage:**
- **Which races:** which ones, and how many for a first pass (for example undead, beasts, and one more)? Do goblins count as the first race?
- **Which types:** which behaviours first? Candidates: ranged archer, fast skirmisher, shielded/armoured brute, healer or buffer, exploder, summoner of small adds.
- **Mix and match:** does every race have every type, or does each race have its own signature types (beasts have no archers)?
- **Elites:** should a race also have an elite version of a type (spec story 26 asks for "smart enemies with elites and bosses")?
- **Weaknesses:** should races resist or take extra damage from tags such as fire or holy (the tag system from ticket 08 could drive this)?
- **Where they live:** which gates and overworld areas get which race? Does each race get its own boss later?
- Probably too big for one ticket: consider splitting into (a) races as data plus looks, (b) one ticket per new behaviour type, (c) placing races in gates.

## Comments

**2026-11-17 (developer, recorded by agent):** Decided in triage:
- **Races:** one new race for the first pass, **Beasts**. Goblins count as the first race.
- **Types:** all four behaviours: ranged archer, fast skirmisher, shielded brute, healer/buffer.
- **Mix:** signature types. Each race fields only the types that fit it, so races feel different.
- **Extras:** all four: elites, weaknesses by tag, each race gets its own gates, and each race gets its own boss.

Agent's suggested split of types between races (change it if it doesn't feel right):
- **Goblins:** archer (goblin archer) and healer/buffer (goblin shaman), next to today's Goblin and Hobgoblin.
- **Beasts:** skirmisher (wolf, darts in and out, hunts in packs) and shielded brute (armoured boar, its hide blocks hits from the front). A spitting spider could be a beast's ranged attack later.
- **Weaknesses:** Beasts take extra fire damage; Goblins take extra holy damage. Numbers to tune in playtest.
- **Beast boss:** for example a great alpha wolf that calls in its pack; its design is its own ticket.

Suggested order, each as its own ticket: (1) races as data plus weaknesses, (2) one ticket per behaviour (archer, skirmisher, brute, healer), (3) elites, (4) Beast gates, (5) the Beast boss.
