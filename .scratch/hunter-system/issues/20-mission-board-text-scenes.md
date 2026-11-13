# 20: Mission board and text-only story scenes

**What to build:** A mission board in the hub gives intel about gates in the System's voice, and one scripted in-engine, text-only story scene plays.

**Blocked by:** 16: Decide the story premise, 19: Hub

**Status:** ready-for-human (write the intel and the arrival scene; settle ticket 16)

- [ ] Mission board shows intel for available gates in the System's voice
- [x] A scripted text scene plays in-engine with no cutscene animation
- [ ] Tone follows the premise decision

## Comments

**2026-11-13 (developer, recorded by agent):** Decided:
- **Co-op:** everyone reads at their own pace.
- **Unwritten text:** blank "write me" markers, not drafts.
- **First scene's moment:** no preference, so the first arrival in the hub was used.

The rules are in `docs/adr/0009-story-scenes-play-once-per-host-world.md`.

**2026-11-13 (agent):** Built the machinery. The words are the developer's.

What was built:
- **In the simulation:** `StorySceneDefinition` and its triggers (first hub arrival, first clear of a gate). The world records which scenes have played, travels in snapshots, and is saved in the host's world (world save format 2; format 1 still loads).
- **The text scene asset** `TextSceneAsset` holds the lines, and `TextScenePlayer` shows them in-engine as text, revealed letter by letter (Space continues, Esc skips).
- **Gate intel** is a field on `GateDefinitionAsset`, shown on the gate board.
- **First content:** the Arrival scene (four lines) and Goblin Warren's intel, all blank markers.
- **A writing list:** the content test `ListTheWritingStillToDo` lists every slot still to write. Five today: the Goblin Warren intel and the four lines of the arrival scene.

Also fixed: a runner leaving a scene saved the host's world to whatever save folder was set at that moment, not the one it loaded from. Harmless in the game, where the folder never changes, but it wrote into the wrong folder between tests.

Evidence:
- 8 story tests: once per world, a clear trigger, loaded scenes, presence, snapshots, and old saves loading.
- A scene test in the co-op scene: hosting a new world plays the arrival scene, reading through it ends it, and hosting that world again does not replay it.

Boxes left open:
- **Box 1 (the System's voice):** the board shows intel, but the voice is unwritten.
- **Box 3 (tone follows the premise):** waits for ticket 16 and the developer's writing.

**2026-11-14 (agent), after review:**
- **Fixed:**
  - Scenes that start together play one after another.
  - A gate-clear scene waits until the party is home.
  - A scene closes the gate board and keeps it shut until it ends.
  - Skipping with Esc leaves the cursor free, as Esc does everywhere.
  - The runner keeps its no-save setting from when it loaded, like its save folder.
  - Scenes are off in scenes without the hub.
- **Evidence:** 408 EditMode and 24 PlayMode tests pass.

