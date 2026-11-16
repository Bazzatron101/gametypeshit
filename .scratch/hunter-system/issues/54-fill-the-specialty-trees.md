# 54: Fill the four classes' specialty trees

**What to build:** Every specialty of the Warrior, Rogue, Archer and Wizard trees (ADR 0022), with each class's D and B trials. Each D specialty has two passives and one skill; each B specialty has two passives and two skills, like the Vanguard and the Berserker. Working names, which the developer may rename.

**Blocked by:** 26: B-rank specialty quests

**Status:** ready-for-human (play and rename: the names are working names; the trial notices and scenes are blank)

- [x] The Warrior's four stubs are built (Sentinel, Champion, Guardian, Bulwark)
- [x] The Rogue, Archer and Wizard each have a full tree of six and their D and B trials
- [x] Each specialty changes how its class plays, using the skill effects and modifiers that exist
- [x] The content check holds every class to the tree's shape, and the tests pass

## Comments

**2026-11-17 (developer, recorded by agent):** Asked to "add more classes and sub classes so it's all full". The developer chose: full trees, each specialty as deep as the Vanguard and Berserker, with working names picked by the agent. New classes are tickets 55 to 57.

**2026-11-17 (agent):** Built.
- **The Warrior's four stubs** are filled: Sentinel (D), Champion, Guardian and Bulwark (B).
- **The Rogue, Archer and Wizard** each have a full tree of six and their two trials:
  - Rogue: Assassin and Trickster; Shadowblade, Nightstalker, Bladedancer, Saboteur.
  - Archer: Marksman and Ranger; Sniper, Hawkeye, Warden, Trapper.
  - Wizard: Pyromancer and Arcanist; Flamecaller, Ashbringer, Chronomancer, Spellblade.
- **What each has:** a D specialty has two passives and one skill (level 16); a B specialty has two passives and two skills (levels 56 and 62). Some skills can be charged, some pierce.
- **The data is in one place,** `Assets/Editor/GreyboxSceneBuilder.Specialties.cs`. Each description is worked out from its numbers, so the words always match what it does.
- **Trials:** each class's D and B trials reuse the first gate's kit. Each gives a clear reward that suits the class, and each has a clear scene with three blank lines.

Evidence: `EveryClassesSpecialtyTreeHasATrialPerTier` checks every class has the tree's shape and all six built. **779 EditMode and 40 PlayMode tests pass**, and the solo-gate playthrough passes for all eight classes.

Not done here: the trials are greybox (E-rank enemies); the trial notices and scene lines are blank markers for the developer.
