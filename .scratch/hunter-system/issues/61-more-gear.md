# 61: More gear

**What to build:** More gear, now that gear shows on the character (ticket 34) and changes stats (ticket 51). From the developer's playtest, 2026-11-17: "the gear looks good and changes the character's feel and now we need to add more". The developer wants all four:

- **More sets:** more full armour sets like Cinder and the Warlord's Plate, each with its own look.
- **Gear per class:** gear that suits each of the eight classes (for example robes for casters, light leathers for rogues).
- **More weapons:** more weapons for each class beyond its starting one.
- **Drops from gates:** more gear dropping from gates and bosses, with rarities.

**Blocked by:** 34: Gear shows on the character (done), 51: Gear that changes stats (done)

**Status:** ready-for-agent

- [ ] Four new armour sets, one per style: heavy plate, medium, light leathers, robes; each with its own look
- [ ] Two new weapons for each of the eight classes (16 in all), locked to their class
- [ ] Any class can wear any armour
- [ ] Rarities: Common, Rare, Epic and Legendary; higher tiers have bigger stats and extra bonuses, and Legendaries have a special look
- [ ] Each gate has its own drop list; gate rank and Luck raise the rarity odds
- [ ] New looks are placeholders labelled "AI" (memory rule), and the content check holds every piece to the rules

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
