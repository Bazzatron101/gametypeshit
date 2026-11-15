# 0019: Hunters have a name and looks

**Status:** Accepted (2026-11-15)

## Context

Ticket 28 asks for a simple character creator with a few appearance options and a name, saved with
the character, with the name shown above the character in co-op. Hunters are greybox capsules (ADR
0002), so appearance has to be something a capsule can show now and a real model can take over later.

## Decision

- **What is chosen** (the developer's choices):
  - **A name**, chosen once and permanent, like the class. Up to 16 characters: letters, digits,
    spaces, apostrophes, hyphens and underscores. Anything else is dropped, and runs of spaces become one.
  - **Looks**: a body colour from a palette of eight, and a build (slim, average or broad), which
    scales the body. Looks change nothing in play.
- **When:** the creator comes first, then the class picker (the developer chose separate screens).
  Play and going online wait for both. A hunter saved before names (format 4 or older) is asked for
  a name once; it keeps its class and progress.
- **Changing looks later:** in the hub only, by pressing L. The name stays. The change is a player
  command (`ChangeLook`), so the host decides and everyone sees it. Outside the hub it does nothing.
- **Nameplates** float over every other player and bot teammate, but not over your own hunter. They
  fade with distance and hide past 40 m.
- **Saves:** the character format moves to version 5, which adds the name and look. Version 4 and
  older still load, with no name and the default look. Snapshots carry each player's name and look.
- **Bot teammates** take their name from their companion record. Each of the five wears a different
  colour; the tank is broad and the rogue slim. Colour 0 (blue) is left as the player's default.
- **Automated runs** (`-coop`) make their hunter without asking: `-name NAME`, or "Bot SEED" for a
  `-bot` run, or "Hunter".
- **The palette and build sizes** live in the game layer (`CharacterLooks`). The simulation keeps only
  the colour's index and the build.

## Consequences

- The local and remote player materials no longer decide a player's colour; the chosen colour does.
  A charging skill now blends from the hunter's own colour.
- Gear drawn on the character (ticket 34) should sit on top of these looks, not replace them.
- Names are not checked for uniqueness or content; sessions are among friends (spec, out of scope).
