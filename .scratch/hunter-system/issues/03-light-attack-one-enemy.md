# 03: Light attack and one enemy

**What to build:** The player can attack with a light attack, and one enemy type chases the player, takes damage, and dies. This is the first fight and it is verifiable headlessly.

**Blocked by:** 02: Simulation core with a movable capsule

**Status:** ready-for-agent

- [ ] Light attack on the mouse button deals damage in the simulation, with damage events emitted
- [ ] One enemy type chases the player and is defeated when its health reaches zero
- [ ] A headless test asserts enemy defeat after the expected number of hits
