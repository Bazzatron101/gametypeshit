# 05: Camera, keyboard and mouse controls, soft lock-on

**What to build:** Keyboard and mouse controls with a free camera, light and heavy attacks on the mouse buttons, and a soft lock-on assist that helps target enemies without forcing a full lock.

**Blocked by:** 03: Light attack and one enemy

**Status:** ready-for-agent

- [x] Free camera follows the player with mouse look
- [x] Light and heavy attacks are on the mouse buttons
- [x] Soft lock-on nudges aim toward the nearest valid enemy and can be overridden by the player
- [x] Heavy attack exists in the simulation with its own damage and timing

## Comments

**2026-11-12 (agent):** Implemented.
- Camera: `ThirdPersonCamera` orbits and follows the player (7 m, pitch -5 to 65 degrees) with mouse or right-stick look. The cursor is locked while playing; Escape frees it, and a click re-locks it without attacking. WASD is camera-relative: the game layer rotates the stick by the camera's yaw, so commands stay world-space and the simulation never sees the camera.
- Mouse buttons: left is light (`Player/Attack`), right is heavy (a new `Player/HeavyAttack` action, also on the gamepad right trigger). Buttons are ignored while the cursor is free.
- Soft lock-on (simulation, when an attack starts): turns the player toward the nearest live enemy inside a 90 degree cone around their facing. The turn is at most 45 degrees, which is the nudge. Only enemies within the attack's own reach count (capped at 4 m), so the assist never drags a swing off a hittable enemy. Holding a direction outside the cone overrides it, and `SoftLockOn.Enabled` turns it off.
- Heavy attack (simulation): 25 damage, 2.5 m, 90 degree arc, 0.35 s windup, 1 s cooldown, each attack with its own cooldown. The player is committed during the windup (no moving or light attacks), and a dodge cancels it. Events: `AttackWindupStarted`; `AttackPerformed` now carries `Kind`.
- Tests: `SimulationAimAndHeavyTest` (lock-on rules, override, unreachable targets ignored, heavy timing, damage, commitment, dodge cancel, cooldown); `CameraMathTest`; `GreyboxCameraTest` (PlayMode: camera follows the player, heavy swings in the scene).

Open notes for later tickets:
- **Not checked by a human:** mouse look feel and sensitivity, cursor lock, right-click heavy, camera-relative WASD.
- **10 (feel review):** whether a dodge-cancelled heavy should refund its cooldown (it does not, which prevents dodge-cancel spam); heavy beats light when both are pressed on one tick; and whether lock-on should lunge toward targets just out of reach.
- **Camera:** no collision yet; it can clip through walls once gates have walls (ticket 17).
- **07:** heavy attack and lock-on tuning live on `SimulationRunner`, like the light attack and dodge.
