# 11: Decide mid-gate joining

**What to build:** A written decision on whether gates lock at the start or let players join at checkpoints, made before netcode begins because it changes the networking design.

**Blocked by:** None (can start immediately)

**Status:** ready-for-human

- [x] Decision recorded: gates lock when started, or players may join at checkpoints
- [x] Reasoning and any consequences for checkpoint revives are noted

## Comments

**2026-11-13 (developer, recorded by agent):** Decided. See `docs/adr/0003-join-and-rejoin-at-checkpoints.md`.
- Friends may join a running gate at any time and appear at the latest checkpoint the party has activated, never mid-fight. A gate holds at most 10 players.
- A party member who disconnects may rejoin the same run at the latest checkpoint, keeping what the run gave them.
- Consequences: late join (full gate-state transfer) is a netcode requirement; the party-size tier is recomputed on join but only affects enemies spawned afterwards; revives are tracked per player and survive a rejoin; a solo run that gains a friend switches to the co-op death rule; a joiner earns full XP, loot and the clear reward from arrival.
- Open for the death rules (17): whether co-op "respawn at the entrance" should become "respawn at the latest checkpoint". This decision makes the checkpoint the natural choice.
