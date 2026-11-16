# 49: Buildings and environment for the overworld

**What to build:** Buildings, props and plant life so the overworld looks good and does not feel empty. From the developer's playtest of the test patch, 2026-11-15: the open world "will need buildings and environment to look good". Today the town has only the board and the ground is bare (ticket 39).

**Blocked by:** 39: Terrain pipeline and a 1 km test patch

**Status:** ready-for-agent

- [ ] Decide where the art comes from (asset store packs, made by hand, or placeholders first) and the art style
- [ ] The guild town has buildings
- [ ] The test patch has trees, rocks and grass, enough to judge the look and how big the world feels
- [ ] A way to dress the rest of the map that scales to ticket 45

## Comments

**2026-11-15 (agent):** Opened from the playtest note on ticket 39. Ticket 36 notes that the map felt big at the current speed; dressing the ground may change that feeling.

**2026-11-16 (developer, recorded by agent):** Triaged: build now.
- **Placeholders first:** nature (trees, rocks, grass) and buildings for the towns, which are empty today.
- **Tag every placeholder art file with the Unity asset label `AI`,** so the developer can find what to replace (in the Project window, search `l:AI`). This applies to any art file an agent makes, not only this ticket's.
- Art style: the low-poly, flat-shaded look of ADR 0002.

**2026-11-16 (developer, recorded by agent):** Triaged: build now ("this still feels very basic").
