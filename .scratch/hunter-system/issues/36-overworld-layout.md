# 36: Lay out the overworld

**What to build:** The layout of the walkable world the game is set in: where the guild town sits, which regions there are, what opens each one, where the gates stand, and how big it is. This replaces the spec's "gates are entered through the hub menu" (see ADR 0015, accepted). Layout only; building terrain, streaming and mounts are later tickets.

**Blocked by:** None

**Status:** ready-for-human (open questions below)

- [x] Scale settled: ~10.5 × 7.5 km on the drawn map, ~40 minutes walking (2026-11-15; first aimed for an hour, ~12 × 7 km); grow if playtests say it is too close
- [x] Region list, order and what opens each one settled (first sketch, 2026-11-14; region sketch 2026-11-15)
- [x] Gate placement per region sketched (story gates and farming gates): gates sit at each region's important places; region sketch kept 2026-11-15
- [x] What physically stops a player entering a region that is not open yet: nothing physical; danger and no guild work there (see Comments)
- [x] Mounts: when you get one and how fast it is: at the D-rank promotion; 2× sprint, 16 m/s (2026-11-15)
- [x] ADR 0015 accepted, and the spec's non-goal and stories 40 and 48 rewritten (2026-11-15)

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

~~A mount at 3× sprint (24 m/s) crosses the walking-size map in about 12 minutes.~~ Settled at 2× sprint (16 m/s), about 19 minutes (see Comments).

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

**2026-11-15 (developer, recorded by agent):** A clear beyond your licence pays **25 %**: the guild's clear reward and the character's XP for it are a quarter of normal. Kept as a configuration value. Loot dropped inside the gate is not cut (settled below).

**2026-11-15 (developer, recorded by agent):** **Where gates stand and how far leaks reach.** The developer: "The gates are centred around important things in the regions so they will reach to either near the entrance of the town or hits the roads to attack supply carts".
- **Gates sit at a region's important places** (its landmarks, mines, shrines, crossings), not scattered at random. Each region's gate layout starts from its list of important places.
- **A leak reaches outward until it meets something people use:** it can come right up near a town's entrance, or reach the road.
- **Supply carts** travel the roads between towns, and leaked monsters attack them. New: carts are NPC traffic on the roads, and a road near an active gate is where you meet trouble.
- Still to settle: ~~whether you can see where a leak is~~ (settled below: no), ~~whether clearing a gate stops its leak for a while~~ (settled below: yes), and what happens when a cart is attacked (settled below: a reward for defending).

**2026-11-15 (developer, recorded by agent):** An attacked supply cart is **a reward for defending it.**
- Saving a cart pays a reward; losing one costs nothing beyond the missed reward. Towns do not run short.
- The reward is a configuration value. Still open: who in a co-op party gets it (everyone nearby, as with clears?), and whether it is coin, XP or items.

**2026-11-15 (developer, recorded by agent):** A leak is **not visible.** No scorched ground or darker sky: you find out when its monsters show up.
- The gate itself can still be seen; only how far its leak reaches is hidden.
- The board's intel (ticket 20) is the way to learn about a leak before meeting it, if intel covers leaks at all (to decide with ticket 20).

**2026-11-15 (developer, recorded by agent):** Clearing a gate **stops its leak for a while.**
- After a clear, the gate's leaked monsters stop coming and the ground around it (the road, the town's entrance) is safe; then the leak starts again.
- How long is a configuration value. Suggested: measured in play time in the host's world, so it does not run out while nobody is playing.
- Clearing a gate near a road or a town is worth doing for the safety, not only the loot.
- Still open: does the pause belong to the host's world (everyone sees the road safe), as with other world progress? Suggested yes (spec story 71).

**2026-11-15 (developer, recorded by agent):** Loot inside a gate beyond your licence is **full loot.** Only the guild's clear reward and the XP are cut to 25 %; what drops in the dungeon is unchanged. So going past your licence is a risk that can still pay in items.

**2026-11-15 (developer, recorded by agent):** Three more.

**Cart reward.** The developer: "All player that protect the cart can get information with where the gate is or gold".
- Every player who helped protect the cart gets the reward, not only the host or whoever landed the last hit.
- The reward is either a gate's location (it becomes found, see below) or gold. Who gets which is random (settled below).

**How long a cleared gate stays quiet.** The developer: "Until a story event happens for it to open or it will be unlocked for a harder challenge at the guild".
- A cleared gate stops leaking and stays quiet with no timer. It wakes again only when:
  - **a story event** reopens it, or
  - **the guild** reopens it as a harder challenge (a higher-rank version of the same gate, offered at the guild).
- This replaces the suggested play-time timer. The quiet state belongs to the host's world, like clears.
- A harder reopened gate leaks again at its new strength (settled below).

**Finding gates.** The developer: "The gates will be hidden for the player until they find it for most of them but for some the guild already knows where they are".
- Most gates are **hidden** (not on the map, not on the board) until a player walks to them, or learns where one is from a cart reward.
- **Some gates the guild already knows:** they count as found from the start (or from when their region opens) and show on the board and map.
- Which gates are guild-known is set per gate in its data: story gates and each region's first gate (settled below).
- Found gates belong to the host's world (`WorldRecord`), so a guest's cart reward reveals the gate for the whole party.

**2026-11-15 (developer, recorded by agent):** The guild knows **the story gates and each region's first gate.**
- They count as found once their region opens, and show on the board and the map, so the way forward is never hidden.
- Every other gate (side gates, farming gates) is hidden until walked to or learned from a cart.
- In a gate's data this is one flag ("known to the guild"), set on story gates and on the first gate of each region.

**2026-11-15 (developer, recorded by agent):** The cart reward is **random**, rolled for each player who protected it: gold, or a hidden gate's location.
- The chance of each is a configuration value.
- If no hidden gate is left to reveal (suggested: in the cart's region), the roll gives gold.
- A revealed location is world progress, so one player's roll reveals the gate for the whole party.

**2026-11-15 (developer, recorded by agent):** A gate the guild reopens as a harder challenge **leaks again, at its new strength.**
- Its leaked monsters match the harder version (rank and enemies), so a road that was safe in early ranks can turn deadly later. This is how A-rank's "old ground turns dangerous" works.
- A story event that reopens a gate works the same way: the reopened gate leaks at whatever strength the story sets.
- Clearing the harder version quiets it again, until the next reopening.

**2026-11-15 (agent, for the developer to change freely):** **Region sketch: towns, important places and gates.** All names are placeholders. Distances are on the ~12 × 7 km map, measured from the west edge (x) and the south edge (y). The main road runs west to east at about y 3.5 km, with short spurs to towns off it. "Known" = known to the guild (story gates and each region's first gate); "hidden" = found by walking or from a cart.

*Greenmarch* (E, open from the start; x 0–3.5 km). Farms, woods and a river ford.
- **Town:** the guild town (the hub), x 0.5 km, on the road.
- **The old mill** → *Mill cellar*: the first gate, known, story. 1 km from town. Small, cramped and quiet (ticket 16's tone).
- **The barrow hill** → *Barrow*: known, story; its clear leads to the D-rank promotion.
- **Thornwood** → *Thornwood hollow*: hidden, farming.
- **Greywater ford** (where the road crosses the river) → *Ford gate*: hidden. It leaks onto the road at the ford: the first place carts get attacked.

*Fenward mire* (D promotion; south of the road, x 3.5–6.5 km). Marsh, peat, drowned buildings.
- **Town:** Stiltmoor, on stilts at the mire's edge, a short spur south of the road.
- **The drowned chapel** → *Drowned chapel*: first gate, known.
- **Heron isle** → *Heron isle*: known, story.
- **The peat cutters' camp** → *Peat camp*: hidden. Close to Stiltmoor, so its leak reaches the town's entrance.
- **The sunken causeway** → *Causeway gate*: hidden. Leaks onto the road.

*Brokenstone hills* (a story beat during C; north of the road, x 3.5–6.5 km). Quarries, ruins, old mines.
- **Town:** Quarrygate, at the foot of the hills, a short spur north of the road.
- **The great quarry** → *Great quarry*: first gate, known.
- **The collapsed mine** → *Deep mine*: known, story (the beat that opens the region happens here or leads here).
- **The watchtower ruins** → *Watchtower*: hidden. Overlooks the road; leaks onto it.
- **The cairn field** → *Cairns*: hidden, farming.
- C-rank before the story beat: the guild offers C-rank versions of found gates in the Greenmarch and Fenward mire (harder reopenings), so C has work before its region opens.

*Ashen steppe* (B promotion; south of the road, x 6.5–9.5 km). Open grass, burnt stone, room for mounts to run.
- **Town:** Ember crossing, a caravan town on the road. Most supply carts start or stop here.
- **The standing stones** → *Stone ring*: first gate, known.
- **The old battlefield** → *Battlefield*: known, story. A good place for the first raid gate (ticket 29).
- **The dry riverbed** → *Riverbed gate*: hidden. Leaks onto the road.
- **The herd grounds** → *Herd grounds*: hidden, farming.

*Saltreach coast* (B specialty quests, ticket 26; north of the road, x 6.5–9.5 km). Cliffs, a harbour, sea caves.
- **Town:** Saltreach harbour, on a spur north to the coast.
- **The lighthouse** → *Lighthouse*: first gate, known.
- **Four specialty quest gates,** one per class (Warrior, Rogue, Archer, Wizard), all known, story. Each sits at its own place: the cliff monastery, the smugglers' caves, the clifftop eyrie, the drowned library.
- **Wreck cove** → *Wreck cove*: hidden, farming.

*Riftlands* (S story; x 9.5–12 km). Waits on the story hook (ticket 16) for its look and places.
- **Town:** Farwatch, the guild's last outpost, at the road's end.
- **Three story gates, all known:** an outpost gate, one deeper in, and the Rift itself at the far east.

*A-rank:* no new region. The guild reopens found gates across the map as A-rank challenges, and they leak at their new strength.

**Count:** 25 gates (4 + 4 + 4 + 4 + 6 + 3; the raid can be the battlefield gate). Every one can be reopened harder, which is how the spec's 40 to 60 gates are reached without building that many places.

Still to settle: names, which places carry which story beats (waits partly on the hook, ticket 16), and the raid gate's place.

**2026-11-15 (developer, recorded by agent):** The region sketch is **kept** as the starting layout. More towns may be added later ("we may add more towns in the end"), so the layout should leave room for them: one town per region is the minimum, not a rule.

**2026-11-15 (developer, recorded by agent):** A mount runs at **2× sprint: 16 m/s.** The ~18 km road takes about 19 minutes mounted (an hour walking, ~38 minutes sprinting). This replaces the 3× (24 m/s, ~12 minutes) used in the earlier notes. Kept as a configuration value, like walking and sprint speed.

**2026-11-15 (agent, for the developer):** **Region sketch redone on the developer's map** (`docs/maps/overworld-reference.webp`). This replaces the rectangle sketch's geography; every rule above stays. Positions are given as places on the drawing. Crossed swords on the drawing are gate sites; the in-game map would only show gates that are found.

*Map changes that come with the drawing:*
- **Shape and size:** the land is about 4:3, so about **10.5 × 7.5 km** keeps the area and the hour on foot (the roads wind).
- **The hub is on the west island,** joined to the mainland by the bridge. Still the west edge; a small, safe start with the world across the bridge.
- **The Riftlands are the south-east island,** reached by ship from the south port. The ship sails when the S story says so.
- **More towns:** the drawing has about 25 settlements and 6 castles. About 8 are guild towns (listed below); the rest start as villages and landmarks and can be upgraded later.
- **New ground:** the snowy mountains (centre-south) and the south peninsula with its port. Put with the Riftlands for now, as the way to the ship (see the open question at the end).

| Region | Where on the map | Opens | Guild town(s) | Gates (known / hidden) |
| --- | --- | --- | --- | --- |
| **Greenmarch** | West island, the bridge, the north-west lobe and the west-central plain | Start | **Guild town** (the hub: the blue-spired town on the west island); **Westkeep** (the castle on the west-central plain) | Mill cellar: island crossed swords, first gate, known, story. Barrow: crossed swords in the north-west mountains, known, story (to D). Bridge gate: crossed swords at the bridge, hidden, leaks onto the bridge road (first cart attacks). Thornwood hollow: crossed swords on the north-west coast, hidden, farming. |
| **Fenward mire** | South-west lobe, inside the west-coast mountain range | D promotion | **Stiltmoor** (the town in the middle of the south-west lobe) | Drowned chapel: crossed swords at the far south-west, first, known. Heron isle: known, story (not drawn yet). Peat camp: hidden, near Stiltmoor, leaks to its entrance. Causeway gate: crossed swords where the road leaves the west-central plain, hidden, leaks onto the road. |
| **Brokenstone hills** | The diagonal mountain range across the middle | Story beat during C | **Quarrygate** (the castle north-east of the range) | Great quarry: crossed swords in the middle of the range, first, known. Deep mine: crossed swords at the range's north end, known, story. Watchtower: crossed swords at the Quarrygate castle, hidden, leaks to its entrance. Cairns: crossed swords at the range's south end, hidden, farming. |
| **Ashen steppe** | Eastern plains, south of the north-east mountains | B promotion | **Ember crossing** (the town in the south-east of the plains) | Stone ring: crossed swords in the east plains, first, known. Battlefield: crossed swords below the north-east mountains, known, story; the raid gate (ticket 29). Riverbed gate and herd grounds: hidden. |
| **Saltreach coast** | North-east lobe and the east coast | B specialty quests | **Saltreach** (the castle on the east coast) | Lighthouse: crossed swords in the far north-east, first, known. Four specialty quest gates (one per class), known, story: in the north-east mountains and along the coast (not drawn yet). Wreck cove: hidden. |
| **Riftlands** | Snowy mountains, the south peninsula, and the south-east island | S story | **Southport** (the port town with the ship); **Farwatch** (the castle on the south-east island) | Snow pass: crossed swords in the snowy mountains, known, story. South gate: crossed swords south of the snow, hidden. The Rift: Farwatch's crossed swords on the island, known, story (the end). |

*A-rank:* still no new region; the guild reopens found gates across the map, harder.

Still the same count (about 25 gates) and every gate can be reopened harder. Some known gates are not on the drawing yet (Heron isle, the four specialty gates); they need crossed swords added.

**Settled (kept for S, see below):** the snowy mountains and south peninsula are a lot of ground to leave to the S story. Alternative: they open with a story beat during A, so A-rank gets one region after all, and only the island stays for S.

**2026-11-15 (developer, recorded by agent):** The snowy mountains and the south peninsula are **kept for the S story**, with the south-east island: the Riftlands are all three. A-rank still opens no new region. Like everywhere else they have no walls; until the S story they are simply too dangerous and the guild gives no work there.

**2026-11-15 (developer, recorded by agent):** **Size kept at about 10.5 × 7.5 km for now;** make it bigger if it feels too close in playtests.
- Correction to the earlier note: on the drawn map the longest road (west island to the east coast) is roughly 11–13 km, so a crossing is about **40 minutes walking**, ~25 sprinting, ~13 mounted, not the hour first aimed for.
- About 60–70 places (31 settlements, ~25 gates, landmarks) over ~50 km² of land: a new place about every 850 m, ~3 min walking or ~1 min mounted.
- Gaps are filled with cheap things the design already has: supply carts, leaked monsters, camps, shrines, ruins and viewpoints.
- **Playtest check:** if it feels too close, grow the map (scale the terrain; keep the drawing). Winding the roads more is the cheaper way to add travel time.

**2026-11-15 (developer, recorded by agent):** The game ends with the player helping the continent's best adventuring guild beat an S-rank dungeon (ticket 16). On this map that dungeon is the Rift on the south-east island, at the end of the Riftlands. There are several guilds on the continent; where the best guild is based is still open (Farwatch, the castle on the island, is a candidate, or one of the mainland strongholds).

**2026-11-15 (developer, recorded by agent):** After the main campaign's end (the demon king's death, ticket 16), **the gates slowly close.** The overworld calms down: leaks stop as their gates close. A closed gate stops leaking but can still be replayed (ticket 16).

**2026-11-15 (developer, recorded by agent):** After the demon king, **demon princes rise up** (the S story's enemies, ticket 16), and **other lands** with their own problems lie off this map (later content). The Riftlands' S gates are suggested to be the princes' gates.

**2026-11-15 (developer, recorded by agent):** **Fast travel: free waystones.**
- Touch a waystone once and it is yours; after that, travel between your waystones for free at any time.
- Suggested, to confirm: one in each guild town and a few at crossroads and region edges; none inside dungeons.
- Suggested: waystones are the host's world's progress (like found gates), and travelling moves the whole party, as the board does. Or each player travels alone; to settle.

**2026-11-15 (developer, recorded by agent):** **Mountains: some are climbable.**
- Most mountains are too steep and are crossed through passes and roads.
- Marked cliffs and paths can be climbed, leading to hidden gates, viewpoints and shortcuts.
- Only the climbable parts need to be playable on their slopes, which keeps the terrain work down. The marking should read clearly (ledges, vines, a different rock).

**2026-11-15 (developer, recorded by agent):** **The sea: shallows only.**
- Players can wade in shallow water near the shore; deep water turns them back. No swimming, no small boats.
- The coast is the edge of the world. The bridge joins the west island to the mainland, and the ship to the south-east island is the only way across the sea (it sails when the S story says so).

**2026-11-15 (developer, recorded by agent):** **The demon king's gate is hidden under Saltreach,** the castle on the east coast.
- It is the main campaign's final dungeon (A-rank, story), the last stronghold before the sea and the Riftlands.
- Players spend B-rank in Saltreach (the specialty quests) without knowing what is beneath it. The gate is hidden, and stays hidden until the A-rank story reveals it.
- After the demon king dies, his gate closes like the others but stays replayable (ticket 16).

**2026-11-15 (developer, recorded by agent):** **Guilds: yours, plus rivals.**
- The player's guild has branches in most towns. A few towns belong to rival guilds, which have their own quests (and their own view of you).
- The best guild on the continent has its seat in **the north-west castle** (developer, 2026-11-15), in the Greenmarch's north-west mountains near the start. You see them early and look up to them all game; at the S story you help them.
- To settle: which towns are rivals', whether the 25 % rule for unlicensed clears and the "found and licensed" board rule apply the same at a rival's board, and whether rival guilds also hand out promotions.

*Strongholds after these answers (the drawing's five castles):* the north-west castle (the best guild's seat, a rival guild), Westkeep (your guild), Quarrygate (your guild), Saltreach (your guild; the demon king's gate hidden beneath), Farwatch (on the south-east island, S story).

**2026-11-15 (developer, recorded by agent):** **The smaller map questions.**
- **Waystones:** each player travels alone (matches free roam; the gate's ready check regroups the party). Placement: one in each guild town plus a few at crossroads and region edges; none in dungeons.
- **Villages:** each of the ~23 villages has an inn and one quest giver with a small quest.
- **Confirmed:** a paused leak belongs to the host's world (everyone sees the road safe); closed gates look sealed after the main game but stay replayable; the first raid gate is the battlefield in the Ashen steppe.
- **Rival guilds:** Stiltmoor (Fenward mire) and Ember crossing (Ashen steppe) are run by rival guilds, as is the best guild's north-west castle. Your own guild runs the other guild towns.
- **Standing with rival guilds** sets their pay: it starts at **50 %** with low standing and rises as standing grows. Standing is raised by **their own quests only** (each rival guild has a quest line). Your own guild pays in full. This adds a standing per rival guild, which the earlier "no separate guild standing" did not have (that was about the XP cut for unlicensed clears, which stays on character XP).
  - Still to settle: what full standing pays (100 %?), whether the rival's pay and the 25 % unlicensed rule stack (25 % × 50 %), and whether only your own guild promotes you.
- **Gate spots:** the crossed swords on the drawing are where the developer thinks gates can go ("the sword crossing and the places where I thinking gates can go"). There are 16 crossed swords; the region sketch has about 25 gates plus the demon king's hidden gate under Saltreach (not drawn, on purpose). The gates without crossed swords (Heron isle, the four specialty gates, and the hidden peat camp, riverbed, herd grounds and wreck cove) need spots from the developer.
- **Names:** the developer will write them. Placeholders stay until then.
