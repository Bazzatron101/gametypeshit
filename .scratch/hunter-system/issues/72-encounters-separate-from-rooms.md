# 72: Encounters separate from rooms

**What to build:** Take a room's enemies out of the room piece and make them their own swappable piece, an "encounter" (who spawns and where). A gate then pairs a room shape with an encounter, so the same room can hold different fights and a fight can be reused in different rooms. Split from ticket 71.

**Blocked by:** none (parent: 71)

**Status:** ready-for-agent

- [ ] An encounter asset holds a list of spawns (enemy and position) and, optionally, a boss; positions are relative to the room so one encounter fits rooms of a similar size
- [ ] A gate's room entry is a room piece plus an encounter (or none, for an empty room)
- [ ] The content check warns when an encounter's spawns fall outside the room they are paired with
- [ ] Existing gates keep working: today's room pieces are split into a room and an encounter (an editor step does it, so nothing is hand-copied)
- [ ] The simulation's `RoomDefinition` does not change shape (rooms still arrive with their spawns), so snapshots and co-op are untouched
- [ ] Tests cover pairing, an empty room, and the content check
