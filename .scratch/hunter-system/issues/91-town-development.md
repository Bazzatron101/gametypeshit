# 91: Towns develop as hunters clear their region

**What to build:** A town grows when hunters claim a gate bounty at its guild, clear gates near it, or move the story on. From the developer, 2026-11-17 (ticket 36): "the bounty will pay with gold and development of the location here the bounty was claimed/made". Today settlements are generated once from where they stand (tickets 82-89) and never change. This ticket gives each town a development level that rises with those and shows in the town.

**Blocked by:** none. Bounties are added later (see below). Parts of a level that belong to unbuilt tickets (trades 85, life 87, castles 88, the bigger guild town 89) are added to it as those land.

**Status:** ready-for-agent

- [ ] Each town has a development level, starting at its generated look
- [ ] **Levels:** an ordinary town has 2 levels above its starting look; a castle (ticket 88) in a dangerous area has up to 4. The counts are configuration values.
- [ ] **What raises it:** a gate cleared in the town's region, and story progress. Bounties claimed at the town's guild come later, once bounties exist (ticket 36); leave a way in for them. Each adds a fixed amount of development; the amounts, and how much each level needs, are configuration values.
- [ ] **It only grows.** Nothing lowers a town's development, leaks included.
- [ ] **What a level brings,** all of these: more buildings, more trades (ticket 85), better shops and gear, a bigger reach, more people and life (ticket 87), and more guild work on offer
- [ ] **War damage runs across the map:** worst in the south-east, lessening towards the north-west, where the game starts. A town's starting damage comes from where it stands on that line, so north-western towns start whole and south-eastern ones start badly damaged. Developing a damaged town restores its buildings.
- [ ] **A town's region** is set on the map, so a clear counts for the towns of its gate's region. Gates and places already name their region (`OverworldAsset`, ticket 84's region looks); every town needs one too.
- [ ] **How it shows:** a mix. New buildings fill new plots on the town's generated streets (ticket 83), and some things switch between prepared stages (for example a damaged building and its restored version).
- [ ] The guild town (ticket 89) develops the same way
- [ ] Development belongs to the host's world (`WorldRecord`), like found gates and clears, so the whole party sees the same towns
- [ ] Saves move up a format version; older worlds start every town at its first level
- [ ] Tests cover clears in and outside the region, story progress, the damage gradient, the level caps, saving and loading, and the old-save upgrade

## Open questions

1. ~~What changes when a town develops?~~ All of the suggestions; war-struck buildings are restored.
2. ~~How many levels?~~ 2 per town; castles in a dangerous area maybe 4.
3. ~~What develops a town?~~ Nearby clears, bounties and story progress.
4. ~~Can a town go back down?~~ No.
5. ~~The guild town?~~ Develops the same way.
6. ~~Added to the layout, or prepared stages?~~ A mix of both.
7. ~~Does the beyond-licence cut apply?~~ Only to the bounty's gold. Development is a fixed amount.
8. ~~Which places are war-struck?~~ A gradient: worst in the south-east, better towards the north-west start.
9. ~~"Nearby"~~ In the town's region; regions are set on the map.
10. ~~The blocker~~ Start with clears and story progress; add bounties later.

## Comments

**2026-11-17 (developer, recorded by agent):** Triage answers:
1. All of the suggestions; "possibly in the war struck areas the buildings are restored".
2. "2 levels per town and for the castles in the bad area 4 maybe".
3. "nearby clears, bounties and story progression".
4. No.
5. Yes.
6. "a mix of both".
7. "itll just count to the gold, the development is a fixed amount".

**2026-11-17 (developer, recorded by agent):** Last three:
8. War damage: "its a range from the south east of the map with the worst parts and the more north west you are the better it is making north west the starting area".
9. A clear counts if it is in the town's region: "we will set up the regions in the town map".
10. "we will add bounties in later".
Moved to ready-for-agent.
