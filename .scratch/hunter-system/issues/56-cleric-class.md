# 56: The Cleric, a support class that heals

**What to build:** A Cleric base class, a holy caster who supports the party, with its own skills, weapon, stats per level and a full specialty tree with D and B trials (ADR 0022). Healing other players does not exist yet, so this ticket builds it. Its holy magic needs ADR 0012's rule that magic is the Wizard's alone loosened (see ticket 58).

**Blocked by:** 54: Fill the four classes' specialty trees

**Status:** ready-for-human (play and rename: working names; trial words blank)

- [x] Skills can heal the caster and other players
- [x] The Cleric can be picked when a character is made, with its own skills, stats per level and weapon
- [x] The Cleric has a full tree of six specialties and its two trials
- [x] The Cleric solos the first gate, like every class (ADR 0012)

## Notes

**To settle before building:**
- **How healing aims:** an area round the Cleric, a bolt that heals the first ally it reaches, or the most hurt ally nearby?
- **What scales it:** Intelligence, Sense or Vitality?
- **Bots:** should bot teammates (ticket 33) heal with it?
- **Solo play:** the Cleric must still solo the first gate, so how much of its kit deals damage?

## Comments

**2026-11-17 (developer, recorded by agent):** Chose the Monk, Cleric and Summoner as new classes (tickets 55 to 57).

**2026-11-17 (developer, recorded by agent):** Decided:
- **Healing aims as a bolt:** it flies where the camera aims and heals the first ally it reaches.
- **Sense raises healing;** the Cleric gains Intelligence and Sense each level.
- **Holy damage:** smite skills deal damage, so the Cleric can solo the first gate.

**2026-11-17 (agent):** Built. The Cleric gains +1 Intelligence and +1 Sense each level.
- **Healing** (`SkillDefinition.Healing`):
  - A healing bolt flies where it is aimed, past enemies, and heals the first other player it reaches. It never heals its caster.
  - A healing area heals every player in it, the Cleric too, which is how a solo Cleric heals itself.
  - Healing never goes past max and never raises the fallen.
  - Sense now raises skills that scale with it, and "more damage" passives on a heal are more healing.
  - A new event, `PlayerHealed`, is sent over the network.
- **Weapon:** Acolyte's Sceptre (holy bolts and a flash of radiance), with a placeholder look labelled "AI".
- **Skills:** Smite and Mending Light (a heal bolt) from the start; Holy Nova (3), Sanctuary (5, an area heal), Judgement (10, can be charged), Radiant Lance (15).
- **Tree:** Priest (healer) and Templar (smiter) at D; Oracle and Saint under Priest; Paladin and Inquisitor under Templar.
- **Magic:** the rule is now "magic belongs to the caster classes" (ticket 58's decision).

Evidence: `HealingTest` (8 tests: the bolt, never the caster, the area, max and the fallen, Sense, drain life, content rules, the wire), and the solo-gate playthrough passes for the Cleric. **779 EditMode and 40 PlayMode tests pass**, and the solo-gate playthrough passes for all eight classes.

Not done here: **bot teammates do not heal yet.** A bot Cleric fights with its smites but never casts a heal (`CompanionBrain` skips healing skills). Teaching bots to heal hurt allies would be a follow-up.
