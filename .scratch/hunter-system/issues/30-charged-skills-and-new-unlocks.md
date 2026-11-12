# 30: Charged skills and new level-gated skills

**What to build:** Some skills get a charged version: holding the key builds a charge that makes the skill bigger or stronger when released. A perfect dodge while charging launches the skill at full charge at the attacker. Four new skills unlock at spread-out levels, and the charged version of a skill can unlock later than the skill itself.

**Blocked by:** 07: Data-driven skills with the six-slot bar, 09: Test arena

**Status:** ready-for-agent

Decisions (from the developer, 2026-11-13):
- Charging scales with hold time: a tap fires the normal skill; holding builds charge up to the skill's maximum; releasing fires with a bonus proportional to the charge.
- While charging, the player walks slowly and cannot attack or use other skills.
- Perfect dodge: if an enemy hit lands inside the dodge's invulnerability window while a skill is charging, the skill fires immediately at full charge, aimed at that enemy.
- Charge is set per skill in data: it grows the size (area radius, cone reach, projectile hit radius) or the damage. Some skills have no charge.
- Each skill's charge unlocks at its own level (later than the skill), with a System prompt. Before that, the skill only taps.
- Four new skills unlock at levels 5, 10, 15 and 20.

- [x] Skill data carries charge time, size and damage bonuses, and a charge unlock level; it is validated
- [x] Holding a chargeable skill's key charges it; releasing fires it with a bonus scaled by the charge; a tap fires it normally
- [x] While charging, movement is slowed and attacks and other skills are blocked
- [x] A perfect dodge while charging fires the skill at full charge toward the attacker
- [x] A skill's charge unlocks at its own level with an event and a System prompt; before that, holding does not charge
- [x] Four new skills unlock at levels 5, 10, 15 and 20
- [x] Headless tests cover charge scaling, blocking while charging, the perfect-dodge counter, and charge unlocks

## Comments

**2026-11-13 (agent):** Implemented.
- Data: each skill has `ChargeSeconds` (0 means no charge), `ChargedSizePercent`, `ChargedDamagePercent` and `ChargeUnlockLevel`. The effective charge level is the later of the skill's and the charge's; a charge with neither bonus is rejected. `PlayerTuning` holds the charge move speed (40%) and a tap grace of 0.15 s.
- Rules (simulation):
  - Pressing a key whose skill can charge, while it is still held, starts a charge. Releasing fires it with `fraction = (ticks held - grace) / charge ticks`, capped at 1.
  - Size scales area radius, cone reach or projectile hit radius. Damage adds to the base after modifiers.
  - While charging: slowed, with no attacks, other skills or loadout changes. Dodging is allowed and keeps the charge, and releasing fires even mid-dash.
  - The cooldown starts when the skill fires. The charge is cleared on defeat and on revive.
  - Commands carry every held skill key (`HeldSkillSlots`), so a charge lasts while its own key stays held.
- Perfect dodge: an enemy hit caught by the dodge's invulnerability while charging fires the skill at full charge, turned to the attacker, with no soft lock-on (`SkillUsed.IsCounter`). Invulnerability outlasts the dash by a few ticks, so a hit in that tail also counters. That fits "inside the invulnerability window"; revisit in the feel review if it feels too generous.
- Events: `SkillChargeStarted`, `SkillChargeUnlocked` (only when the charge arrives after the skill), and `SkillUsed.ChargeFraction` / `IsCounter`.
- Skills: Rending Slash (damage x2, charge at 4), Ground Slam (size x2, at 6), Fire Bolt (no charge), Flame Burst (size +60%, at 7). New: Hunter's Javelin (lv 5, projectile; damage x2 at 8), Whirlwind (lv 10, area; size +80% at 12), Inferno Wave (lv 15, fire cone; reach +60% at 18), Monarch's Decree (lv 20, huge area, no charge).
- UI: a charge meter above the bar, a HOLD tag on slots whose charge is unlocked, a "CHARGE UNLOCKED" System announcement, a "PERFECT DODGE — COUNTER!" flash, and a body glow that builds with the charge.
- Tests: `SimulationChargeTest` (tap vs hold, scaling, cap, damage, cooldown, slowed and blocked, defeat, perfect-dodge counter, a dodge that catches nothing, charge unlocks, two keys held, tap grace, loadout lock, validation); `PlayerCommandBufferTest`; `ArenaChargeTest` (PlayMode: a held Ground Slam fires bigger; the new skills unlock at 5, 10, 15, 20). Deliberate removals of the counter, the loadout lock and the tap grace were each caught.

Open notes:
- **Not checked by a human:** holding keys, the charge meter, the HOLD tags and the counter flash.
- **Feel review (10):** tap grace length, charge times, move speed while charging, and whether the post-dash invulnerability tail should counter.
- **Structure:** the four charge fields could become one `ChargeDefinition` (null for none); they currently repeat across the definition, the asset and the validation.
- **Counter timing:** a counter projectile launched during the enemy phase starts flying on the next tick, like any other projectile.
