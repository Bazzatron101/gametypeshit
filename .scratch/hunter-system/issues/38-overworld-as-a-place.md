# 38: The overworld as a third kind of place

**What to build:** The overworld as a place in the simulation next to the hub and the gates (ADR 0015, ADR 0008): leaving the guild town onto it, walking to a gate's entrance and going in with the ready check, and coming back out to the gate's entrance after a run. Players roam freely and apart.

**Blocked by:** 37: Give the simulation real ground

**Status:** ready-for-human (review; nothing to walk in a build until the content places gates in a world, tickets 39 and 45)

- [x] An overworld mode in `SimulationWorld` beside the hub and the gate, with its own ground (ticket 37)
- [x] The guild town's edge leads out onto the overworld and back
- [x] A gate's entrance in the world uses the board's ready check: everyone marks ready, only the host starts, and the whole party goes in wherever they stand (ADR 0015)
- [x] After a run the party comes back out at that gate's entrance
- [x] Walking near a hidden gate finds it; found gates and open regions are kept in the host's `WorldRecord` (ADR 0005), and guests see the host's world
- [x] A gate in a region that is not open can still be entered; its guild reward and XP are 25 % (a configuration value), loot is not cut (ADR 0015)
- [x] The gate board sends the party into found, licensed gates without walking there
- [x] A player who joins while the host is in the world appears next to the host; during a run, joining stays at the latest checkpoint (ADR 0003)
- [x] Tests: leave town, find a gate, enter with the ready check, clear, come back out; a late join in the world; the board only offers found gates

## Notes

- Regions opening by rank and story is ticket 25; this ticket only needs the record to hold which regions are open.
- A small test patch of ground is enough here; the full map is ticket 45.

## Comments

**2026-11-15 (developer, recorded by agent):** Two choices before building:
- **The 25 % cut applies to XP from kills** inside a gate whose region is not open. The clear reward's draw counts as loot and is not cut. The guild-pay cut waits for guild pay to exist.
- **The guild town is part of the overworld,** not a walled room: walk out of it freely. Scenes without an overworld keep today's walled hub.

**2026-11-15 (agent):** Built. EditMode suite: 597 passed, 0 failed.

- `Overworld.cs`: `OverworldDefinition` (gate sites, starting regions, entrance and find radii, `UnopenedRegionXpPercent` = 25) and `GateSite` (gate id, entrance, region, known from the start). `SimulationConfig.Overworld`; an overworld needs a hub (the town).
- `SimulationWorld.Overworld.cs`: `InOverworld`; found gates and open regions (`SetFoundGates`, `SetOpenRegions`, `IsGateFound`, `IsRegionOpen`, `IsGateLicensed`, `IsOnBoard`); finding a hidden gate by coming within `FindRadius` (a `GateFound` event); `GateEntranceNear` and `TryStartGateAtEntrance` (the ready check at an entrance, any gate, licensed or not); coming back out round the gate's entrance.
- With an overworld, the ground outside a gate is the world's (no town walls), the board and ready check work anywhere outside a gate, the board only starts found gates in open regions, and a joiner out in the world appears beside the host (`JoinPosition` is null there, and `JoinPoint` keeps them off ground they cannot stand on). During a run joining is still at the latest checkpoint.
- A run remembers its XP percentage (set as it starts), so host and guests cut the same; it is in snapshots, with found gates and open regions.
- The host's `WorldRecord` saves found gates and open regions (world save format 3; older saves still load). `SimulationRunner` loads them into the world and saves each find.
- `GateBoard`: lists only found, licensed gates; at a gate's entrance out in the world the same panel opens for that gate alone ("Press E at the gate"), with a note when the region is not open.
- Tests: `OverworldTest` (12), two world-save tests in `SaveFilesTest`, and `GateFound` in the codec test.

**Left for later tickets:**
- No content asset places gates in a world yet, and no scene has an overworld; ticket 39 (the test patch's gate markers) and 45 do that. `HubView` still draws the town's walls in a scene that has a hub.
- Nothing opens a region yet: ticket 25 calls `SetOpenRegions` (and saves into `WorldRecord.OpenRegions`).
- A gate with no site on the map is treated as known and licensed, so the board offers it as before.
- Licence is "the region is open" until ranks exist (ticket 25).
