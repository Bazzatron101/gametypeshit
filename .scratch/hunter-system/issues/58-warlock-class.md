# 58: The Warlock, a dark caster

**What to build:** A Warlock base class, a dark caster, with its own skills, weapon, stats per level and a full specialty tree with D and B trials (ADR 0022). Working names.

**Blocked by:** 54: Fill the four classes' specialty trees

**Status:** ready-for-human (play and rename: working names; trial words blank)

- [x] The Warlock can be picked when a character is made, with its own skills, stats per level and weapon
- [x] The Warlock plays differently from the Wizard
- [x] The Warlock has a full tree of six specialties and its two trials
- [x] The Warlock solos the first gate, like every class (ADR 0012)

## Notes

**To settle before building:**
- **Magic is the Wizard's alone** (ADR 0012; content validation checks it). The Warlock, and the Cleric's holy magic (ticket 56), need that rule loosened to "magic belongs to the caster classes". Is that right?
- **What sets it apart from the Wizard:** damage over time (curses that tick; nothing ticks yet), draining health from enemies to heal itself, trading its own health for power, or something else?
- **Its stats each level:** Intelligence and Vitality, or Intelligence and Luck?

## Comments

**2026-11-17 (developer, recorded by agent):** Asked to add a Warlock class, alongside the Monk, Cleric and Summoner (tickets 55 to 57).

**2026-11-17 (developer, recorded by agent):** Decided:
- **Magic belongs to the caster classes:** the Wizard, the Warlock and the Cleric. This changes ADR 0012's "magic belongs to the Wizard only", and the content check follows.
- **Drain life:** the Warlock's dark spells heal it for part of the damage they deal. That is a new life-steal effect, and it shares healing with the Cleric (ticket 56).
- **Intelligence and Luck each level.**

**2026-11-17 (agent):** Built. The Warlock gains +1 Intelligence and +1 Luck each level.
- **Drain life** (`SkillDefinition.LifeStealPercent`): every Warlock skill heals the Warlock for part of the health its direct hits remove (bursts do not count). It uses the same healing as the Cleric.
- **Weapon:** Hexed Rod, with a placeholder look labelled "AI".
- **Skills:** Shadow Bolt (20% drain) and Drain Touch (50%) from the start; Hex Nova (3), Soul Siphon (5), Abyssal Wave (10, can be charged), Void Rupture (15).
- **Tree:** Hexer and Shadowcaller at D; Soul Reaper and Blood Mage under Hexer; Voidlord and Doombringer under Shadowcaller.
- **Magic:** ADR 0012's rule is now "magic belongs to the caster classes" (Wizard, Warlock, Cleric), and the content check follows.

Evidence: `HealingTest.DrainLifeHealsTheCasterForPartOfTheDamage`, and the solo-gate playthrough passes for the Warlock. **779 EditMode and 40 PlayMode tests pass**, and the solo-gate playthrough passes for all eight classes.
