# 0021: Snapshots go as compressed differences from the last world sent

**Status:** Accepted (2026-11-16)

## Context

Ticket 31: a ten-player session's host sent every client the whole world 20 times a second, about 880 KB/s
(7 Mbit/s) of upload with the party together (`docs/measurements/ten-bot-stress-test.md`). Ticket 41 cut that
out in the overworld by sending each client only what is near it, but in a gate or the hub nothing was cut.

Ticket 31 listed compression, delta snapshots against an acknowledged snapshot, fewer full snapshots, and
culling. Every message already goes over Fish-Net's reliable, ordered channel (`FishNetTransports`), and the
simulated network in the tests is reliable and ordered too.

## Decision

- **Every snapshot and welcome is Deflate-compressed** (fastest level).
- **Each snapshot is written against the last world sent to the same connection,** the welcome or the previous
  snapshot: the world goes as its byte-by-byte XOR with that one, mostly zeros, which compress to very little.
  When the two worlds differ in length (an enemy arrives or falls, a player joins or comes into range) the whole
  world goes instead, still compressed. A byte in the message says which.
- **No acknowledgements:** because the channel is reliable and ordered, a client always has the world the
  host last sent it. The client rebuilds each snapshot's world from the one before, in arrival order.
- In a gate or the hub, clients that were last sent the same world share one encoded message.

## Consequences

- **Snapshots must stay on a reliable, ordered channel.** Moving them to an unreliable channel would need
  acknowledged baselines (the client sending back the snapshot tick it has) instead of "the last one sent".
- A lost or reordered snapshot cannot happen silently: a client asked to rebuild against a world of the
  wrong size refuses the message.
- The host compresses one message per client per snapshot in the overworld (each client's is different),
  and usually one in a gate. That is measured in the stress tests' host tick time.
- XOR only helps when the layout does not move. Arrivals and departures cost a whole compressed world for
  that one snapshot; with the current content that is rare next to 20 snapshots a second.
