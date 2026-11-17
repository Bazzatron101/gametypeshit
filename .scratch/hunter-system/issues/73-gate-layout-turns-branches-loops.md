# 73: Gate layouts that turn, branch and loop

**What to build:** Replace the straight line of rooms (ADR 0006) with a layout step that places rooms on a grid and joins them by doorways, so a gate can turn, branch into side rooms and dead ends, and loop (paths that rejoin). Split from ticket 71.

**Blocked by:** none (parent: 71)

**Status:** ready-for-agent

- [ ] A gate describes its rooms and how they connect (which doorway of one room leads to which room), not just an order
- [ ] A layout step places the rooms on a grid from that description; the result is the same on host and guests
- [ ] The floor is still the union of rooms and doorways; players and enemies cannot leave it
- [ ] Checkpoints, room wake-up, room clearing, the boss room and the gate clear all work with branches (a side room need not be cleared to clear the gate)
- [ ] The content check catches broken gates: rooms that overlap, doorways that lead nowhere, rooms that can't be reached, a boss room that isn't at an end, a gate with no checkpoint
- [ ] A straight-line gate still works, so existing gates don't break before ticket 79 converts them
- [ ] Bot teammates find their way through turns, branches and loops
- [ ] Update ADR 0006 (or add a new ADR) for the new layout rules
- [ ] Tests cover placement, a branch, a loop, and each content check failure
