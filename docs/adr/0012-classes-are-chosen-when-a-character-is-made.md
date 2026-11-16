# 0012: Classes are chosen when a character is made

**Status:** Accepted (2026-11-14)

## Context

The spec puts classes at D-rank: classless through E-rank, then one of three base classes by level
(ticket 24). The developer asked for four basic classes instead (Warrior, Rogue, Archer and Wizard)
and changed when they are chosen. In the developer's words: "fire ball is only for the magic classes
so they pick the classes at the start". The early game is still the same content for everyone. A
Wizard starts weak, with one basic Fireball, and grows into the strongest spells.

Ticket 24 was also blocked by the M3 feel review (ticket 21), which has not happened. The developer
asked for the classes now.

## Decision

- **When:** a class is picked when a character is made, before the first session, and **the choice is
  permanent**. A character saved before classes existed is asked once, the next time it is loaded.
  It then gains the class's stats for every level it has already reached, and the class skills its
  level has reached. Its own placed points stay.
- **Four classes** (the developer's choice), as content data (`Assets/Data/Classes`):

  | Class   | Each level, on top of the 3 free points | Starts with                     | Unlocks                                    |
  |---------|-----------------------------------------|---------------------------------|--------------------------------------------|
  | Warrior | +1 Strength, +1 Vitality                | Rending Slash, Ground Slam      | Shield Bash 5, Whirlwind 10                |
  | Rogue   | +1 Agility, +1 Luck                     | Twin Fangs                      | Shadow Flurry 3, Throwing Knives 5, Eviscerate 10 |
  | Archer  | +1 Agility, +1 Sense                    | Quick Shot, Hunter's Javelin    | Arrow Rain 3, Volley 10                    |
  | Wizard  | +1 Intelligence, +1 Sense               | Fireball (weak: 18 damage)      | Flame Burst 3, Arcane Orb 8, Inferno Wave 15, Monarch's Decree 20 |

  The skills from before classes were split among the classes, and new ones were added so each class
  has its own kit (the developer's choice). Fire Bolt became the Wizard's Fireball. Its id stays
  `fire-bolt`, so saves and modifiers still find it. Hunter's Javelin became the Archer's level-1
  heavy shot: it was a level-5 Strength skill dealing 40, and is now a level-1 Agility skill dealing 30.
  A character from before classes keeps its bar order only for the skills its class keeps; the rest of
  the bar fills as skills are learned. Magic belongs to the Wizard only, which content validation checks. (Changed 2026-11-17, ticket 58: magic belongs to the caster classes, the Wizard, the Warlock and the Cleric.)
- **The jump:** only the Rogue clears an enemy (the developer's choice). Its jump is 450% of the base,
  which is 4.05 m and clears the Warlord's 3.5 m charge (ADR 0011). Content validation checks that the
  Rogue's jump clears the tallest boss and that no other class's jump does.
- **Skills** name their class. A skill with no class is shared and every class learns it (there are
  none yet). A character learns only its own class's skills and the shared ones. Skills from another
  class in an old save are dropped when it loads. Each class's starters must fit the six-slot bar.
- **Saves:** the character format moves to version 2, which adds the class id. Version 1 still loads, as
  a character with no class. A class id the game does not have loads as no class, and the game asks
  for a class again. That character gains no stats for past levels from the new class, because its
  stats already hold the old class's bonuses. Snapshots carry the class by id.
- **In the game:** a picker asks for the class. Offline, the world waits for the pick. In the co-op
  menu, hosting and joining wait for it. A Steam invite joins once the class is picked. Automated runs
  (`-coop`) play `-class ID` or the first class without asking.
- **Moving the old skill assets.** The scene builder never overwrites an existing asset, with one
  exception made here. A skill asset from before classes (it has no class) is moved onto its class
  once. Fire Bolt and Hunter's Javelin are also retuned to the values above. After that, the
  Inspector's values stand as before.
- **Every class must solo the first gate.** The solo playthrough test runs once per class, and all
  four clear the gate and beat the Warlord.

## Consequences

- The spec's "classless through E-rank" and "three classes at D-rank" are replaced for now. The
  specialties at B-rank (ticket 26) branch from these four classes. The hidden class is unchanged.
- The numbers are first guesses. The feel review (ticket 21) is the place to tune them.
- The debug arena's "Teach all skills" still teaches every class's skills, for testing.
