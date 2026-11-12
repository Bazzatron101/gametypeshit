# 09: Test arena

**What to build:** An in-editor test scene for spawning enemies and trying skills quickly, so tuning takes minutes instead of full runs.

**Blocked by:** 07: Data-driven skills with the six-slot bar

**Status:** ready-for-agent

- [x] Arena scene lets the developer spawn chosen enemies and set the player's level and loadout
- [x] Skills and modifiers can be tried without leaving the scene
- [x] The arena uses the same data definitions as the real game, with no duplicate content

## Comments

**2026-11-13 (agent):** Implemented.
- Scene: `Assets/Scenes/Arena.unity` (Hunter System > Rebuild Test Arena Scene, or Rebuild All Scenes). F1 toggles the arena panel, which opens on start:
  - spawn 1, 3 or 6 of any enemy in a ring, or clear them all
  - raise the level (+1, 5, 10, 20, 40, max)
  - teach every skill, and click a slot to cycle known skills into it
  - toggle any modifier
  - reset cooldowns, and heal
- One content list: `Assets/Data/GameContent.asset` holds the player tuning (`PlayerTuning.asset`: tick rate, health, move speed, light and heavy attacks, dodge, lock-on), progression, and every skill, modifier and enemy. Both scenes' runners point at it; the runner holds nothing else. The builder appends any newly found asset of these types, and `ContentValidationTest` fails if one exists outside the list (an asset added after the last rebuild) or if there is more than one progression or tuning asset.
- Simulation: world operations from outside player commands queue and apply at the start of the next tick, in call order, with events: `GrantModifier`, `RevokeModifier`, `GrantXp`, `RaiseToLevel` (worked out at apply time, so repeats do not overshoot; never lowers), `TeachSkill`, `ResetCooldowns`, `RestoreHealth` (also clears a pending windup or dodge). `SpawnEnemy` and `DespawnEnemy` stay immediate (the caller manages the view by id). There is a new player command `AssignSkill` (move, swap or clear bar slots, with a `SkillSlotChanged` event per slot touched), which is also the base for a loadout screen.
- New enemy: Hobgoblin (90 HP, slower, 20 damage, 0.8 s telegraph, 60 XP).
- Cursor: the camera tracks open menus by owner, so the arena panel and the stat screen no longer fight; the cursor returns to the game only when the last one closes.
- Tests: `SimulationArenaControlsTest`, `ArenaLayoutTest`, `CameraMenusTest`, `PlayerCommandBufferTest` (assignments), `ContentValidationTest` (single content list); `ArenaTest` (PlayMode: spawn a chosen type and clear; level through the real path including unlocks; loadout plus modifier, then fire).

Open notes for later tickets:
- **Not checked by a human:** the arena panel UI.
- **10 (feel review):** use the arena to tune. Tuning lives in `PlayerTuning`, `Progression` and the skill, modifier and enemy assets, so changes made there apply in every scene.
- **Netcode (12):** `SpawnEnemy`/`DespawnEnemy` are immediate host-side calls, not queued. Gate spawners (17) should decide whether spawns become queued operations with events.
- **Guarding:** `TeachSkill`, `RestoreHealth`, `ResetCooldowns` and `RaiseToLevel` are general world calls. Quests, checkpoints and revives will reuse some of them; anything arena-only stays in `ArenaPanel`.
- **Greybox toggles:** F5 to F12 map to the content's modifier list in order, so new modifiers can shift the keys. Remove the toggles when gear grants modifiers (22, 23).
