# 0017: Gear is worn from a bag, and its tags count

**Status:** Proposed (2026-11-15)

## Context

Ticket 23 asks for gear that can be equipped, and whose tags combine with skills through the
modifier system (ticket 08) to make "a demonstrably broken build". In the M3 review (ticket 21) the
developer asked for equipment so that characters feel different. The spec says broken builds come
"from tag-based item synergies and skill modifiers, not raw stat inflation". The developer chose
six slots: weapon, head, body, hands, feet, trinket. Loot pools, personal draws and duplicates
already existed (ADR 0007, ADR 0016), but a pool could only hold modifiers.

## Decision

- **Loot is items.** Modifiers and gear share one base (`LootDefinition`: id, name, description,
  sell value). A gate's pool holds both, and the draw, the no-repeats rule and duplicates work the
  same for each. Owning gear means having it in the bag.
- **A piece of gear** (`GearDefinition`) has a slot, tags, and modifiers that work only while it is
  worn. Its modifiers are ordinary modifiers, aimed at a tag or at one skill, written into the piece
  itself rather than as separate modifier assets.
- **The bag and the slots.** A character keeps every piece it finds in its bag and wears at most one
  per slot. Wearing a piece puts what was in its slot back in the bag. A defeated player cannot
  change gear. Otherwise it can be changed anywhere, mid-gate too: changing gear is a command, so
  the host decides. A piece from a gate goes on at once if its slot is empty.
- **Worn gear is read, not granted.** When a skill is used, the player's own modifiers and the
  modifiers of the gear they wear are combined in the same way. Gear does not go through
  `GrantModifier`, so a save never mixes the two and taking a piece off cannot leave its modifier
  behind.
- **Tags count.** A modifier can name a tag in `PerWornTag`: it then counts once for every worn piece
  with that tag, and not at all with none. Every number scales, including extra bolts and the
  burst's share; the burst's radius does not. This works on gear and on modifiers from gates. It is
  how pieces build on each other, so sets come from tags rather than from a list of set bonuses.
- **The documented broken build** is the Cinder set on a Wizard's Fireball. The Cinder Staff fans
  three bolts. The Robe and Boots make every hit burst. The Hood (damage), Gloves (cooldown) and
  Boots (burst damage) count every fire piece worn, and the Ember Heart adds damage. In the
  headless test, a level-1 Fireball fired for ten seconds into a pack of six does 90 damage bare
  and 5,168 with the set: 57 times as much. The six pieces worn one at a time add 458 between them,
  so the set does about eleven times what its parts do separately.
- **What drops now.** The Goblin Warren's pool gains four pieces, one for each kind of role: the
  Goblin Cleaver and Warlord's Plate (melee and physical), the Hunter's Quiver (projectile) and the
  Ember Heart (fire). The rest of the Cinder set is for later gates. Until then the test arena hands
  out every piece.
- **Saves and snapshots.** Character format 4 adds the bag and what is worn (by id, like skills),
  and the worn-tag field on modifiers. Older saves load with an empty bag. A piece the game no longer
  has is dropped, and a piece is only worn in its own slot. Snapshots carry the bag and the slots,
  and events name gear by id. Every piece must be in the game's gear list, and a gate can only drop
  pieces from it.
- **Bot teammates** wear what suits their role: the front line wants melee and physical, the Archer
  projectile and physical, the Wizard magic, fire and area. A piece scores one point for each wanted
  tag it carries and one for each of its modifiers aimed at one. A teammate wears the best piece for
  an empty slot, swaps only for a better one, and keeps the first one found on a tie.
- **On screen:** I opens the gear menu anywhere: the six slots, and the bag with Wear and Take off.
  The arena panel can give every piece or wear one. Showing gear on the character is ticket 34.

## Consequences

- Gear is strong on purpose. The spec wants broken endgame builds, and the pools stay small. If
  sets are too strong too early, the answer is where pieces drop, not weaker sets.
- Light and heavy attacks have no tags, so melee gear reaches only melee skills. Giving
  `AttackDefinition` tags is a later step.
- The F5 to F8 modifier toggles in the greybox (`DebugModifierToggles`) are still there. They can go
  once the developer has tried gear in the game.
- Gold still buys nothing. Duplicates of gear sell like any other duplicate.
- Nothing has been checked by a person in the game yet: the gear menu, the reward notice, the arena
  buttons, or how the Cinder set feels.
