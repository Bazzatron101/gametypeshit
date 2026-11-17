# 78: Shuffled farming gates

**What to build:** Repeatable farming gates build a new layout from their pieces every run, so farming stays fresh (spec story 43). Story gates keep their fixed layouts. Split from ticket 71.

**Blocked by:** 72: Encounters separate from rooms, 73: Gate layouts that turn, branch and loop

**Status:** ready-for-agent

- [ ] A gate is marked fixed or shuffled
- [ ] A shuffled gate lists the pieces it can draw from (room shapes, encounters, features) and rules (how many rooms, how many branches, where checkpoints go, the boss room last)
- [ ] The host rolls a seed when the run starts; guests build the same layout from it, and it rides in snapshots for late joiners (ADR 0003)
- [ ] Every rolled layout passes the content check; a layout that fails is re-rolled
- [ ] Each run gets a new layout (developer's default; change in triage if it should be daily or weekly)
- [ ] Party scaling (ticket 27) and solo-completability still hold
- [ ] Tests cover the same seed giving the same layout, many seeds all passing the check, and a guest joining mid-run
