# 38: The overworld as a third kind of place

**What to build:** The overworld as a place in the simulation next to the hub and the gates (ADR 0015, ADR 0008): leaving the guild town onto it, walking to a gate's entrance and going in with the ready check, and coming back out to the gate's entrance after a run. Players roam freely and apart.

**Blocked by:** 37: Give the simulation real ground

**Status:** needs-triage

- [ ] An overworld mode in `SimulationWorld` beside the hub and the gate, with its own ground (ticket 37)
- [ ] The guild town's edge leads out onto the overworld and back
- [ ] A gate's entrance in the world uses the board's ready check: everyone marks ready, only the host starts, and the whole party goes in wherever they stand (ADR 0015)
- [ ] After a run the party comes back out at that gate's entrance
- [ ] Walking near a hidden gate finds it; found gates and open regions are kept in the host's `WorldRecord` (ADR 0005), and guests see the host's world
- [ ] A gate in a region that is not open can still be entered; its guild reward and XP are 25 % (a configuration value), loot is not cut (ADR 0015)
- [ ] The gate board sends the party into found, licensed gates without walking there
- [ ] A player who joins while the host is in the world appears next to the host; during a run, joining stays at the latest checkpoint (ADR 0003)
- [ ] Tests: leave town, find a gate, enter with the ready check, clear, come back out; a late join in the world; the board only offers found gates

## Notes

- Regions opening by rank and story is ticket 25; this ticket only needs the record to hold which regions are open.
- A small test patch of ground is enough here; the full map is ticket 45.
