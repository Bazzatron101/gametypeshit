# 0013: Skills aim with the camera, and a solo fall resets the boss

**Status:** Accepted (2026-11-14)

## Context

The developer asked for two changes after playing: "make the skill attack be in the direction the
camera is" and "when the player dies in solo the bosses health needs to go back to full health".

## Decision

- **Skills aim where the camera looks.** Each command carries the camera's yaw. When a skill fires,
  the player turns that way and the skill goes that way. This applies to cones, projectiles, and a
  charged skill when it is let go. An aimed skill takes **no soft lock-on**. The assist turns
  fully to the nearest enemy within 45° either side, which could send a skill far off where the
  camera looks. Light and heavy attacks keep the assist.
  - A charge countered by a perfect dodge still turns to the attacker.
  - A source with no camera (bots) sends no aim, and its skills go where it faces, as before.
  - Light and heavy attacks are unchanged. The request was about skills.
- The aim travels as 65,536 steps of a full turn (two bytes on the wire). The command stores it in
  those steps, so the host replays exactly what the guest predicted.
- **A solo fall resets the boss.** In a gate with one player present, the moment the player falls,
  every boss room whose boss is not yet beaten empties. A boss that falls in the same tick as the
  player stays beaten. When the player walks back in, the boss returns at full
  health, calm, and without its goblins. Other rooms stay as they were. Once the gate is cleared,
  nothing resets.
  - This is the solo counterpart of the co-op wipe (ADR 0006). The wipe also refills every
    unfinished room.

## Consequences

- A solo boss must be beaten in one life. With three revives per attempt, that means up to four tries
  before the gate starts again. The solo playthrough test still clears the gate with every class.
