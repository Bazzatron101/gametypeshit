# 46: Weapons that attack differently

**What to build:** A sword, a bow and a staff. The weapon you wear decides what your light and heavy attacks do, so each one plays differently. (The developer, 2026-11-15: "start work making some weapons like a sword bow and staff and have them all attack differently".)

**Blocked by:** 23: Gear and tag synergies

**Status:** done

- [x] A worn weapon replaces the default light and heavy attacks
- [x] Sword, bow and staff each attack in their own way
- [x] Weapon attacks carry tags, so gear and modifiers reach them
- [x] Warriors start with the sword, Archers with the bow, Wizards with the staff
- [x] Headless tests cover each weapon's attacks (WeaponTest)

## Comments

**2026-11-15 (developer, recorded by agent):** Decided:
- **The weapons** (agent's proposal, accepted):
  - **Sword:** wide, quick slashes; the heavy attack is a long thrust.
  - **Bow:** arrows; the heavy attack is a slow drawn shot that pierces a line of enemies.
  - **Staff:** magic bolts; the heavy attack is an arcane blast around you.
- **Any class can use any weapon.** The sword scales with Strength, the bow with Agility and the staff with Intelligence.
- **Starting weapons:** Warrior the sword, Archer the bow, Wizard the staff. This includes existing characters whose weapon slot is empty. The Rogue keeps the default attack until it gets its own weapon.
- **Timing:** wait until the session working on the overworld (tickets 37 to 45) has committed. It is changing `SimulationWorld.cs` and `SimulationConfig.cs`, which weapons need too.

**2026-11-15 (agent):** Plan:
- A weapon is gear in the weapon slot with a light and a heavy attack. Each attack is a skill-shaped hit (cone, area or projectile, with damage, reach, tags, scaling stat and cooldown) plus a windup. Weapon attacks then go through the same path as skills: modifiers, projectiles, bursts, snapshots. Light and heavy attacks get tags this way (the gap noted on ticket 08).
- Projectiles can pierce: they hit every enemy along their path, once each.
- `AttackPerformed` names the weapon's hit, so the view can flash its shape. The swing marker is shown only for cone attacks.
- Bot teammates fight at their weapon's reach.

**2026-11-15 (agent):** Built, after the overworld session committed. The rules are in `docs/adr/0018-the-weapon-decides-the-attacks.md` (proposed).

What was built:
- **Weapon attacks:** `WeaponAttack` (a skill-shaped hit plus a windup) on `GearDefinition.LightAttack`/`HeavyAttack`. Light and heavy attacks use the worn weapon's, else the default. The hit goes through the skills' path (`PerformHit`, split out of `FireSkill`), so modifiers, extra bolts and bursts reach weapon attacks.
- **Piercing projectiles:** `SkillDefinition.Pierces`.
- **Lookups:** `SimulationConfig.FindSkill` finds skills and weapon hits by id, for modifiers aimed at one skill, snapshots and events. A hit's id may not clash with a skill's.
- **Events:** `AttackPerformed` names the weapon's hit, with its reach and radius.
- **Starting weapons:** `ClassDefinition.StartingWeaponId`, issued to a character with no weapon at all, worn or in the bag.
- **Content:** Iron Sword, Hunting Bow, Apprentice Staff (`Assets/Data/Gear/`), set as the Warrior's, Archer's and Wizard's starting weapons. The Cinder Staff and Goblin Cleaver now have attacks too.
- **On screen:** `SkillEffectsView` flashes a weapon's slash or blast. `PlayerView` shows its swing marker only for cone attacks.
- **Bots:** `CompanionBrain` attacks at the worn weapon's reach.

Evidence:
- `WeaponTest` (16 tests): the sword slashes wide and short, and thrusts far and narrow after a windup; an arrow stops at the first enemy; the drawn shot pierces a line of four; the staff's bolt, and its blast hitting in front, behind and beside; the default attacks without a weapon; Strength and Intelligence scaling; tag modifiers and extra bolts on weapon attacks; a dodge cancels a weapon's windup; an arrow in flight survives a snapshot; the event over the wire; starting weapons (new and existing characters, and the Rogue without one); validation; an Archer teammate shooting from range without closing in.
- `pwsh scripts/test.ps1` (EditMode): 561 passed, 0 failed. PlayMode was not run.

Not done:
- **The Rogue has no weapon** yet (daggers were offered and not chosen for now).
- **Not checked by a person:** how each weapon feels, the flashes, and aiming the bow with the camera.

**2026-11-15 (developer, recorded by agent):** Agility should raise damage ("Yes, add it").

**2026-11-15 (agent):** Done. `StatEffectsDefinition.DamagePerAgility` is +5% per point, like Strength and Intelligence (`Progression.asset`, `damagePerAgility`). It applies to everything that scales with Agility: the bow, and the Archer's and Rogue's skills, which until now gained nothing from it.
- `WeaponTest` checks that the bow hits harder with Agility than with Strength.
- `CompanionTest.ExperiencedTeammatesPlayBetterThanRookies` failed: in its single fight, the rookie tank held the boss 88% of the time. With Agility off, the same fight gave 71%, and the elites gave 79% either way. Across other dice, rookies range from 13% to 88%, so the test now plays four fights each and compares the totals: elites 73%, rookies 61%. The claim is the same, measured less noisily.
- `pwsh scripts/test.ps1` (EditMode): 561 passed, 0 failed.
- **To watch:** Agility now gives both damage and move speed, so the Archer and the Rogue may feel strong.
