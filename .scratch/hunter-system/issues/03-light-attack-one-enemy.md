# 03: Light attack and one enemy

**What to build:** The player can attack with a light attack, and one enemy type chases the player, takes damage, and dies. This is the first fight and it is verifiable headlessly.

**Blocked by:** 02: Simulation core with a movable capsule

**Status:** ready-for-agent

- [x] Light attack on the mouse button deals damage in the simulation, with damage events emitted
- [x] One enemy type chases the player and is defeated when its health reaches zero
- [x] A headless test asserts enemy defeat after the expected number of hits

## Comments

**2026-11-12 (agent):** Implemented.
- Simulation: `PlayerCommand.LightAttack` hits every live enemy within 2 m in a 120 degree cone around the player's facing (the last direction they moved), then waits out a 0.4 s cooldown. Each tick records `AttackPerformed`, `DamageDealt` and `EnemyDefeated` events in `SimulationWorld.Events`.
- Enemy: the Goblin is data (`Assets/Data/Enemies/Goblin.asset`): 30 health, 3 m/s, stops 1.2 m from the nearest player. Defeated at 0 health; three light attacks at 10 damage.
- Input: left mouse (or gamepad west) through the project's `Player/Attack` action. `PlayerCommandBuffer` keeps a press until a tick takes it, which resolves the 02 finding about lost taps.
- Tests: `SimulationCombatTest` (headless; `EnemyIsDefeatedAfterThreeLightAttacks` is the ticket's test), `PlayerCommandBufferTest`, and `GreyboxFightTest` (PlayMode, in the real scene).

Open notes for later tickets:
- **Not checked by a human:** clicking the mouse in the live game. Tests drive attacks through a scripted command source.
- **07 (data-driven skills):** light attack stats are serialized fields on `SimulationRunner`, not a data asset yet. Move them when skills become data.
- **10 (feel review):** a press during cooldown is dropped, not buffered. Input buffering is a feel decision.
- **05 (soft lock-on):** facing is the last move direction; lock-on may replace it for attacks.
