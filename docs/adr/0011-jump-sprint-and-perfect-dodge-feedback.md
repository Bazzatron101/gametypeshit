# 0011: Jumping clears the ground, not the enemy; sprinting is for moving, not fighting

**Status:** Accepted (2026-11-14)

## Context

The developer asked for a jump, a sprint, and something that shows a perfect dodge (ticket 32). They
chose the keys, how sprint works, and what a jump does in a fight, with one rule of their own: the
base jump is not high enough to jump over an enemy, but classes can make it so.

## Decision

- **Keys:** Space jumps, hold left Shift to sprint, and dodge moves from Space to left Ctrl. On a
  gamepad, south jumps, east still dodges, and pressing the left stick sprints.
- **Sprint** (developer's choice): held, with no stamina. Movement runs at 160% of walking. Starting an
  attack or a skill breaks the run for 0.4 s, and charging or dodging stops it while they last; pressing
  attack while it is still cooling down does not. A jump keeps its run-up.
- **Jump:**
  - A hop along a fixed arc: 0.9 m high, 0.6 s long. You can steer in the air, but you can't jump
    again, dodge, attack or use skills.
  - Being in the air **clears ground-level attacks**: a boss's slam is a shockwave along the ground.
  - **Sweeps and ordinary swings still hit you** in the air.
- **Jumping over an enemy** (developer's rule) means being higher than the enemy is tall when it would
  hit you. Every enemy has a height (goblins 1.8 m, the Warlord 3.5 m), and the base jump is below all
  of them, which content validation checks. So **a base jump does not clear a boss's charge**: the boss
  runs through you. Each player has a jump height percentage, for classes (ticket 24) to raise; a high
  enough jump passes over the charge.
- **Perfect dodge feedback:** an enemy's hit landing inside a dodge's invulnerability bursts a ring
  around that player, which everyone sees, and shows the local player a "PERFECT DODGE" banner. Each
  one made while the banner is still up (1.1 s) counts up the streak ("x2", "x3"). It is presentation
  only and changes nothing in the fight. On a guest it follows the host's confirmation, about a round trip later.
- A jump or a cleared attack is announced as an event (`PlayerJumped`, `AttackJumped`). A guest
  predicts its own jumps, like its dodges.

## Consequences

- The jump gives the slam a second answer (out of the circle, or over the shockwave) with different
  timing to a dodge. The charge still has only one answer, stepping aside, until a class raises the jump.
- Jump height lives on the player in the world, not yet in the character's save. Classes will decide
  where it is kept.
- Players have no collision with enemies yet, so on foot you can already walk through an enemy.
  "Jumping over" only matters for the charge until bodies block each other.
- There is no feedback yet for clearing a slam with a jump (`AttackJumped` is available for it).
- Side change: a boss with a slam but no sweep now slams anyone close. Without it, such a boss (used in
  the jump tests) would never attack someone in front of it within reach.
- Dodge is on Ctrl, so dodging while moving makes Ctrl+W, Ctrl+S or Ctrl+D. That is harmless in a
  built game, but it can trigger shortcuts in the Unity editor's Play mode, or in a browser build.
- The camera follows a jump's height gently, so the view doesn't bob a full jump with every hop.
