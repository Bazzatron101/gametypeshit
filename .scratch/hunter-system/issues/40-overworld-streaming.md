# 40: Load the overworld in tiles around players

**What to build:** Streaming for the overworld (ADR 0015, "streaming is needed"). Each client loads the terrain tiles and props around its own player and unloads far ones. The host simulates the tiles around every player, up to 10 spread across the map, not only the tiles around itself.

**Blocked by:** 38: The overworld as a third kind of place, 39: Terrain pipeline and a 1 km test patch

**Status:** needs-triage

- [ ] Clients load tiles within a configured radius of the local player and unload tiles past a larger one, with no hitch a player notices
- [ ] The host keeps active every tile near any player; ground data (ticket 37) and gate leaks for a tile exist only while it is active
- [ ] Loading is far enough ahead at mount speed (16 m/s, ticket 42), not only walking speed
- [ ] A player never stands or lands on a tile whose ground is not loaded yet (they wait, or the tile loads first)
- [ ] The host's memory with players spread apart is measured and recorded
- [ ] A PlayMode test: two players walking apart load and unload the right tiles on host and client

## Notes

- Additive scenes per tile or Addressables both fit; pick one in this ticket and record it in an ADR.
