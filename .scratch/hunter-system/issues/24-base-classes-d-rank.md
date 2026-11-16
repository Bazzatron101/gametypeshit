# 24: Base classes (chosen at the start)

**What to build:** The player chooses one of four base classes when the character is made: Warrior,
Rogue, Archer or Wizard. Each class has its own skill set, gains its main stats each level, and the
Rogue jumps high enough to clear an enemy. All classes share one base animation set.

**Changed by the developer (2026-11-14):** this ticket was "classless through E-rank, three classes at
D-rank". The developer chose four classes, picked at the start. See ADR 0012.

**Blocked by:** 06: XP, levels, six stats and the level-up UI, 07: Data-driven skills with the six-slot bar, 21: M3 feel review and kill-switch (the developer asked for classes before the review)

**Status:** done

- [x] A class choice when a character is made (and once for characters saved before classes), permanent
- [x] Four classes exist as data with distinct skill sets, and each class gains its own stats every level
- [x] Only the Rogue's jump clears an enemy
- [x] Every class solos the first gate
- [x] All classes share one base animation set (there is one greybox body; nothing class-specific was added)

**2026-11-15 (developer asked, agent built):** The test arena panel (F1) can change class for testing, though the choice is otherwise permanent. A row of class buttons under "Class" swaps the old class's per-level stats for the new class's (placed points stay), its skills for the new class's up to your level, and gives and wears the new class's weapon. Bag, gold and modifiers stay. The arena never saves, so your real hunter keeps their class. `SimulationWorld.ChangeClassForTesting`; tests in `ClassTest` and `WeaponTest`. EditMode: 625 passed, 0 failed.
