# 0006: Gates are chains of kit rooms; the fallen return at checkpoints

**Status:** Accepted (2026-11-13)

## Context

The spec asks for gates "built from a modular room kit" with checkpoints, three solo revives and
then a restart, and a co-op rule for fallen players (stories 41, 47, 54 to 56). ADR 0003 added
joining and rejoining at the latest checkpoint. Ticket 17 had to turn these into rules precise
enough to simulate, and four points were not written anywhere. The developer decided them on
2026-11-13; two of the answers changed what the ticket and the spec said.

## Decision

**Rooms**
- A gate is the entrance to a dungeon: an ordered list of room pieces from a kit. Each piece is a
  rectangle with the enemies that wait in it and, optionally, a checkpoint. Pieces are reused across
  gates and within one.
- Rooms run end to end in a straight line from the entrance, centred on one line. A narrow piece
  between two wide ones is a corridor. Turns and branches are not supported yet.
- The gate's floor is the union of its rooms. Players and enemies cannot leave it; that is all a wall
  is. **The way onward is never locked** (developer's decision): players may run past a fight.
- A room's enemies appear when a living player first steps into it, and may chase anywhere in the
  gate. A room is cleared when its enemies are all defeated; the gate when every room is.
- Three seconds after a room is cleared its defeated enemies are removed from the world, so they do
  not ride along in every snapshot for the rest of the gate.
- Gates live in the game's content and are named by id in snapshots, like skills. Host and guests
  must hold the same content.

**Checkpoints**
- A checkpoint is activated the first time a living player comes within its radius (2 m). The latest
  checkpoint is the furthest one activated; before any, it is the entrance.
- Joiners and rejoiners appear at the latest checkpoint (ADR 0003).

**Solo** (exactly one player present)
- A fallen player chooses when to get up: at the latest checkpoint, at full health, with the fight as
  they left it. Three times per attempt.
- With no revives left, getting up **starts the gate again from the entrance**: every room refills,
  checkpoints are lost, and the three revives come back (developer's decision).

**Co-op** (two or more players present)
- **A fallen player cannot bring themselves back.** They return when a living teammate reaches a
  checkpoint, and appear there at full health (developer's decision, replacing the spec's choice
  between waiting and respawning).
- "Reaches" means walking onto any activated checkpoint, a new one or an earlier one. A teammate who
  was already standing on it when the player fell must step away and come back.
- Being put on a checkpoint is not reaching it: a player who is revived there, joins there, or comes
  back from away there brings nobody back until they step off and walk on again. Otherwise joining or
  reconnecting would be a free revive for the party.
- **Wipe:** when everyone present is down, after 3 seconds the whole party returns at the latest
  checkpoint (the entrance, if none is activated). Rooms that were not cleared start over with their
  enemies at full strength; cleared rooms and checkpoints are kept (developer's decision). A wipe
  that has begun still happens if a teammate's connection drops during the countdown.
- Solo revives are counted per player and are neither used nor refilled in co-op. A solo run that a
  friend joins plays by the co-op rule from that moment (ADR 0003).

**Away players**
- A player whose connection drops stays in the run where they stood, marked away. An away player is
  not a teammate for any rule above (so a host left alone plays by the solo rule), reaches no rooms or
  checkpoints, and is ignored by enemies, including an attack that was already winding up.
- Wipes and solo restarts leave an away player exactly as they were. Entering a gate takes everyone in.
- A player who comes back while down is placed at the latest checkpoint, still down, and waits for a
  teammate like anyone else (or, alone, uses the solo rule).

**Never the character**
- Revives, wipes and restarts change the gate only. Level, XP, stats and skills are untouched
  (spec: "A wipe costs progress inside the gate only").

## Consequences

- With the way always open, a party can run to the next checkpoint without clearing a room. Clearing
  matters only for finishing the gate (and, later, its boss and reward: ticket 18).
- In co-op there is no cost to dying except waiting, and no limit. Two players can leapfrog between
  checkpoints. If that proves too forgiving, the knobs are the wipe rule and how far apart checkpoints
  are placed.
- A fallen co-op player has nothing to press. The spec's "I am never stuck waiting" now depends on
  teammates and on checkpoint spacing, not on a respawn button.
- A lone co-op survivor far from any checkpoint must walk back to one to fetch teammates, with any
  enemies they have woken following them.
- Restarting solo throws away all progress in the gate. In a 30-minute gate that is a heavy stake; the
  first real gate (M3) should be played solo before this is considered settled.
- The straight-line layout keeps the kit and the walls trivial. Gates that turn or branch need a
  layout step that places rooms on a grid; the rules above do not change.
- Enemy views and gate geometry are built from the simulation at run time, so a new gate needs no
  hand-made scene: only room piece assets and a list.
