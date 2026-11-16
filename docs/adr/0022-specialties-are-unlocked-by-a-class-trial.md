# 0022: Specialties are a tree, each tier unlocked by a class trial and picked for good

**Status:** Accepted (2026-11-17)

## Context

The spec gives each base class two specialties at B-rank, gated by an authored quest (story 16). It
counts six specialties, from the three base classes of the original plan. ADR 0012 made it four classes.
The developer then asked for an earlier tier at D-rank, with the B specialties branching from the D ones.

The game has no quest system. Missions are gates on the board, and story scenes play once per host world
(ADR 0009). Ticket 53's side quests may want a general quest system later.

The developer chose (2026-11-17):
- the Warrior first;
- a guild trial gate as the quest;
- a permanent pick;
- new skills plus passives as what a specialty changes;
- a D-rank tier as a branching tree;
- the Warrior again for the first D specialty.

## Decision

- **A tree in two tiers.** Each class has two specialties at D (`SpecialtyDefinition.Rank`), and two at B
  under each D one (`SpecialtyDefinition.ParentId`). That makes six per class: **24 in all** with four classes (it was six), and 48 with the eight classes of tickets 55 to 58.
  A hunter ends with one D specialty and one B specialty under it.
- **Each tier's quest is a guild trial gate.** A class names one trial per tier (`ClassDefinition.TrialGateIds`).
  The gate's rank is the tier's (`GateDefinition.Rank`). It has no site on the map, so the guild posts it on
  the board once the host's world reaches that rank (ticket 25). Like every boss gate it has a clear reward
  (both Warrior trials give Deep Rend, the Rending Slash upgrade). A story scene can play on its first clear,
  like any gate's (`StoryTrigger.GateCleared`).
- **Passing a trial:** when a trial is cleared, every hunter present whose class it is passes it, if their own
  rank is at least the gate's. To pass the B trial, a hunter must already have picked their D specialty,
  because the B branches grow from it. Hunters who don't qualify gain nothing from it: other classes, lower
  ranks, or no D pick. A pass belongs to the character, not the world: a guest who passes in a host's world
  keeps it in their own save.
- **The pick:** with a tier's trial passed, the hunter picks one of that tier's specialties. At B, only the two
  under their D pick are offered. **Each pick is permanent**, as with the class (ADR 0012).
  - The pick is a player command (`PlayerCommand.ChooseSpecialtyId`), so the host decides and guests pick
    like the host.
  - A pick is allowed only out of a gate. Anything else (before the trial, a second pick at a tier, a branch
    under the other D specialty, another class's specialty) is ignored.
  - A picker opens when the party is home. "Decide later" closes it, and K opens it again.
- **What a specialty brings:**
  - **Skills:** ordinary skill assets that name the specialty (`SkillDefinition.SpecialtyId`) as well as the
    class. They are learned at their levels, only by hunters with that specialty. Skills the hunter's level
    has already reached are learned when the pick is made.
  - **Passives:** modifiers that are always in effect (`SpecialtyDefinition.Passives`, tags or one skill,
    ticket 08), on top of the hunter's own and their gear's. Both tiers' passives count.
- **A stub:** a specialty with no skills and no passives is shown but cannot be picked, so nobody spends a
  permanent pick on an empty branch.
- **Saves:** the character format moves to version 7. It adds the picks (lowest tier first) and the passed
  trials (by rank). Version 6 still loads, with neither. When a character loads, a pick is dropped if:
  - the game no longer has it,
  - it belongs to another class,
  - it is a second pick at one tier, or
  - its parent was dropped.

  Passed trials stay passed. Snapshots carry the picks and the passed trials.
- **The first path:** the Warrior's Vanguard (D), then Berserker (B). The rest of the Warrior's tree is stubs:
  Sentinel at D, with Champion under Vanguard and Guardian and Bulwark under Sentinel. The names are
  working names.
  - **Trials:** "Warrior's D-rank Trial" (`warrior-trial-d`) and "Warrior's B-rank Trial" (`warrior-trial-b`).
  - **Vanguard:** physical skills deal 10% more damage (Vanguard's Edge). Shield Bash's cooldown is 30% shorter
    and it reaches 1 m further (Advance). Skill: Overhead Smash (level 16), a heavy blow straight ahead.
  - **Berserker:** melee skills deal 20% more damage (Berserker's Fury). Whirlwind's cooldown is 35% shorter
    and it reaches 1 m further (Relentless). Skills: Cleave (level 56), a 220° swing on a 4 s cooldown, and
    Earthsplitter (level 62), a quake that can be charged.

## Building the next specialty

1. **Skills.** Add them to the builder's `SkillData`, or make skill assets by hand.
   - Each skill names its class, and the specialty's id in `specialty`.
   - Unlock levels are at the tier's rank or later: D is level 16 and up, B is 56 and up.
   - A Warrior on the built path knows seven skills against six bar slots. The player chooses which six to
     slot, so keep later trees to about that size.
2. **Passives.** Make modifier assets (`Assets/Data/Modifiers`). Each must also be listed in the content's
   modifiers. A passive targets a tag or one of the class's skills.
3. **The specialty.** On the class asset (`Assets/Data/Classes`), under Specialties, fill in the stub, or add
   the entry: id, name, rank, parent (at B), description and passives. The id is permanent once players
   have it. A B specialty can only be built if the D specialty above it is built.
4. **The trials**, once per class: one gate per tier.
   - Give each gate its tier's rank and a guild notice (a blank "write me" until the developer writes it,
     ADR 0009).
   - The gates have no site on the continent.
   - List them as the class's trial gates.
   - For a story scene, add a text scene on `GateCleared` for that gate.
5. **Check it.** Run **Hunter System > Refresh Content** to list the new assets in the content, then run the
   tests. The content check `EveryClassesSpecialtyTreeHasATrialPerTier` holds every class to this shape.

## Consequences

- The spec's "two specialties per base class at B, six in total" becomes a tree: two at D and four at B per
  class, 24 in all.
- The trials are greybox. They reuse the first gate's kit (goblins, hobgoblins and the Warlord), whose enemies
  are tuned for E-rank. Enemies for D- and B-rank are later content work.
- A trial is a gate, not a quest with steps. If ticket 53 builds a quest system, a trial could become one step
  of a quest without changing how the pick works.
- In co-op, Warriors of the right rank pass together. A party with no Warrior can still run the trial and
  gains nothing.

## Update (2026-11-17, tickets 54 to 58)

- **Every tree is built.** Ticket 54 filled the Warrior's four stubs and the Rogue's, Archer's and Wizard's trees. Tickets 55 to 58
  added the Monk, Cleric, Warlock and Summoner, each with a full tree and its D and B trials. That is 48 specialties, none of them stubs.
- **The trees are data in one place:** `Assets/Editor/GreyboxSceneBuilder.Specialties.cs`. Each specialty's description is worked
  out from its passives' and skills' numbers, so the words match what it does. All names are working names.
- **New mechanics** some specialties use: healing (`SkillDefinition.Healing`: a bolt that heals the first ally it reaches, or an area
  that heals everyone in it), drain life (`LifeStealPercent`), and summons (`SkillEffectKind.Summon`). Sense now raises skills that
  scale with it.

