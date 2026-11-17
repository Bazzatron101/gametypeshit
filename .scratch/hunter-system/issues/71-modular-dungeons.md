# 71: Mix up the dungeons, built from swappable pieces

**What to build:** Make gates (dungeons) feel more varied, and make them quick to change at any time. From the developer, 2026-11-17: "change the dungeons up and make it modular so its easy to change up at any time".

**Blocked by:** none (builds on 17: Gate runtime, done)

**Status:** split into 72-79 (this ticket is done when they are)

**Child tickets:**
- 72: Encounters separate from rooms
- 73: Gate layouts that turn, branch and loop
- 74: Top-down gate view in the editor
- 75: New room shapes
- 76: Room features
- 77: Dungeon theme kits
- 78: Shuffled farming gates
- 79: Rebuild today's gates from the new pieces

- [ ] Gates can turn, branch and loop (paths that rejoin, more than one way through) instead of running in one straight line (ADR 0006 already names this: "a layout step that places rooms on a grid")
- [ ] Repeatable farming gates shuffle their layout from their pieces each run; story gates keep fixed layouts. The host decides the shuffle and every player sees the same gate
- [ ] Theme kits: each race or gate theme has its own room look (for example goblin warren vs beast cave)
- [ ] A top-down editor view that draws a gate's layout while you edit it
- [ ] Room features for the first pass: cover/pillars, traps/hazards (telegraphed), chests, ledges/stairs
- [ ] Room pieces come in more shapes than plain rectangles (for example L-shapes, round chambers, long halls with side alcoves)
- [ ] A room's enemies are a swappable "encounter" (a list of who spawns and where), separate from the room's shape, so the same room can hold different fights
- [ ] Rooms can hold reusable features (for example pillars to hide behind, hazards, a chest, a shortcut), each its own data piece
- [ ] Changing a gate means editing data only: reorder, add, remove or swap rooms, encounters and features without touching code or scenes
- [ ] The content check catches broken gates (rooms that don't connect, a boss room that isn't last, a gate with no checkpoint)
- [ ] Existing gates (Goblin Warren, Thornwood Hollow, the class trials, the Goblin King raid) are rebuilt from the new pieces
- [ ] Snapshots, co-op, checkpoints, party scaling and bot teammates all work in the new layouts
- [ ] New looks are placeholders labelled "AI" (memory rule)
- [ ] Tests cover layout placement, branching, and the content check

## Notes

**What exists today:**
- A gate (`GateDefinitionAsset`) is an ordered list of room pieces (`RoomPieceAsset`), entrance first. This is ADR 0006.
- Every room is a rectangle with a width and length, its enemy spawns, an optional checkpoint and an optional boss. The enemies are baked into the room piece, so a different fight needs a new piece.
- Rooms run end to end in one straight line; a narrow room between two wide ones is a corridor. Walls are just the edge of the floor.
- Gate geometry is built from the data at run time, so a new gate needs no hand-made scene, only assets.
- There are 10 room pieces (`Assets/Data/Rooms/`) and 19 gates (`Assets/Data/Gates/`).
- The spec (story 43) plans for repeatable farming gates to be "procedurally shuffled later", while story gates keep fixed layouts.

**To settle in triage:**
- **"Change up" for whom:** is this mainly so *you* can rebuild gates quickly while designing, or so *players* get a different layout each run (shuffled or random gates)? Or both?
- **Shuffling:** if gates shuffle, which ones: only repeatable farming gates (as the spec says), or story gates too? Does a shuffle happen every run, or daily/weekly?
- **Layout shape:** turns only, or branches and dead ends too (for example a side room with a chest)? Can rooms have more than one exit?
- **Room features:** which first: cover/pillars, traps/hazards, chests, locked shortcuts, vertical bits (ledges, stairs)?
- **Look:** should each race (ticket 63) or gate theme have its own room kit look (a goblin warren vs a beast cave)?
- **Editor tooling:** is editing assets in Unity's inspector enough, or do you want a tool to see a gate's layout from above while you edit it?
- **Existing gates:** convert them to the new pieces, or leave them as they are?
- Probably too big for one ticket: consider splitting into (a) encounters separate from rooms, (b) grid layout with turns and branches, (c) new room shapes, (d) room features, (e) shuffled gates.

## Comments

**2026-11-17 (developer, recorded by agent):** Decided in triage:
- **For whom:** both. The developer rebuilds gates quickly from pieces, and repeatable farming gates shuffle for players. Story gates stay fixed (as the spec says).
- **Layout:** turns, branches and loops (paths that rejoin).
- **Features:** all four for the first pass: cover/pillars, traps/hazards, chests, ledges/stairs.
- **Look and tools:** theme kits per race or gate theme, a top-down layout view in the editor, and today's 19 gates converted to the new pieces.

Not asked, agent's default (change if wrong): a farming gate reshuffles every run. Chests and side rooms hold loot from the gate's own drop list (ticket 61).

Suggested split, in order, each as its own ticket:
1. Encounters separate from rooms (who spawns where becomes its own piece)
2. Grid layout with turns, branches and loops, plus the content check for broken gates
3. Top-down editor view
4. New room shapes
5. Room features: cover/pillars, then traps/hazards, then chests, then ledges/stairs (ledges need the ground model from ticket 37 to handle height inside a gate)
6. Theme kits (goblin warren look, beast cave look; pairs with tickets 63 and 69)
7. Shuffled farming gates
8. Convert today's 19 gates to the new pieces
