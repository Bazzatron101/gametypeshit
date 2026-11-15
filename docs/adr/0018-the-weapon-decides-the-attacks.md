# 0018: The weapon decides the attacks

**Status:** Proposed (2026-11-15)

## Context

The developer asked for "some weapons like a sword bow and staff and have them all attack
differently" (ticket 46). Until now every player had the same light and heavy attacks: a cone in
front, from the config, with no tags, so gear and modifiers could not reach them (noted on ticket
08 and in ADR 0017). The developer accepted the agent's proposal for how each weapon attacks, and
chose starting weapons for the classes.

## Decision

- **A weapon's attacks replace the default ones while it is worn.** A weapon is gear in the weapon
  slot (ADR 0017) with a light and a heavy attack. Either may be left out to keep the default. Only
  weapons have attacks.
- **An attack is a skill-shaped hit plus a windup.** The hit is a `SkillDefinition`: a cone, an area
  round the player or a projectile, with damage, reach, tags, a scaling stat and a cooldown. It goes
  through the same path as skills, so modifiers reach it by tag or by its id, and extra bolts and
  bursts apply. It is never learned or put on the bar. Its id must not clash with a skill's, because
  snapshots and events name hits by id. The windup belongs to the attack, and a dodge cancels it, as
  before. Light and heavy attacks keep their own cooldowns.
- **Aim.** A weapon's hit aims as a skill does (ADR 0013): exactly where the camera looks, and with
  soft lock-on when there is no aim (bots).
- **Projectiles can pierce:** a piercing one flies on to its full range, hitting every enemy along
  its path once.
- **The weapons** (agent's proposal, accepted):

  | Weapon | Light | Heavy | Scales with |
  | --- | --- | --- | --- |
  | Iron Sword | a wide slash (2.4 m, 150°) | a long, narrow thrust (3.6 m, 35°) after 0.3 s | Strength |
  | Hunting Bow | an arrow (16 m) | a drawn shot after 0.5 s that pierces a whole line (22 m) | Agility |
  | Apprentice Staff | a magic bolt (11 m) | an arcane blast all round (3.2 m) after 0.4 s | Intelligence |

  The Cinder Staff attacks like a staff, with fire tags, so the Cinder set boosts its attacks too.
  The Goblin Cleaver attacks like a slower, heavier sword.
- **Any class can use any weapon.** The scaling stat is what favours one class over another.
- **Starting weapons** (developer's choice): the Warrior is issued the sword, the Archer the bow and
  the Wizard the staff. A character gets one whenever it has no weapon at all, worn or in the bag,
  so existing characters get theirs too. A character that took its weapon off but still carries
  one is left alone. The Rogue keeps the default attack until it has its own weapon.
- **On screen:** a weapon's slash or blast flashes like a skill. Arrows and bolts are drawn in flight.
  The old swing marker shows only for cone attacks.
- **Bot teammates** attack at their weapon's reach: an Archer shoots from range instead of walking
  up to swing.

## Consequences

- **Agility now raises damage** (developer's choice): +5% per point, like Strength and Intelligence,
  for everything that scales with it. That is the bow and the Archer's and Rogue's skills, which had
  scaled with Agility without gaining anything. Agility also still raises move speed, so it does two
  things where Strength and Intelligence do one. Watch the Archer and the Rogue for being too strong.
- With Agility damage, one bot fight was no longer enough to show that experienced teammates' tank
  holds the boss more than rookies' (it varied between 13% and 88% for rookies). The test now plays
  four fights each: elites' tank is nearest the boss 73% of the time, rookies' 61%.
- A weapon dropped from a gate changes how its finder fights, not only how hard they hit.
- Nothing has been checked by a person in the game yet: how each weapon feels, the flashes, and the
  bow's reach against the camera's aim.
