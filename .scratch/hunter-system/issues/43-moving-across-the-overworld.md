# 43: Roaming monsters and bots that cross real ground

**What to build:** The overworld's roaming monsters, and the movement they and the bot teammates need on ground that is not a flat room. Monsters leak from gates and match the gate near them, its rank and enemy types; a cleared gate stops leaking until a story event or the guild reopens it (ADR 0015). Bot teammates (ticket 33) must follow players across the world.

**Blocked by:** 37: Give the simulation real ground, 38: The overworld as a third kind of place

**Status:** ready-for-human (walk up to a gate in the overworld and meet what leaks from it; bring your teammates)

- [x] Each found or hidden gate leaks a configured number of monsters of its own types and rank around its entrance; a cleared gate stops
- [x] Monsters exist only while their gate's tile is active (ticket 40)
- [x] Monsters and bots move with steering over the walkability grid (ticket 37): around steep ground and water, through passes; Unity NavMesh is not used, since the simulation must run the same everywhere
- [x] Monsters stay near their gate and give up a chase past a configured distance
- [x] Bot teammates follow their player across the overworld, keep up on a mount, and regroup at a gate
- [x] Tests: a leak's types match its gate, a cleared gate stops leaking, a monster walks around a ridge, a bot follows its player through a pass

**2026-11-16 (developer, recorded by agent):** Triaged: build now ("this still feels very basic").

**2026-11-16 (agent):** Built. EditMode 670 and PlayMode 38 passed.

- **Leaks** (`SimulationWorld.Leaks.cs`): each gate on the map keeps `OverworldDefinition.LeakCount` (3) of its own kinds round its entrance (`LeakRadius`, 10 m). These are the non-boss enemies its rooms place, in turn. The whole leak arrives the first time its tile is loaded. One killed is replaced `LeakRespawnSeconds` (60) after it fell, one at a time. A cleared gate stops leaking. When its tile unloads (ticket 40), its monsters go. Leak state is in snapshots.
- **Rank:** gates have no rank yet (ticket 25), so a leak is the gate's own monsters as its rooms author them, with no extra scaling.
- **Leash:** a leaked monster chases only players within `LeakLeash` (30 m) of its gate, and walks home otherwise.
- **The host's alone:** a guest's world (`RunsLeaks` false, set by `ClientSession`) never makes or removes leaks itself; it shows the ones snapshots bring, so its prediction never shows a monster the host has not made.
- **Steering** (`GroundPath`): straight when the line is clear; otherwise a search over 2 m cells of the ground (8 directions, no cutting corners, ties broken the same way everywhere), with the furthest corner in sight taken to smooth the zigzag. Monsters use it out in the overworld and work their way again every 15 ticks or when the goal moves. In gates and the hub they go straight as before. No Unity NavMesh.
- **Bot teammates** (`CompanionBrain`): out in the overworld they mark themselves ready, fight what leaks as they do in a gate, follow in formation with the same steering, get on a mount when their leader rides and off when the leader stops near them. The host gives them a mount when their leader has one, and brings one left more than 150 m behind back beside the leader (`HostSession.CompanionRegroupDistance`). That covers regrouping at a gate.
- **Tests:** `OverworldMonstersTest` (10): a gate's own kinds round its entrance and never its boss; a cleared gate stops; a kill replaced after the wait; only while the tile is loaded; a guest's world makes none; the leash; a way found round a wall; a monster walking round a wall to a player; a teammate following its leader round a wall; a teammate riding with its leader.

**Left for later:**
- A steered monster's last-found way is not in snapshots, so a world restored mid-chase may take a slightly different line for up to a quarter of a second. Guests never run leaks, and the host never restores, so nothing sees it today.
- Leaks scale with gate rank once ranks exist (ticket 25), and with story events that reopen cleared gates.
- The stress-test bots (`BotBrain`) do not use the overworld yet: ticket 44.
