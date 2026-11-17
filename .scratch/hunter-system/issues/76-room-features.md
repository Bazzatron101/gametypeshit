# 76: Room features

**What to build:** Reusable things to place in rooms, each its own data piece: cover/pillars, traps/hazards, chests, and ledges/stairs. Split from ticket 71. Build in that order; each can land on its own.

**Blocked by:** none (parent: 71). Ledges/stairs use the ground model from ticket 37 (done)

**Status:** ready-for-agent

- [ ] A room lists its features with a position (and turn); features are reusable assets
- [ ] **Cover/pillars:** block movement, and block enemy shots (pairs with the goblin archer, ticket 64)
- [ ] **Traps/hazards:** spikes, fire floors, swinging blades; each telegraphs before it hurts, so it can be dodged; it can hurt enemies too
- [ ] **Chests:** opened by a player; each player gets loot from the gate's drop list (ticket 61), personal loot as the spec says
- [ ] **Ledges/stairs:** height changes inside a room that players and enemies walk up and down; a jump can reach a low ledge
- [ ] Snapshots and co-op carry feature state (a trap mid-swing, an opened chest); the host decides hits and loot
- [ ] Bot teammates walk round cover and avoid live traps
- [ ] New looks are placeholders labelled "AI"
- [ ] Tests cover each feature, its telegraph, and snapshots
