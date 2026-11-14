# 36: Lay out the overworld

**What to build:** The layout of the walkable world the game is set in: where the guild town sits, which regions there are, what opens each one, where the gates stand, and how big it is. This replaces the spec's "gates are entered through the hub menu" (see ADR 0015, proposed). Layout only; building terrain, streaming and mounts are later tickets.

**Blocked by:** None

**Status:** ready-for-human (open questions below)

- [x] Scale settled (how long a crossing takes, and at what speed): an hour at walking pace, ~12 × 7 km
- [ ] Region list, order and what opens each one settled
- [ ] Gate placement per region sketched (story gates and farming gates)
- [x] What physically stops a player entering a region that is not open yet: nothing physical; danger and no guild work there (see Comments)
- [ ] Mounts: when you get one and how fast it is
- [ ] ADR 0015 accepted, and the spec's non-goal and stories 40 and 48 rewritten

## Decided (developer, 2026-11-14)

- **A walkable overworld,** not only a map screen. You walk to the gates.
- **Mounts** make travel faster.
- **Size:** about an hour to run from one side of the map to the other.
- **The guild town (the hub) is on the west edge,** and the world unrolls east, away from it. The S-rank end story lies at the far east.
- **Regions open by story beats as well as ranks,** unevenly: some ranks open two regions, some none.
- **Light danger outside dungeons:** a few roaming monsters and spots that leaked from gates. Real fights stay in the dungeons.
- **Co-op:** players roam freely and regroup at a gate to go in together.

## First sketch (agent, to change freely; names are placeholders)

West to east:

| Region | Opens | Feel |
| --- | --- | --- |
| Guild town | Start | The hub: gate board (intel), storage, shop. Safe. |
| Greenmarch | Start | Farms and woods around the town. E-rank gates. The first gate is a small, cramped, quiet place close to town (ticket 16's tone). |
| Fenward mire | D promotion | Marsh south of the road. D-rank gates. |
| Brokenstone hills | A story beat during C | Quarries and old ruins north of the road. C-rank gates. |
| Ashen steppe | B promotion | Open plains; the steppe gives mounts room to run. |
| Saltreach coast | B specialty quests (ticket 26) | Northern coast; specialty quest gates. B opens two regions. |
| (none) | A promotion | No new region. A-rank gates open inside regions you already know, so old ground turns dangerous again (spec story 11, in reverse). |
| Riftlands | S story | The far east, where the gates come from. Ties into the hook (ticket 16, still open). |

- **Main road** runs west to east through every region, so the way forward is never in doubt.
- ~~**Region borders** with guild wardens or natural barriers.~~ Replaced: no walls; danger and the guild's work keep you out (see Comments).

## Scale, worked out from the current tuning

`PlayerTuning`: walking 5 m/s; sprint 160 % = 8 m/s.

| Crossing takes an hour… | Road length | Map size (road winds ~1.5× straight) | For comparison |
| --- | --- | --- | --- |
| walking (5 m/s) | ~18 km | ~12 × 7 km (~85 km²) | About the size of the biggest open-world games |
| sprinting (8 m/s) | ~29 km | ~19 × 10 km (~190 km²) | Over twice that |

A mount at 3× sprint (24 m/s) crosses the walking-size map in about 12 minutes.

Either size is far more ground than can be built by hand at ~250 hours a year (spec, "Capacity reality"). It is only possible with generated terrain, hand-placed points of interest (gates, camps, landmarks, the road), and a lot of plain land between them. Worth deciding before any terrain is built.

## Open questions

1. ~~**Scale**~~ Settled: an hour at walking speed (see Comments).
2. ~~**The gate board**~~ Settled: intel and send party (see Comments). A gate's entrance in the world uses the same ready check (see Comments).
3. ~~**Mounts: when**~~ Settled: at the D-rank promotion (see Comments). Guests without a mount walk (see Comments).
4. ~~**Joining**~~ Settled: next to the host (see Comments).
5. ~~**Leaked danger**~~ Settled: matches nearby gates (see Comments).

## Comments

**2026-11-14 (developer, recorded by agent):** Scale decided: **an hour to cross at walking pace.**
- Walking 5 m/s for an hour: a main road of about 18 km, winding across a map of about 12 × 7 km (~85 km²).
- Sprinting (8 m/s) crosses it in about 38 minutes; a mount at 3× sprint, in about 12.
- With the origin at the map's centre, nothing is more than ~7 km out, so float precision is fine without a floating origin.
- If walking or sprint speed is retuned for combat, the map's size stays the same; the crossing time changes instead.

**2026-11-14 (developer, recorded by agent):** The gate board does **intel and sends the party.**
- It gives intel on gates (ticket 20) and can send the whole party into any gate the host's world has already found, as it does today (ADR 0008's ready check and host start stay).
- A gate has to be found by walking to it first; after that, the board saves the trip. This keeps spec story 40 ("no travel I do not enjoy") for repeat runs, while the first visit is a journey.
- The list of found gates belongs to the host's world (`WorldRecord`).

**2026-11-14 (developer, recorded by agent):** The first mount comes with the **D-rank promotion.**
- It arrives with the class choice (ticket 24) and Fenward mire opening, so the world gets bigger and faster to cross at the same moment.
- E-rank is all on foot, inside the Greenmarch, which fits the small, weak start (ticket 16).
- The mount belongs to the character (ADR 0005), so a guest brings their own; a guest still at E-rank has none.

**2026-11-14 (developer, recorded by agent):** A player who joins while the host is in the overworld **appears next to the host.**
- The other join rules stay: during a run, at the latest checkpoint (ADR 0003); while the host is in the guild town, in the town (ADR 0008), which is next to the host anyway.
- If the host is in a region the joining character has never seen, they still appear there: map progress is the host's world's (spec story 71).
- If the host is mid-fight with leaked monsters, the joiner lands in that fight. Fine for light danger; revisit if it feels bad.

**2026-11-14 (developer, recorded by agent):** Leaked monsters **match the gates near them.**
- Each gate leaks a few of its own enemies (its rank, its enemy types) into the ground around it. Danger follows the gates, not the region.
- So when A-rank gates open in old regions, the land around them turns dangerous too, which is the point of A opening no new region.
- A gate's surroundings warn you what is inside before you go in, alongside the board's intel (ticket 20).
- Still to settle: how far the leak reaches, whether it shows (a scorched ring, a darker sky), and whether clearing a gate stops its leak for a while.

**2026-11-14 (developer, recorded by agent):** A guest without a mount **just walks.** No riding double, no loaned mounts.
- They fall behind on long trips; the party can wait, or go home and use the gate board, which sends everyone into a found gate wherever they stand.
- Joining appears next to the host, so a late guest starts with the party at least.
- Revisit if playtests show unmounted guests left behind often.

**2026-11-14 (developer, recorded by agent):** A gate's entrance out in the world uses **the same ready check as the board** (ADR 0008).
- Walking up to the entrance opens the same list: the gate, the party, who is ready. Each player marks themselves ready; only the host starts, and only when everyone present is ready.
- The whole party goes in together, wherever they stand, as from the board. So a guest still walking does not need to arrive first.
- Readiness exists at the board and at gate entrances, and is cleared on the way in, on the way home, and when a connection drops, as today.

**2026-11-14 (developer, recorded by agent):** Regions that are not open yet have **no wall.** The developer: "The area is to dangerous and the player has no or very little quests that the guild will let the do in that town".
- You can walk anywhere from the start. A region "opening" means the guild starts giving you work there, not that a way in appears.
- **Danger keeps you out,** and the danger is the enemies there (developer: "dangerous in terms of enemies in the area"), not the land itself: no poison swamps, storms or cliffs. A region's gates are above your rank, and the monsters they leak (which match the gates) are far too strong for a low-rank hunter.
- This sits with "light danger": there are still only a few monsters outside dungeons, but in a region above your rank each one can kill you.
- **No work keeps you out:** the guild in that region's town offers you no quests, or very few, until your rank or the story allows it. Opening a region = its town's guild unlocks your quests there.
- This drops the guild wardens and natural barriers from the first sketch. Story-opened regions (Brokenstone hills, the Riftlands) open the same way: the story makes the guild hand out work there.
- **New from this:** each region has a town with a guild branch (the sketch had only one town). The guild town on the west edge stays the hub.
- Still to settle: can a party go through a gate in a region the guild has not opened for them? Settled below: yes.

**2026-11-14 (developer, recorded by agent):** A party **can go through any gate it reaches, at its own risk,** even in a region the guild has not opened for them.
- The gate's ready check works as anywhere else; no licence is checked.
- The gate is at its own rank: no scaling down for a weak party (the spec rules out player-power and level scaling). Party-size scaling (ticket 27) still applies.
- Coming home and dying work as in any gate (ADR 0006, 0008).
- **The gate board** (developer, 2026-11-14): "you can only go to gates that you have found and are licensed for". The board sends the party only into gates that are both found and in a region the guild has opened for them. A gate beyond your licence has to be walked to every time. (Recorded as the board's rule; walking into an unlicensed gate at your own risk stays.)
- Still to settle: does a clear beyond your licence count for anything with the guild (promotion, story), or only the loot? Settled below.

**2026-11-14 (developer, recorded by agent):** A clear beyond your licence **counts, but with reduced rewards and XP from the guild.** The developer: "counts but reduced rewards and xp for the guilds".
- The clear is recorded in the host's world like any other (ADR 0008), and the guild knows about it.
- The guild pays less for it: a reduced clear reward and reduced XP. The reduction is a configuration value, not a code constant (spec: curves are configuration).
- **The reduced XP is the character's XP** (developer, 2026-11-14). No separate guild standing.
- Still to settle: by how much it is reduced, and whether loot from the gate itself is reduced too.
