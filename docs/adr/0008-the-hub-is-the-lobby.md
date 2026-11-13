# 0008: The hub is a place in the host's world, and the lobby

**Status:** Accepted (2026-11-13)

## Context

The spec asks for "a small, shared, host-owned hub" with a gate board, where up to 10 players gather
and which "doubles as the lobby" (stories 40, 48 and 52). Ticket 19 had to decide how a party gets
from the hub into a gate and back. The developer chose three points on 2026-11-13.

## Decision

- **The hub is a place in the simulation,** like a gate. It is a walled floor laid out away from the
  gates (gates run along +Z from the origin; the hub sits elsewhere). While no gate is running the
  party is in the hub. It is in the game's content and switched on per scene: the co-op scene uses it;
  the greybox and the test arena stay open fields.
- **Joining:** a player who joins while the party is in the hub appears in the hub; during a run, at the
  latest checkpoint (ADR 0003).
- **The gate board** stands in the hub. Walking up to it opens a list of the gates (marking those the
  host's world has cleared), the party, and who is ready.
- **Ready check** (developer's choice): each player marks themselves ready. Readiness is player input,
  sent as a command and kept in the simulation, so everyone sees it. It exists only in the hub and is
  cleared on the way into a gate, on the way home, and when a player's connection drops (coming back,
  they choose again). Away players do not hold the party up.
- **Only the host starts a gate** (developer's choice), and only when everyone present is ready. The
  whole party goes in together, wherever they stand.
- **Coming home** (developer's choice): after a clear, a portal opens in the boss's room (or the last
  room of a gate with no boss). A living player walking into it brings the whole party home; anyone
  already standing on its spot when it opens must step out first (as with checkpoints), so the clear
  and its reward are seen before anyone goes. If nobody walks in, a countdown (30 s) brings them home.
  The host can also leave a run at any time (two clicks), cleared or not. Either way everyone stands in the hub on their feet, and nothing from the gate follows them. Coming
  home never costs the character.
- **Host-world progress:** the host's world records each gate it clears (ADR 0005's `WorldRecord`),
  saved at once. The simulation holds the list too (loaded from the host's save when hosting), so the
  board shows every player which gates the host's world has cleared. Guests' own progress is theirs
  only through their characters.

## Consequences

- The hub and every gate live in one world, so one snapshot, one set of walls and one join rule serve
  both. Leaving a gate is a teleport: a guest's prediction is corrected by the full distance, which
  shows up in correction measurements without being a misprediction.
- Starting a gate needs everyone ready, so one idle friend blocks the party. The host's answer is to
  wait or ask them; there is no kick yet.
- Storage, the shop (stories 50 and 51) and the mission board's intel (ticket 20) are not in the hub yet.
- Party-size scaling (27) applies on the way in; nothing about the hub changes for it.
