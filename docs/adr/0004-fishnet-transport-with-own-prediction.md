# 0004: Fish-Net for connections and Steam; prediction in our own simulation

**Status:** Proposed (2026-11-13). It becomes Accepted when a client on a second Steam account joins and plays over the Steam relay; until then the Steam path is unproven (see "Evidence").

## Context

The spec calls for host-run sessions of up to 10 players over Steam, fully server-authoritative,
with client-side prediction and reconciliation (spec "Networking"), plus joining and rejoining a
running gate at a checkpoint (ADR 0003). Fish-Net was the front-runner "on the strength of built-in
prediction", unverified for current versions. Ticket 12 asked for a spike that checks current
versions, tries Fish-Net and at least one alternative, and records a choice.

What changed the question: the game already runs one engine-free simulation that takes a command per
player per tick and produces state and events (`SimulationWorld`). It can now capture and restore its
whole state as bytes (`CaptureSnapshot`/`RestoreSnapshot`), and a restored world continues
byte-for-byte like the original. With that, prediction and late join are properties of our simulation,
and the networking library only needs to provide connections, the Steam relay and message delivery.

## Options checked (versions as of 2026-11-13)

| Option | Version | Prediction | Late join | Steam | Verdict |
|---|---|---|---|---|---|
| **Fish-Net, as transport and sessions only** | 4.7.4 (2026-10-16), free licence allows games | Ours (below) | Ours: a welcome snapshot | FishyFacepunch 4.1.0 + Facepunch.Steamworks 2.5.2 | **Chosen** |
| Fish-Net, using its own prediction | 4.7.4 | Built in, per `NetworkObject` (replicate/reconcile) | Built in, per object | same | Ruled out (below) |
| Unity Netcode for GameObjects | 2.x | Docs: no full prediction; "GhostObjects" prediction released as a first iteration on 2026-11-05 | Built in | Community transports | Ruled out: prediction too new to depend on, same per-object model |
| Mirror, PurrNet | n/a | No current, verifiable information found | | | Not tried; nothing they offer is needed beyond a transport |

**Why not Fish-Net's own prediction.** (Ruled out on design grounds, not tried hands-on.) Its prediction replays inputs on individual networked Unity
objects, each with its own state. Our rules live in one simulation over all players, enemies and
projectiles, so using it would mean splitting that state across Unity components and keeping two
copies of the rules, or giving up the headless simulation the spec makes the primary architectural
rule ("Single deterministic simulation core... This is the primary architectural rule and the testing seam").
Our approach keeps the single seam: bots, tests and the network all speak commands and snapshots.
Giving up Fish-Net's per-object model also gives up its built-in area-of-interest culling, one of the
reasons it was the front-runner; interest management becomes ours to build (see Consequences).

## Decision

- **Fish-Net 4.7.4** for connections, the transport layer and delivery. The game sends one Fish-Net
  broadcast type, `NetPacket { byte[] }`, carrying its own messages (`HunterSystem.Netcode.Wire`).
- **FishyFacepunch 4.1.0 over Facepunch.Steamworks 2.5.2** for Steam (SteamSockets, Steam relay);
  **Tugboat** (in Fish-Net) for local UDP testing on one PC.
- **Prediction and reconciliation in our own code** (`HunterSystem.Netcode`, engine-free):
  - The host runs the authoritative world and takes one numbered command per player per tick. When a
    command is late it repeats the last movement and held keys, never a press; the late command is still
    applied when it arrives, so that movement counts twice and a press lands late. Above 6 waiting
    commands it drops the oldest movement-only ones (never one with a press), so lag cannot keep growing.
  - Every 3 ticks it sends each client a full snapshot with that player's last processed command number.
  - A client applies its own commands at once to its predicted world; on each snapshot it restores the
    host's world and replays the commands not yet processed.
- **Join and rejoin** are one path: `Hello` answered by `Welcome` (player id plus a full snapshot), or by
  `Refused` when the session already holds 10 characters. The host identifies players by the identity the
  transport vouches for (on Steam, the connection's SteamID as verified by Steam), not by the key a client
  claims, so nobody can take over another player's character. Over plain UDP there is no verified
  identity and the claimed key is used (local testing only).
- **Untrusted input:** the host counts and ignores any client message it cannot read, and neutralises
  impossible command values (non-finite movement, a stat outside the six).

## Evidence

- Headless (EditMode) tests over a simulated network with 100 ms latency each way, loss and a deliberate
  speed mismatch: own movement shows on the first tick; client and host agree once commands arrive;
  mispredictions are corrected; presses survive; late commands keep you moving; clients see each other;
  a late joiner gets the whole running world; a rejoining player gets their character back.
  Removing reconciliation fails three of them.
- Snapshots: a restored world continues byte- and event-identically to the original over 110 ticks
  of mixed action (projectiles in flight, charges held, modifiers, level-ups). This holds when no world
  operation is queued: queued operations belong to the host and are not in snapshots; their results
  reach clients in the next snapshot.
- Hostile input (session tests): garbage and truncated messages, non-finite movement, an impossible
  stat, a client claiming another's key, a fast client's backlog and a full session are all handled.
  Removing backlog shedding and the identity check fails three tests.
- Two built copies on one PC over Tugboat (`scripts/coop-local-test.ps1`): the client joined as player 2,
  walked about 32 m while attacking, then stood still; its predicted position matched the host's to
  under a millimetre. Largest correction 8 cm. Snapshots 738 bytes (2 players, 3 enemies).
- A built copy hosted on Steam (app 480): Steam initialised and the FishyFacepunch relay server started.
- **Not yet shown:** a client joining over Steam from a second account. This needs the developer's
  second PC (ticket 12's first checkbox stays open until then).

## Consequences and constraints

- Version pins: Unity 6000.6.8f1, Fish-Net 4.7.4 (free), FishyFacepunch 4.1.0 (last released 2024-12,
  compiles against 4.7.4; given a local `FishyFacepunch.asmdef` so our assemblies can reference it),
  Facepunch.Steamworks 2.5.2, Steam test app ID 480 until the game has its own.
- Fish-Net's network tick is set to 60 Hz on both network managers, so sends line up with our
  simulation. At 30 Hz the local round trip was about 13 ticks; at 60 Hz, about 9.
- **Bandwidth:** full snapshots every 3 ticks cost about 15 KB/s per client now. They grow with players,
  enemies and projectiles; the 10-bot stress test (14) must measure this, and delta snapshots or our own
  area-of-interest culling (Fish-Net's is not used) are the likely next step.
- **Spawn point:** joiners appear on a fixed line for now; ADR 0003's "latest activated checkpoint"
  arrives with the gate runtime (17).
- **Steam "Join Game"** works by launching the game with `+connect <SteamID>` (the rich presence value).
  A copy already running at the menu does not yet listen for join requests.
- **Disconnection:** a client notices a closed connection and says so; rejoining means restarting the game
  for now. Characters of disconnected players count towards the 10.
- **Events on clients are lost, not duplicated:** a world keeps only its last step's events, and
  restoring a snapshot clears them, so events from replayed ticks and from the host's own ticks (a kill
  by another player) never reach a client's presentation. Ticket 13 should send the host's events with
  each snapshot for presentation.
- **Remote players** are not predicted; they jump between snapshots. Ticket 13 should interpolate them.
- All messages use the reliable channel for now; commands could move to unreliable with redundancy
  if loss makes reliable delivery stall.
- Replacing Fish-Net later means rewriting two small adapter classes (`FishNetHostTransport`,
  `FishNetClientTransport`); the sessions and the simulation do not change.
