# 17: Gate runtime

**What to build:** A gate assembled from a modular room kit, with checkpoints and the death rules. Solo players get three checkpoint revives then restart at the entrance. Co-op players who die come back when a living teammate reaches a checkpoint (changed by the developer on 2026-11-13; see the comments and ADR 0006).

**Blocked by:** 04: Dodge, enemy damage and player defeat, 13: Two-player Steam co-op

**Status:** ready-for-human (play the gate solo and with a friend)

- [x] Rooms from a reusable kit chain into one playable gate
- [x] Checkpoints are reached and saved during a run
- [x] Solo: three revives per gate, then entrance restart
- [x] Co-op: a fallen player returns when a living teammate reaches a checkpoint (changed by the developer, 2026-11-13; was "can choose a checkpoint revive or an entrance respawn")
- [x] A wipe costs progress inside the gate only, never the character
- [x] Headless tests cover the revive counting and respawn choices

## Comments

**2026-11-13 (agent):** From ticket 11 / ADR 0003:
- The gate runtime must track the latest activated checkpoint and place a joining or rejoining player there on request.
- Revives are per player, by player id, on the host's world, and survive a disconnect and rejoin. A late joiner starts with none used.
- A solo run that a friend joins switches to the co-op death rule from that moment; revives already used do not come back.
- Decide here whether co-op "respawn at the entrance" becomes "respawn at the latest checkpoint" (the ADR recommends the checkpoint).

**2026-11-13 (developer, recorded by agent):** Decided: in co-op, a fallen player who chooses to respawn comes back at the **closest checkpoint**, not the gate entrance. Recorded as the activated checkpoint nearest the party members still alive, so they rejoin the fight fastest; if the whole party is down (a wipe), the latest activated checkpoint. Solo is unchanged: three checkpoint revives, then restart at the entrance.

**2026-11-13 (developer, recorded by agent):** Four rules decided; the full rules are in `docs/adr/0006-gate-runtime-and-death-rules.md`.
- **Co-op deaths:** there is no respawn button. A fallen player comes back when a living teammate reaches a checkpoint. This replaces the "closest checkpoint" respawn decided earlier.
- **Wipe:** everyone returns at the latest checkpoint and unfinished rooms refill.
- **Solo, out of revives:** the whole gate resets, with three new revives.
- **Doors:** the way onward is always open.

One reading of the developer's answer is the agent's own: "reaches a checkpoint" counts any activated checkpoint, new or earlier, so a party is never left with no checkpoint ahead. Change one condition in `ReachCheckpoints` if only new checkpoints should count.

**2026-11-13 (agent):** Built.

What was built:
- **Rules in the simulation:** `GateDefinition`, `RoomDefinition` and `GateLayout` describe a gate. `SimulationWorld.StartGate` runs it: rooms fill when reached, walls confine, checkpoints activate, solo revives are counted, teammates bring back the fallen, wipes and restarts reset rooms. Gate state travels in snapshots, so guests predict the walls and late joiners get the whole run.
- **Away players:** a disconnected player's character is marked away. It is no teammate for the rules and enemies ignore it, so a host left alone plays by the solo rule.
- **Kit:** `RoomPieceAsset` and `GateDefinitionAsset`, listed in the game content. The first gate, Goblin Warren, chains five pieces into seven rooms, using the corridor three times.
- **In the game:** the co-op menu has "Play solo / host locally" and, once hosting, "Enter gate: Goblin Warren". `GateView` draws rooms, walls and checkpoint pillars from the layout. `GateHud` shows progress, notices and the death panel. Solo revive is a button or Enter.
- **Bots** walk a gate room by room and ask to get up when down.

Evidence:
- 30 headless rule tests. The rules were broken on purpose to confirm the tests fail.
- Session tests: a guest's prediction stops at the wall, a joiner appears at the latest checkpoint, a fallen guest is brought back by the host and told once.
- Playthroughs with the real content: three bots clear the gate in 22 s and one bot clears it solo in 29 s, with no deaths.
- `scripts/coop-gate-test.ps1`: a built host and client cleared the gate over local UDP and agreed on every room and checkpoint.
- Scene tests for the geometry and the death panel.

Limits:
- **Goblin Warren is greybox proof, not a dungeon.** It takes under 30 seconds and bots never die in it. The spec's 30-minute gates are M3 content work.
- **Straight lines only.** Rooms chain along one axis; turns and branches need a layout step.
- **No way out of a gate yet.** Leaving or finishing returns nowhere until the hub exists (19); the boss and clear reward are ticket 18.
- **Not played by a person yet.** The death panel and the feel of the rules need the developer's hands, solo and with a friend.

**2026-11-13 (agent), after review:**
- **Fixed:**
  - **Being put on a checkpoint no longer counts as reaching it.** A friend joining, a player reconnecting, or a player revived there could bring back the fallen for free.
  - An attack that was already winding up no longer lands on a player who has gone away.
  - A solo restart no longer moves or revives away players.
  - A wipe that has begun still happens if a teammate drops out during the countdown. Before, the player left had to spend a solo revive or restart the gate.
  - A cleared room's defeated enemies are removed after three seconds, so snapshots do not keep growing through a gate.
  - The host's "Spawn Goblins" buttons are hidden inside a gate; those enemies would have survived its resets.
  - Gate state read from a snapshot is range-checked.
- **Left as is (judgement calls):**
  - The command is called `Respawn` while the rules say "revive".
  - `GateState` forwards to the world.
  - `-1` stands for the entrance in checkpoint numbers.
  - "Away" is a connection idea living in the simulation; ADR 0006 defines it as a rule.
- **For the developer to decide by playing:**
  - Whether "reaches a checkpoint" should count earlier checkpoints or only new ones.
  - Whether co-op deaths are too cheap, since nothing limits them.
  - Whether a solo restart of the whole gate is too harsh for a long gate.
