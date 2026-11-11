# 04: Dodge, enemy damage and player defeat

**What to build:** Enemies can hurt the player, the player can dodge with brief invulnerability frames, and the player has a defeat state.

**Blocked by:** 03: Light attack and one enemy

**Status:** ready-for-agent

- [x] Enemy attacks reduce player health and emit damage events
- [x] Dodge grants a short invulnerability window during which damage is ignored
- [x] Player defeat is a defined state, ready to be extended by the death rules later
- [x] Headless tests cover dodge-negates-damage inside the window and not outside it

## Comments

**2026-11-12 (agent):** Implemented.
- Enemy attacks: when the target is in reach the enemy winds up (0.5 s, telegraphed by an orange tint), then the hit lands if the target is still in reach. 10 damage, then a 1.5 s cooldown. Events: `EnemyAttackStarted`, `PlayerDamaged`, `AttackDodged`, `PlayerDefeated`. The stats are on the enemy definition (`Goblin.asset`).
- Dodge: Space or gamepad east (a new `Player/Dodge` action; those keys were removed from the unused template Jump and Crouch actions). A 3 m dash over 0.25 s in the move direction, or the facing direction if standing still. Invulnerable for 0.3 s starting on the press tick, with a 0.8 s cooldown. No steering or attacking mid-dash. Players act before enemies, so a dodge pressed on the hit tick counts.
- Defeat: `PlayerLifeState { Alive, Defeated }`. All player damage goes through `Player.TakeDamage`, the one place the death rules need to hook into. Defeated players ignore commands and enemies ignore them.
- Tests: `SimulationDefenseTest` pins both window edges (the last invulnerable tick negates the hit; one tick later does not), plus early, late and same-tick cases. A deliberate off-by-one in the window was caught. `GreyboxDefenseTest` (PlayMode) checks that hits land and the dodge dashes in the real scene.

Open notes for later tickets:
- **Not checked by a human:** Space or gamepad dodge in the live game. Tests drive it with a scripted source.
- **Death rules:** there is no revive or respawn yet. Those extend `PlayerLifeState` and `TakeDamage`.
- **07 (data-driven skills):** dodge stats are serialized on `SimulationRunner`, like the light attack, not a data asset yet.
- **Reporting quirk:** a dash that carries the player out of reach counts as a miss, not an `AttackDodged` event. That is fine for gameplay; revisit if feedback or statistics need "dodged" to include it.
- **06 (UI):** `CombatHud` is a placeholder IMGUI health readout.
