# 75: New room shapes

**What to build:** Room pieces in more shapes than plain rectangles: for example L-shapes, round chambers, and long halls with side alcoves. Split from ticket 71.

**Blocked by:** 73: Gate layouts that turn, branch and loop

**Status:** ready-for-agent

- [ ] A room's floor can be made of several rectangles (L-shapes, alcoves) or a circle, as data
- [ ] A room says where its doorways are, so the layout step (ticket 73) can join it
- [ ] The simulation's ground (ticket 37) keeps players and enemies inside the shape
- [ ] At least four new pieces: an L-room, a round chamber, a hall with alcoves, and a T-junction for branches
- [ ] Gate geometry is still built from data at run time; new looks are placeholders labelled "AI"
- [ ] Tests cover containment in each shape and doorways lining up
