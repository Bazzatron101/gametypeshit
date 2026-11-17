# 81: Hunters are Australian animals, with a species bonus

**What to build:** Every hunter is an upright animal-folk character, and every species is an Australian native animal. The player picks a species in the character creator. Each species gives a small stat bonus that grows with rank, plus one small perk. From a design discussion with the developer, 2026-11-17. It gives the game a memorable hook and makes co-op players easy to tell apart. Ready-made low-poly animal art also fits the flat-shaded style (ADR 0002).

**Blocked by:** none

**Status:** ready-for-agent

- [ ] Species are data: each has a name, a stat, a perk and its look. Adding one needs no code.
- [ ] Eight species:

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

- [ ] **Stat bonus:** +2 to the species' stat at E-rank, plus 1 more at each rank promotion. It adds on top of class bonuses and free points.
- [ ] **Perks are small:** each is a nudge you notice, not a build-deciding power. All perk numbers live in the species data and can be changed in the Inspector.
- [ ] No species is the only right pick for a class: every class has at least two species that suit it
- [ ] The species is chosen in the character creator, before the class picker, and is **permanent** like the class and name (ADR 0019). There is no way to change it later.
- [ ] Body colour stays, as fur or feather colour; build (slim, average, broad) stays
- [ ] All species share one body and skeleton. Only heads, ears, tails and proportions differ, so worn gear (ticket 34) and future animations fit every species.
- [ ] Greybox look until real art arrives: the capsule body plus simple ears, snout or beak and tail made from basic shapes, labelled "AI" (memory rule)
- [ ] The stat screen shows the species, its bonus and its perk; nameplates show the species
- [ ] Saves move up a format version. Older hunters are asked for a species once and keep their class and progress.
- [ ] Snapshots carry each player's species, and perks run on the host like other combat rules
- [ ] Bot teammates each get a species that suits their role
- [ ] Automated runs (`-coop`) take `-species NAME`, with a default when it is not given
- [ ] An ADR records the species rules: one shared body, small bonuses that grow by rank, a permanent choice, Australian natives only
- [ ] Tests cover the bonus at each rank, each perk, the save upgrade and the content check

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
