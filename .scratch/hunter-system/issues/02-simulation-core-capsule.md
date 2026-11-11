# 02: Simulation core with a movable capsule

**What to build:** A fixed-timestep simulation that takes per-player input commands and produces state, with a capsule moving on a greybox floor in the game, and the same movement reproduced by a headless test. Player state is addressable per player from the start (no singleton player), so single-player is the one-player case of the multiplayer model.

**Blocked by:** 01: Project scaffold

**Status:** ready-for-agent

- [ ] Simulation runs on a fixed timestep independent of rendering and the engine's scene objects
- [ ] Player input is expressed as commands per tick and the capsule moves in the game as a result
- [ ] A headless test feeds scripted commands and asserts the resulting position
- [ ] Player state is keyed per player, with no global player singleton
