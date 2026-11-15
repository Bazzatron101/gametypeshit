# 0015: A walkable overworld

**Status:** Accepted (2026-11-15; proposed 2026-11-14)

## Context

The spec lists "Open-world streaming. Gates are entered through the hub menu" as a non-goal, and story 40
says "I want to select gates from the hub menu, so that I do not spend time on travel I do not enjoy". On
2026-11-14 the developer chose a walkable overworld instead: you walk to the gates, mounts make travel
faster, and a crossing takes about an hour. Ticket 36 holds the layout.

## Decision

- **The overworld is a third kind of place in the simulation,** next to the hub and the gates (ADR 0008).
  The guild town (the hub) sits on its west edge. Dungeons stay separate places. A gate's entrance in the world
  uses the board's ready check (ADR 0008): everyone marks ready, only the host starts, and the whole party
  goes in wherever they stand (developer, 2026-11-14).
- **No walls between regions** (developer, 2026-11-14). The whole map can be walked from the start; a
  region that is not open is too dangerous, and its town's guild gives you little or no work there.
  "Opening" a region means its guild starts giving you quests. A party may still go through any gate it
  reaches, at its own risk, with no licence check and no scaling down (developer, 2026-11-14). Such a clear
  counts, but the guild's clear reward and the character's XP for it are 25 % of normal; loot inside the gate is not cut (developer,
  2026-11-15), a configuration value.
- **Regions open by rank promotions and story beats,** recorded in the host's world (ADR 0005's
  `WorldRecord`), with discovered gates. Guests see the host's world, as with gates today.
- **The gate board stays:** it gives intel and sends the party into gates the host's world has already
  found and is licensed for (developer, 2026-11-14). A gate is found by walking to it; after that the board saves the trip.
- **Players roam freely** and regroup at a gate to go in together.
- **Joining in the overworld:** a player who joins while the host is out in the world appears next to the
  host (developer, 2026-11-14). During a run they still join at the latest checkpoint (ADR 0003).
- **Light danger:** a few roaming monsters outside dungeons. They leak from gates and match the gate near
  them, its rank and enemy types (developer, 2026-11-14), so danger follows the gates rather than the region.

## Consequences

- The spec's non-goal and stories 40 and 48 need rewriting.
- **Streaming is needed.** At this size the world cannot all be loaded: terrain in tiles around each
  player, and the host simulates the tiles around every player, up to 10 spread apart.
- **Snapshots need interest management:** each client gets only what is near it (ties to ticket 31's
  bandwidth work).
- **Size:** about 10.5 × 7.5 km on the drawn map (`docs/maps/overworld-reference.webp`), about 40 minutes to cross walking; to grow if playtests find it too close (developer, 2026-11-15). First aimed for an hour, about 12 × 7 km
  (developer, 2026-11-14).
- **Coordinates:** keep the world's origin at the map's centre. Nothing is then more than ~7 km out, where
  float precision stays near a millimetre, so no floating origin is needed.
- **Content cost is the main risk.** Terrain must be mostly generated, with hand-placed gates, camps,
  landmarks and the road. This competes with the 40 to 60 gates the spec plans for.
- Mounts become part of the character (ADR 0005). The first comes with the D-rank promotion (developer,
  2026-11-14), so a guest still at E-rank has none and walks; there is no riding double (developer, 2026-11-14).
- **Gates are hidden until found,** by walking to them or from a supply cart's reward; some are known to
  the guild from the start: the story gates and each region's first gate (developer, 2026-11-15). A cleared gate stops leaking until a story event or
  the guild reopens it as a harder challenge. All of this is the host's world's.
