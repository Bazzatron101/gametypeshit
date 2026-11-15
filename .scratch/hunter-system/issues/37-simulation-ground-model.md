# 37: Give the simulation real ground

**What to build:** A ground model in the simulation (plain C#, no Unity physics) that answers the overworld's three questions: how high the ground is at (x, z), whether a spot can be walked on, and whether a line is blocked. Today the simulation assumes flat ground: landing sets height to 0 (`SimulationWorld.Jump.cs`, `Land`), and the hub and gates keep everyone inside a rectangle with `floor.Nearest` (`ConfineToHub`, the gate's walls). The overworld (ADR 0015) has hills, sea, steep mountains, passes and climbable faces (ticket 36), so none of that holds there.

**Blocked by:** None

**Status:** needs-triage

- [ ] A `Ground` in the simulation gives height, walkability and a blocked-line test at any point; the hub and gates keep working through the same seam (their floors are flat, walled ground)
- [ ] Walking, jumping and landing follow the ground's height; a player cannot walk onto unwalkable ground (too steep, the sea) and slides along its edge rather than stopping dead
- [ ] Climbable faces are their own kind of ground, marked in the data rather than only in the art (ticket 36, "some are climbable")
- [ ] The ground's data is plain data (a heightmap and a walkability grid) loaded the same way on the host and on clients, so prediction and reconciliation agree (ADR 0004); a test checks a client's predicted path matches the host's on sloped ground
- [ ] `ReachBeforeWall` and other wall checks use the blocked-line test
- [ ] EditMode tests on a small made-up heightmap: slopes, a cliff, the sea's edge, a climbable face, landing on a hill

## Notes

- The data is meant to be baked from the Unity Terrain at build time (ticket 39), so the art and the rules cannot drift apart. This ticket only needs a hand-made test heightmap.
- Grid resolution is a configuration value; about 1 m cells keep a 10.5 × 7.5 km map's walkability near 80 MB as bytes, so it will likely be read per tile with ticket 40 rather than all at once.
- Coordinates keep the origin at the map's centre (ADR 0015), so no floating origin.
