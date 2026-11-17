# 81: Hunters are Australian animals, with a species bonus

**What to build:** Every hunter is an upright animal-folk character, and every species is an Australian native animal. The player picks a species in the character creator. Each species gives a small stat bonus that grows with rank, plus one small perk. From a design discussion with the developer, 2026-11-17. It gives the game a memorable hook and makes co-op players easy to tell apart. Ready-made low-poly animal art also fits the flat-shaded style (ADR 0002).

**Blocked by:** none

**Status:** done

- [x] Species are data: each has a name, a stat, a perk and its look. Adding one needs no code.
- [x] Eight species:

  | Species | Stat | Perk |
  |---|---|---|
  | Kangaroo | Strength | Stronger knockback on hits |
  | Tasmanian devil | Strength | Hits on low-health enemies do more damage |
  | Sugar glider | Agility | Glides when falling |
  | Wombat | Vitality | Takes less damage from behind |
  | Koala | Vitality | Slowly regains health out of combat |
  | Cockatoo | Intelligence | Skills cool down slightly faster |
  | Platypus | Sense | Senses hidden enemies and traps nearby |
  | Quokka | Luck | Slightly better drops (adds to the Luck drop odds from ticket 61) |

- [x] **Stat bonus:** +2 to the species' stat at E-rank, plus 1 more at each rank promotion. It adds on top of class bonuses and free points.
- [x] **Perks are small:** each is a nudge you notice, not a build-deciding power. All perk numbers live in the species data and can be changed in the Inspector.
- [x] No species is the only right pick for a class: every class has at least two species that suit it
- [x] The species is chosen in the character creator, before the class picker, and is **permanent** like the class and name (ADR 0019). There is no way to change it later.
- [x] Body colour stays, as fur or feather colour; build (slim, average, broad) stays
- [x] All species share one body and skeleton. Only heads, ears, tails and proportions differ, so worn gear (ticket 34) and future animations fit every species.
- [x] Greybox look until real art arrives: the capsule body plus simple ears, snout or beak and tail made from basic shapes, labelled "AI" (memory rule)
- [x] The stat screen shows the species, its bonus and its perk; nameplates show the species
- [x] Saves move up a format version. Older hunters are asked for a species once and keep their class and progress.
- [x] Snapshots carry each player's species, and perks run on the host like other combat rules
- [x] Bot teammates each get a species that suits their role
- [x] Automated runs (`-coop`) take `-species NAME`, with a default when it is not given
- [x] An ADR records the species rules: one shared body, small bonuses that grow by rank, a permanent choice, Australian natives only
- [x] Tests cover the bonus at each rank, each perk, the save upgrade and the content check

## Notes

**Follow-ups decided in triage (not part of this ticket):**
- **Monstrous gate creatures:** gate enemies should read clearly as monsters, so wolves become dire wolves, boars become armoured monster boars, and so on. This applies to tickets 65, 66 and 69.
- **The guild town:** its people become Australian animal-folk too.
- **Goblins** stay as they are, as gate monsters.

Context: hunter looks live in `CharacterLooks` (game layer); the simulation keeps only the colour index and build (ADR 0019). Class stat bonuses already exist, so species bonuses add on top of them, not in place of them.

## Comments

**2026-11-17 (developer, recorded by agent):** Decided in triage:
- **Bonus:** a stat bonus plus a small perk.
- **Growth:** the stat bonus grows a little at each rank promotion.
- **Species:** all Australian native animals: the suggested six (kangaroo, sugar glider, wombat, cockatoo, platypus, quokka) plus the Tasmanian devil and the koala, eight in all.
- **Changing species:** never, like the class.
- **Enemies:** gate creatures are made clearly monstrous.
- **World:** the town's people become animal-folk; goblins stay.

**2026-11-17 (agent):** Built (ADR 0024).

- **Simulation:** `SpeciesDefinition` and `SpeciesPerk` (`Species.cs`); `SimulationConfig.Species`, checked when a world starts.
  - **Stat bonus:** worked out from the hunter's own rank each time (`SpeciesStatBonus`, inside `EffectiveStat`), so it is never written into the saved stats and grows at each promotion by itself. `PlayerState.SpeciesStats` shows it; `TotalStat` includes it.
  - **Perks** (`SimulationWorld.Species.cs`), with these starting numbers:

    | Species | Perk as built |
    |---|---|
    | Kangaroo | Hits push ordinary enemies 1.2 m straight back; bosses stand firm |
    | Tasmanian devil | +15% damage on an enemy at or below 30% health |
    | Sugar glider | The way down from a jump lasts 60% longer |
    | Wombat | 20% less damage from a hit from behind (outside the front half) |
    | Koala | 1% of max health a second, after 6 seconds without hitting or being hit |
    | Cockatoo | Skills cool down 8% faster |
    | Platypus | Finds hidden gates from 50% further (20 m becomes 30 m) |
    | Quokka | +5 Luck in a gate's clear draw, on top of the Luck stat (`DropLuck`) |

  - **Saves:** character format 8 adds the species. Format 7 and older load with none.
  - **Snapshots** carry each player's species and when they last fought (for the koala).
- **Data:** the eight species are a list in `GameContent.asset` (Inspector: Species). Adding one needs no code: it picks a stat and one of the perk kinds and its numbers.
- **Making a hunter:** a new `SpeciesPicker` opens after the name and looks and before the class picker. A hunter from before species is asked once, and keeps class and progress (`SimulationRunner.ChooseSpecies`, `CharacterRecord.WithSpecies`).
- **Shown:**
  - Greybox ears, snout or beak, tail and proportions per species (`SpeciesLooks`), on the body so they take its colour, pose and build.
  - The stat screen shows the species, its perk and its bonus (gold "(+2 kangaroo)").
  - Nameplates read "Name · Species", and the co-op menu says "level 9 wombat Warrior".
- **Bots:** each teammate gets a species that suits its class: warrior wombat, archer platypus, rogue sugar glider, cleric koala, other casters cockatoo, the rest Tasmanian devil. Older teammates get one when they join.
- **Automated runs:** `-species ID`, or the first species (kangaroo).
- **Tests:** `SpeciesTest`. Covers the bonus at E, D, C and S, Vitality health, the save and the old-save upgrade, snapshots, the content check, bots and each perk. The PlayMode creator tests now pick a species too; `TestSaves` gives test hunters a kangaroo.

**What differs from the ticket:**
- **Perks for systems that do not exist yet:**
  - **Platypus:** there are no hidden enemies or traps yet, so it senses hidden gates for now.
  - **Sugar glider:** jumps are the only falls, so it glides on the way down from a jump.
- **The species picker is its own screen** between the creator and the class picker, not a row in the creator, so an older hunter can be asked for a species alone.
- **Greybox parts are made in code,** so there is no art file to label "AI" yet.

**Evidence:** EditMode 861 of 862 passed on the first run. The one failure was my own test: it raised a hunter to level 96, past the level cap of 95. After fixing it, all 16 `SpeciesTest` tests pass, and all **45 PlayMode** tests pass, including the creator scenes with the species step.

Not verified: nobody has seen the species picker, the greybox heads and tails, or the perks in the editor yet.
