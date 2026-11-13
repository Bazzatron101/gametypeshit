# 0009: Story scenes play once per host world, read at each player's own pace

**Status:** Accepted (2026-11-13)

## Context

The spec tells the story through "in-engine, text-only scenes" carried by a fixed NPC cast, with
story progress belonging to the host's world (stories 58, 61 and 71), and the gate board giving
intel "in the System's voice" (story 49). Ticket 20 asks for the board's intel and one scripted
scene. The premise's hook and the System's voice are still open (ticket 16), and the developer
writes the words. The developer chose on 2026-11-13 how scenes play in co-op and what fills the text
until it is written; they had no preference on the first scene's moment.

## Decision

- **The simulation decides when a scene plays; the presentation decides what it says.** A scene's
  trigger (the first arrival in the hub, or the first clear of a named gate, told once the party is
  home) is in the simulation's config, so every machine agrees on the moment. The lines live in a
  text scene asset that only the presentation reads.
- **Once per host world.** The host's world records the scenes it has played (ADR 0005's world save,
  format 2), loads them when hosting, and snapshots carry them to guests. A scene never plays twice,
  not even in a guest's prediction. A guest's own game does not remember scenes; their story is the
  host's (story 71).
- **The first scene** plays on a world's first arrival in the hub (the developer had no preference; the
  hub is where every world starts).
- **Each player reads at their own pace** (developer's choice). The scene opens for everyone present at
  once; each player advances or skips their own copy, and the game carries on underneath (in the hub,
  where nothing attacks: every trigger fires there). Scenes that start together play one after another.
  A scene has the screen: the gate board closes and stays shut until it ends. A player who joins later
  does not see a scene that has already played.
- **Gate intel** is a text field on each gate, shown on the gate board under the gate's name.
- **Blank markers, not drafts** (developer's choice): every unwritten slot reads "[...: write me]". A
  content check lists every slot still holding one, so the writing left to do is always visible.
- Scenes are text only: a panel with the speaker and the line, revealed letter by letter. No camera
  work, animation or voice (spec, out of scope).

## Consequences

- Adding a scene is a data change: a text scene asset with lines and a trigger, in the content list.
  New kinds of trigger (a rank promotion, a story beat) are a small simulation change each.
- Because scenes are per host world, a guest who visits several friends' worlds sees each world's
  scenes. Whether a guest should see a story beat they have already seen elsewhere is a question for
  when the story exists.
- Until ticket 16 settles the hook and the System's voice, ticket 20's "tone follows the premise" stays
  open, and the game shows its blank markers.
