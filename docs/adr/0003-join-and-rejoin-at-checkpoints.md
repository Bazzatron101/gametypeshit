# 0003: Players may join or rejoin a gate at checkpoints

**Status:** Accepted (2026-11-13)

## Context

Up to 10 players join a host's world through Steam invites. Gates are long and checkpointed.
Before netcode work starts (ticket 12), the project has to decide whether a gate's party is
fixed when it opens or can change while it runs, because that decides whether the game needs
to send a full gate state to a player arriving mid-run.

The options were:

1. Lock at start: the party is fixed when the gate opens.
2. Join at checkpoints: a friend may enter a running gate and appears at a checkpoint.
3. Join any time: drop in mid-fight beside the host.

The developer plays with six to ten regular friends, and the spec's goal is "I am never
stuck waiting". Option 3 means joining during live combat and prediction catch-up, which is
too much netcode for one developer.

## Decision

- **Join at checkpoints.** A friend may join a running gate at any time. They appear at the
  latest checkpoint the party has activated, never mid-fight. The gate refuses joins once it
  holds 10 players.
- **Rejoin at checkpoints.** A party member who disconnects (crash, connection drop) may
  reconnect to the same run and reappear at the latest checkpoint, keeping everything that run
  gave them.
- Joining and rejoining use the same mechanism: the host sends the arriving player a full
  snapshot of the gate state, then normal play resumes.

## Consequences

**Netcode (tickets 12 and 13)**
- Late join is a requirement, not an option: the netcode spike must prove full-state transfer
  to a joining client, then normal prediction and reconciliation from there. The simulation
  already adds players by id at any time (`SimulationWorld.AddPlayer`) and queues world
  changes to tick boundaries, which suits a host-side "player arrives" operation.
- The joiner spawns at a checkpoint the host chooses, so no join ever lands in an active
  fight's middle.

**Enemy scaling (ticket 27)**
- The party-size tier (solo, 2 to 4, 5 to 10) is recomputed whenever the party changes, but
  only affects enemies spawned afterwards. Enemies already alive keep their stats, so a join
  never makes a fight in progress suddenly harder.

**Death rules and checkpoint revives**
- Revives are tracked per player, by player id, on the host's world, so they survive a
  disconnect and rejoin.
- A solo run that a friend joins becomes a co-op run from that moment: the solo limit of three
  checkpoint revives stops applying, and the co-op rule (wait for a checkpoint revive or
  respawn at the closest checkpoint) takes over. Revives already used do not come back.
- A late joiner starts with no revives used.
- Co-op respawning was "at the entrance", a long walk back in a long gate. Settled on
  2026-11-13: a fallen player who chooses to respawn comes back at the activated checkpoint
  nearest the living party (ticket 17), the same placement a joiner or rejoiner gets.

**Rewards**
- A joiner earns XP and personal loot for everything after they arrive, in full (the spec's
  "rewards are not reduced").
- The guaranteed gate-clear reward goes to everyone present at the clear, joiners included.
- A rejoining player keeps what they had already earned in that run.

**Gates (ticket 17)**
- Checkpoints are first-class: the gate runtime must know the latest activated checkpoint and
  be able to place a player there on request.
