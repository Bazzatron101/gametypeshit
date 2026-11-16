# 43: Roaming monsters and bots that cross real ground

**What to build:** The overworld's roaming monsters, and the movement they and the bot teammates need on ground that is not a flat room. Monsters leak from gates and match the gate near them, its rank and enemy types; a cleared gate stops leaking until a story event or the guild reopens it (ADR 0015). Bot teammates (ticket 33) must follow players across the world.

**Blocked by:** 37: Give the simulation real ground, 38: The overworld as a third kind of place

**Status:** ready-for-agent

- [ ] Each found or hidden gate leaks a configured number of monsters of its own types and rank around its entrance; a cleared gate stops
- [ ] Monsters exist only while their gate's tile is active (ticket 40)
- [ ] Monsters and bots move with steering over the walkability grid (ticket 37): around steep ground and water, through passes; Unity NavMesh is not used, since the simulation must run the same everywhere
- [ ] Monsters stay near their gate and give up a chase past a configured distance
- [ ] Bot teammates follow their player across the overworld, keep up on a mount, and regroup at a gate
- [ ] Tests: a leak's types match its gate, a cleared gate stops leaking, a monster walks around a ridge, a bot follows its player through a pass

**2026-11-16 (developer, recorded by agent):** Triaged: build now ("this still feels very basic").
