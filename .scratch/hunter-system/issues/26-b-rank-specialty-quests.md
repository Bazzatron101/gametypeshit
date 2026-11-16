# 26: B-rank specialty quests

**What to build:** One quest-gated specialty branch at B-rank, built as a template for the remaining five.

**Blocked by:** 24: Base classes at D-rank, 25: Rank promotions and map opening

**Status:** ready-for-human (write the trial notices and scenes; confirm or rename the working names)

- [x] A B-rank quest unlocks one specialty for one base class
- [x] The specialty adds skills or modifiers that change the play style
- [x] The structure is documented so the other five can be built the same way (now 22 more: see the comments)

## Comments

**2026-11-17 (developer, recorded by agent):** Decided before building:
- **Count:** four base classes with two specialties each is **eight** specialties, not the spec's six (ADR 0012 added the fourth class). This ticket builds one; the template is for the other seven.
- **First class:** the Warrior.
- **The quest:** a guild trial gate. At B-rank the guild posts the class's trial on the gate board, and clearing it unlocks the specialty choice. There is no general quest system yet (ticket 53's side quests may want one).
- **Choosing:** pick one of the class's two specialties, and the pick is permanent, as with the base class (ADR 0012). Only one branch gets content here; the other is a stub.
- **The payoff:** new specialty skills plus passive modifiers on the class's skills (tags, ticket 08).

**2026-11-17 (developer, recorded by agent):** Asked for another specialty tier at D-rank, and chose:
- **A branching tree:** at D-rank each class passes a D trial and picks one of two D specialties. At B-rank it passes the B trial and picks one of the two B specialties that branch from its D pick. That is six specialties per class (two at D, four at B), 24 in all.
- **The first D specialty is the Warrior's,** so one class shows both tiers working together.

**2026-11-17 (agent):** Built. The rules and how to build the rest are in `docs/adr/0022-specialties-are-unlocked-by-a-class-trial.md`. The spec's class line now describes the tree.

What was built:
- **The tree:** two specialties at D and two at B under each D one (`SpecialtyDefinition.Rank`, `ParentId`), listed on the class asset. That is six per class and 24 in all. The Warrior's tree is Vanguard and Sentinel at D, Berserker and Champion under Vanguard, and Guardian and Bulwark under Sentinel. **The built path is Vanguard (D) then Berserker (B)**; the other four are stubs, shown but not pickable. All six names are working names.
- **The quests are trial gates,** one per tier: "Warrior's D-rank Trial" and "Warrior's B-rank Trial". They have no site on the map, so the guild posts each on the board when the host's world reaches its rank. A clear passes the trial for every Warrior present at that rank. The B trial also needs the D pick, because B branches grow from it. Each trial gives Deep Rend as its clear reward, as every boss gate must give a reward, and has a clear scene with three blank lines.
- **The pick** is permanent and is a player command, so guests pick like the host. It works only out of a gate, and at B only the two branches under the D pick are offered. A picker opens when the party is home; "Decide later" closes it and K reopens it. The test arena (F1) has a "Pass the next trial" button.
- **Vanguard:** physical skills +10% damage; Shield Bash's cooldown 30% shorter and 1 m more reach; new skill Overhead Smash (level 16).
- **Berserker:** melee skills +20% damage; Whirlwind's cooldown 35% shorter and 1 m more reach; new skills Cleave (level 56) and Earthsplitter (level 62, can be charged).
- **Saves:** the character format moves to version 7, with the picks and the passed trials; version 6 still loads. Snapshots carry both, and passes and picks belong to the character, so a guest takes them home.

Evidence:
- `SpecialtyTest` (24 tests): each trial is posted at its rank; a clear unlocks the D pick; the B trial needs the D pick; at B only the branches under the D pick are offered; rank and class are checked; a guest passes in the host's world; other gates are no trial; skills come at their levels; passives change damage (+20% measured); picks are permanent; disallowed picks are ignored; stubs can't be picked; saves, old saves, dropped picks and snapshots; the command crosses the wire; content rules.
- `EveryClassesSpecialtyTreeHasATrialPerTier` checks the real content's shape. The codec test covers the two new events.
- **721 EditMode and 40 PlayMode tests pass.**

Not done here:
- **Words:** the two trial notices and six scene lines are blank markers. The writing list goes from 19 to 27 slots.
- **The trials are greybox,** made from the first gate's E-rank goblins and Warlord. There are no D- or B-rank enemies yet.
- **The other 22 specialties:** four Warrior stubs and every other class's tree. ADR 0022 lists the steps.
- **Seven skills, six slots:** a Warrior on the built path knows seven skills, so the player chooses which six to slot.

