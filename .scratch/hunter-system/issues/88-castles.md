# 88: Castles with walls, a gatehouse and a town inside

**What to build:** Make castles proper strongholds. Split from ticket 82. The developer chose all four:
- **Stone walls and a gatehouse:** a walled ring with corner towers, and a gate the road goes through.
- **A town inside the walls:** houses, a market and a smithy round the keep.
- **A bigger keep:** taller and grander, seen from far off.
- **Houses outside the walls:** farms and cottages round the walls (ticket 86 for the fields).

**Blocked by:** 83: Streets instead of rings, 84: More building kinds (parent: 82)

**Status:** ready-for-agent

- [ ] A castle's curtain wall follows the ground round its keep and inner town, with towers at the corners and along long runs
- [ ] A gatehouse where each road meets the wall; the road runs through it and stays walkable. The walls themselves block (footprints), so the only ways in are the gates
- [ ] A bigger keep (placeholder, labelled AI), drawn far enough out to be a landmark
- [ ] Inside: a small town on streets (ticket 83) with a market, a smithy and houses
- [ ] Outside: cottages and farms round the walls
- [ ] Walls never cut a road, a gate entrance or the guild's board; a castle on a slope still closes its ring
- [ ] Tests: every road into a castle passes through a gate; inside can be reached from outside only through gates
