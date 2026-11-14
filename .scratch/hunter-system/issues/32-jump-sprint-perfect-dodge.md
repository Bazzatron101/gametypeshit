# 32: Jump, sprint and perfect dodge feedback

**What to build:** Improve movement with a jump and a sprint, and show the player when they have made a perfect dodge (requested by the developer, 2026-11-14).

**Blocked by:** None

**Status:** ready-for-human (try the feel)

- [x] Hold to sprint, faster on foot, ending when you fight
- [x] A jump that clears ground-level attacks but not an enemy; classes can raise it
- [x] A perfect dodge is shown on screen
- [x] Keys: Space jump, Shift sprint, Ctrl dodge

## Comments

**2026-11-14 (developer, recorded by agent):**
- **Keys:** Space jumps, Shift sprints, Ctrl dodges.
- **Sprint:** hold, no stamina.
- **Jump:** clears ground attacks. The base jump is not high enough to jump over an enemy; classes can raise it so it is.

**2026-11-14 (agent):** Built. The rules are in `docs/adr/0011-jump-sprint-and-perfect-dodge-feedback.md`.
- **Sprint:** 160% speed while held. Starting an attack or skill breaks the run for 0.4 s; charging or dodging stops it while they last.
- **Jump:** 0.9 m high, 0.6 s long. You can steer in the air, but you can't attack, dodge or jump again. It clears a boss's slam.
- **The charge:** a base jump does not clear it, because the Warlord is 3.5 m tall. A player's jump height percentage (for classes) clears it once it is above the boss's height.
- **Enemy heights:** each enemy has one, and content validation keeps the base jump below all of them.
- **Perfect dodge:** a cyan ring bursts around the player, and the local player sees a "PERFECT DODGE" banner that counts up a streak.
- **Evidence:**
  - 15 jump and sprint tests, with the slam, charge and sprint rules each broken on purpose to confirm their tests fail.
  - A scene test makes a real perfect dodge against a goblin and checks the banner and the ring.
  - The built two-copy fight and the networked gate pass.
- **Not done:** no feedback for clearing a slam with a jump; jump height is not saved with the character.

**2026-11-14 (agent), after review:**
- **Fixed:**
  - Pressing attack on cooldown no longer makes a sprint stutter. The run pauses only after an attack or skill really starts.
  - Placing a player mid-jump lands them.
  - Bots measure distance along the ground.
  - The camera follows jump height gently.
  - The input file now ends with a newline.
- **Noted in ADR 0011:**
  - A slam-only boss slams anyone close.
  - Ctrl as the dodge key can trigger editor or browser shortcuts when combined with W, S or D.
- **Evidence:** 442 EditMode and 25 PlayMode tests pass. The built network tests pass.

