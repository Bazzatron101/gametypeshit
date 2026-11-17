# 37: Give the simulation real ground

**What to build:** A ground model in the simulation (plain C#, no Unity physics) that answers the overworld's three questions: how high the ground is at (x, z), whether a spot can be walked on, and whether a line is blocked. Today the simulation assumes flat ground: landing sets height to 0 (`SimulationWorld.Jump.cs`, `Land`), and the hub and gates keep everyone inside a rectangle with `floor.Nearest` (`ConfineToHub`, the gate's walls). The overworld (ADR 0015) has hills, sea, steep mountains, passes and climbable faces (ticket 36), so none of that holds there.

**Blocked by:** None

**Status:** done

- [x] A `Ground` in the simulation gives height, walkability and a blocked-line test at any point; the hub and gates keep working through the same seam (their floors are flat, walled ground)
- [x] Walking, jumping and landing follow the ground's height; a player cannot walk onto unwalkable ground (too steep, the sea) and slides along its edge rather than stopping dead
- [x] Climbable faces are their own kind of ground, marked in the data rather than only in the art (ticket 36, "some are climbable")
- [x] The ground's data is plain data (a heightmap and a walkability grid) loaded the same way on the host and on clients, so prediction and reconciliation agree (ADR 0004); a test checks a client's predicted path matches the host's on sloped ground
- [x] `ReachBeforeWall` and other wall checks use the blocked-line test
- [x] EditMode tests on a small made-up heightmap: slopes, a cliff, the sea's edge, a climbable face, landing on a hill

## Notes

- The data is meant to be baked from the Unity Terrain at build time (ticket 39), so the art and the rules cannot drift apart. This ticket only needs a hand-made test heightmap.
- Grid resolution is a configuration value; about 1 m cells keep a 10.5 × 7.5 km map's walkability near 80 MB as bytes, so it will likely be read per tile with ticket 40 rather than all at once.
- Coordinates keep the origin at the map's centre (ADR 0015), so no floating origin.

## Comments

**2026-11-15 (agent):** Built. EditMode suite: 545 passed, 0 failed.

- `Assets/Scripts/Simulation/Ground.cs`: `IGround` (height, kind, `Confine(from, to)`, `ReachAlong`), `GroundKind` (walkable, blocked, water, climbable), and three kinds of ground: `OpenGround` (flat, endless: the arena and tests), `FloorGround` (flat rooms inside walls: the hub, and each gate through `GateLayout.Ground`), and `HeightmapGround` (corner heights blended between samples, a kind per cell, blocked off the grid).
- `SimulationConfig.Ground` holds the world's ground; null is open and flat. `SimulationWorld.Ground` is the ground the party is on now: the running gate's, else the hub's, else the config's.
- `SimulationWorld.Ground.cs`: each tick notes where everyone set off from, and at the end settles them: a move onto ground nobody can stand on slides along the edge (it keeps whichever along-the-axis part of the move it can make), and everyone is put at the ground's height; a jumping player keeps their height above it. This replaces `ConfineToGate` and `ConfineToHub`.
- Jumps rise from and land on the ground under the player; clearing an enemy compares the height above the ground. Added players and enemies, and placed players, stand on the ground at once (so a client's first prediction agrees with the host).
- Boss charges use `ReachAlong` and `Confine`, so a cliff stops one as a wall does.
- Tests: `GroundTest` (17: the heightmap, slopes, a cliff, sliding along it, the sea, the map's edge, a climbable face, not being trapped, jumps, landing uphill, placing, enemies, open ground and the hub), and `NetSessionTest.OnSlopedGroundTheClientsPredictionMatchesTheHost` (up a hill, into a cliff and along it with a jump: no correction).

**Left for later tickets:**
- In a game with a hub, the world's ground is not used yet, since there is no overworld mode. That is ticket 38.
- Climbable ground is just walkable for now. How climbing plays (speed, stamina, an animation) is not decided.
- Projectiles fly level at the height they were thrown from and pass through hills; nothing in this ticket asked for that to change.
- Ground data is held whole in memory. Reading it per tile comes with ticket 40.
- The slide works along the grid's axes, so following a diagonal cliff zigzags a little at 1 m cells. Watch for it once there is real terrain.

**2026-11-17 (agent, asked by the developer to close what can be closed):** Closed: the review was of the ground model, which the test patch, the continent and everything walking on them (tickets 38 to 45, 49) have been built and tested on since.
