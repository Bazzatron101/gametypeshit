# 55: The Monk, a fifth base class

**What to build:** A Monk base class that fights unarmed with fast strikes (Agility and Vitality each level), with its own skills, starting weapon (hand wraps), and a full specialty tree with D and B trials (ADR 0022). Working names.

**Blocked by:** 54: Fill the four classes' specialty trees

**Status:** ready-for-human (play and rename: working names; trial words blank)

- [x] The Monk can be picked when a character is made, with its own skills, stats per level and weapon
- [x] The Monk has a full tree of six specialties and its two trials
- [x] The Monk solos the first gate, like every class (ADR 0012)

## Comments

**2026-11-17 (developer, recorded by agent):** Chose the Monk, Cleric and Summoner as new classes (tickets 55 to 57).

**2026-11-17 (agent):** Built. The Monk fights with bare hands and gains +1 Agility and +1 Vitality each level.
- **Weapon:** Hand Wraps (quick jabs and a heavy palm), with a placeholder look labelled "AI".
- **Skills:** Palm Strike and Rising Kick from the start; Flurry of Fists (3), Force Wave (5, a wave of force for reach), Hundred Palms (10, can be charged), Dragon's Descent (15).
- **Tree:** Iron Fist and Wind Walker at D; Mountain Breaker and Dragon Fist under Iron Fist; Storm Dancer and Shadow Palm under Wind Walker. It has its two trials.
- **Bots:** a Monk teammate plays as a skirmisher.

Evidence: the solo-gate playthrough passes for the Monk. **779 EditMode and 40 PlayMode tests pass**, and the solo-gate playthrough passes for all eight classes.
