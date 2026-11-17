# 91: Towns develop when hunters claim bounties

**What to build:** A town grows when hunters claim a gate bounty at its guild. From the developer, 2026-11-17 (ticket 36): "the bounty will pay with gold and development of the location here the bounty was claimed/made". Today settlements are generated once from where they stand (tickets 82-89) and never change. This ticket gives each town a development level that rises with bounties claimed there and that shows in the town.

**Blocked by:** none yet. It needs bounties to claim, and nothing builds them so far. Cart protection, the gate search that narrows to a gate's exact point, and gate leaks (ticket 36) have no build tickets. Triage decides whether those get tickets first or this ticket starts with a placeholder way to raise a town's level.

**Status:** needs-triage

Draft, to settle in triage:

- [ ] Each town has a development level, starting at its generated look
- [ ] Claiming a gate bounty at a town's guild raises that town's development (and pays gold, ticket 36)
- [ ] Development shows in the town: what changes is still open (see below)
- [ ] Development belongs to the host's world (`WorldRecord`), like found gates and clears, so the whole party sees the same towns
- [ ] How much one bounty develops a town, and the levels, are configuration values
- [ ] Saves move up a format version; older worlds start every town at its first level
- [ ] Tests cover raising a town's level, saving and loading it, and the old-save upgrade

## Open questions

1. **What changes when a town develops?** More buildings, more trades (ticket 85), better shops or gear, a bigger reach, more people and life (ticket 87), more guild work on offer, or a mix.
2. **How many levels,** and how many bounties each one takes?
3. **Are bounties the only thing that develops a town?** Other candidates: protected carts, clears near the town, story events.
4. **Can a town go back down,** for example when a leak reaches its entrance, or does it only grow?
5. **The guild town (ticket 89),** the hub: does it develop the same way, or is it handled on its own?
6. **Generation:** does development add to the generated layout (new plots on its streets, ticket 83), or switch between prepared stages?
7. **Does the 25 % beyond-licence cut (ticket 36) apply** to a bounty's gold, its development, both, or neither?

## Comments
