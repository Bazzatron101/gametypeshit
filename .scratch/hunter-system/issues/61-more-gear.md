# 61: More gear

**What to build:** More gear, now that gear shows on the character (ticket 34) and changes stats (ticket 51). From the developer's playtest, 2026-11-17: "the gear looks good and changes the character's feel and now we need to add more". The developer wants all four:

- **More sets:** more full armour sets like Cinder and the Warlord's Plate, each with its own look.
- **Gear per class:** gear that suits each of the eight classes (for example robes for casters, light leathers for rogues).
- **More weapons:** more weapons for each class beyond its starting one.
- **Drops from gates:** more gear dropping from gates and bosses, with rarities.

**Blocked by:** 34: Gear shows on the character (done), 51: Gear that changes stats (done)

**Status:** ready-for-human (press I in the arena to see the new gear and its colours; clear Thornwood Hollow, a class trial or the raid for drops)

- [x] Four new armour sets, one per style: heavy plate, medium, light leathers, robes; each with its own look
- [x] Two new weapons for each of the eight classes (16 in all), locked to their class
- [x] Any class can wear any armour
- [x] Rarities: Common, Rare, Epic and Legendary; higher tiers have bigger stats and extra bonuses, and Legendaries have a special look
- [x] Each gate has its own drop list; gate rank and Luck raise the rarity odds
- [x] New looks are placeholders labelled "AI" (memory rule), and the content check holds every piece to the rules

## Notes

**To settle in triage:**
- **How much:** how many sets, and how many weapons per class, for a first pass?
- **Rarities:** which tiers (for example common, rare, epic), and what a higher tier gives (bigger stats, extra bonuses, a different look)?
- **Who can wear what:** can any class wear any armour, or is some gear locked to classes?
- **Where it drops:** which gates and bosses drop which pieces, and does rank matter?
- Probably too big for one ticket: consider splitting by the four parts above.

## Comments

**2026-11-17 (developer, recorded by agent):** Decided in triage:
- **First round:** four armour sets (heavy, medium, light, robes) and two new weapons per class.
- **Rarities:** Common, Rare, Epic, Legendary. A higher tier gives bigger stats plus extra bonuses (for example more skill damage); Legendaries have a special look.
- **Who wears what:** any class can wear any armour; weapons belong to their class.
- **Drops:** each gate has its own list of what it can drop, so players can farm a gate for a piece. Higher-rank gates raise the odds of better rarities, and Luck raises them too (the Tab panel's "Drop odds (coming soon)" becomes real).

Suggested order if split: rarities and per-gate drop lists first (they work with today's gear), then the four sets, then the weapons.

**2026-11-17 (agent):** Built.
- **Rarities** (`Rarity`, `RarityOddsDefinition`): every piece is Common, Rare, Epic or Legendary. In a clear draw each item weighs its tier's base weight (60 / 28 / 10 / 2). Each rank a gate is above E adds 25% per tier above Common, and each point of Luck above the start adds 2%, so B gates and lucky hunters find rarer pieces. Modifiers weigh as Common. When everything in a pool is one tier, it draws exactly as before. The odds are on `GameContent` (Rarity Odds) and can be changed in the Inspector.
- **Weapons belong to their class** (`GearDefinition.ClassIds`): another class's weapon cannot be worn ("Not yours" in the gear panel) and is never drawn from a clear. A pool holding only other classes' weapons gives a duplicate to sell. Bots skip them too. Any class wears any armour, and the content check refuses armour locked to a class.
- **Existing gear:** each class's starting weapon is Common and locked to its class. The Goblin Cleaver (Warrior) and Cinder Staff (Wizard) are Rare. The Cinder set and the Hunter's Quiver are Rare; the Warlord's Plate and Ember Heart are Epic.
- **Four sets** (head and feet Common, hands Rare, body Epic with its own bonus):
  - Ironwall (heavy: Strength and Vitality)
  - Wayfarer's (medium: Agility and Vitality)
  - Nightweave (light leathers: Agility and Luck)
  - Starweave (robes: Intelligence and Sense)
- **Two weapons per class,** a Rare and a Legendary. Each Legendary has two bonuses of its own:
  - Warrior: Knight's Longsword, Worldsplitter
  - Rogue: Twin Daggers, Whisperfang (the Rogue's first weapons)
  - Archer: Yew Longbow, Galeheart
  - Wizard: Oak Staff, Starfall Staff
  - Monk: Iron Knuckles, Dragonscale Wraps
  - Cleric: Silver Sceptre, Dawnbringer
  - Warlock: Bone Rod, Voidcaller
  - Summoner: Spirit Staff, Soulbinder
- **Where each drops** (each gate keeps its own list):
  - Thornwood Hollow (E): the Ironwall and Wayfarer's sets.
  - The Goblin King's raid: the Nightweave and Starweave sets, and every Legendary weapon.
  - Each class's D trial: its Rare weapon. Its B trial: its Legendary.
  - Goblin Warren is unchanged.
- **On screen:** names are coloured by rarity (grey, blue, purple, gold) in the gear panel, with "Rare · Warrior only" under a picked piece. The clear notice names the rarity. A worn Legendary glows with a pulsing gold light (`LegendaryGlow`).
- **Looks:** 32 placeholder looks built from shapes per style and weapon kind (`GearLookSetup.MoreGear.cs`), all labelled "AI".
- **Data in one place:** `Assets/Editor/GreyboxSceneBuilder.Gear.cs`. Each piece is made once by Hunter System > Refresh Content; after that, the Inspector's values stand.

Evidence: `RarityTest` (11 tests: the odds, refused odds, a Legendary rarer at E than at B, one-tier pools drawing as before, never drawing another class's weapon, the duplicate to sell, wearing, the content rules, four sets and two weapons per class, every new piece dropping and labelled "AI"). `CharacterSaveSceneTest` now gives the greybox Warrior a Warrior weapon (it used the Wizard's staff). **835 EditMode and 40 PlayMode tests pass.**

Not done here:
- **The numbers are starting values.** Nobody has played with the new gear yet.
- **The Rogue still starts with no weapon;** its first comes from its D trial.
- **The Legendary glow is not checked by eye:** tests run without graphics.
