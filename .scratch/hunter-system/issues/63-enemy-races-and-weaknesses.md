# 63: Enemy races as data, with weaknesses

**What to build:** Make a race a real thing in the content. A race is its own asset (name, look, weaknesses), and every enemy definition names its race. Goblins become the first race (Goblin, Hobgoblin, and later the goblin archer and shaman). Add the Beasts race (its enemies come in later tickets). Races take more or less damage from certain tags. Split from ticket 62.

**Blocked by:** none (parent: 62)

**Status:** ready-for-agent

- [ ] A race asset holds a name, a look (body colour/shape placeholder) and damage multipliers by tag
- [ ] `EnemyDefinitionAsset` names its race; the simulation's `EnemyDefinition` carries what it needs (the multipliers), not Unity assets
- [ ] Goblin and Hobgoblin belong to Goblins; the Shield Crystal belongs to no race
- [ ] Weaknesses: Beasts take extra fire damage, Goblins take extra holy damage (numbers are data, tuned in playtest)
- [ ] A hit's tags (ticket 08) decide the multiplier; damage numbers show when a hit was strong against the target
- [ ] `EnemyView` shows each race's look; new looks are placeholders labelled "AI"
- [ ] Snapshots and co-op carry the race; the content check holds every enemy to having a valid race (except crystals)
- [ ] Tests cover the multipliers, an enemy with no race, and snapshots
