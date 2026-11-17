# 0023: Raids split up to break the shield and shelter together; teammates' effects are drawn faint

**Status:** Accepted (2026-11-17)

## Context

The spec promises "10-player raid gates with split objectives and phases" that "stay readable at ten players"
(stories 75 and 76), run with the ten-bot harness (story 82), and says effect-culling rules "must be defined
before the first raid is built". Ticket 29 asks for groups of two or three acting at the same time in different
places, a final phase with a positional mechanic everyone handles together, and culling rules, without breaking
the frame time and bandwidth recorded by tickets 14 and 31.

The developer chose (2026-11-17):
- **split objective:** crystals that must be broken together;
- **final phase:** gather in a shelter from a room-wide blast;
- **culling:** your own effects bright, teammates' faint;
- **boss:** a new Goblin King.

## Decision

**A raid is a gate with a raid definition** (`GateDefinition.Raid`, authored on the gate asset). Its fight is the
boss's room. Everything else about the gate is unchanged: rooms, checkpoints, party scaling (ADR 0010), the
death rules (ADR 0006), the boss's own attacks and enrage (ADR 0007), the clear reward, joining at checkpoints.

**Phase 1, shielded.** When the boss's room fills, the boss is shielded: no damage gets through.
- Crystals stand far apart in the room, **one for every three players present** when it fills (rounded up, at
  least one, at most one per authored place). Ten players meet four, so they split three, three, two and two.
  A solo hunter meets one, so a raid can still be tried alone.
- A crystal never moves or attacks. Its health is scaled for the party like any enemy. It gives no XP.
- **The shield falls only when every crystal is broken within the window of the first one breaking** (8 s for the
  Goblin King). Breaking one starts the clock. If the rest are not broken by the end of the window, the broken ones
  grow back whole and the clock stops. A party that rushes one crystal, or sends everyone to one corner in turn,
  never brings the shield down.

**Phase 2, open.** The boss fights as any boss does.

**Phase 3, final.** At 35% of its health the boss also stops to wind up a blast that fills its room.
- A **shelter** circle is marked on the floor (3.5 m across for the King, room for ten), at the next of the
  authored places, round and round. The first comes 2 s into the phase, then 12 s after each blast lands.
- While it winds up (4 s) the boss stands and channels: it starts no attacks. It finishes an attack under way first.
- When it lands, **everyone present outside the shelter loses 80% of their maximum health**. A dodge or a jump does
  not help: the whole room is hit. Two blasts outside fell a player.

**Starting over.** The raid lives in the boss's room, so it starts over with it: a co-op wipe, or a solo fall
(ADR 0013, including a fall to the blast), empties the room and the raid goes back to waiting. The next time the
room fills, the crystals rise again for the party then present. The clear ends the raid; no blast lands after it.

**Shared state.** The raid's phase, crystals, regrow clock and shelter are in the world's state and its snapshots,
so the host, every guest and a late joiner see the same marks at the same moment. Bots read the same state.

**Bots** (stress-test bots and bot teammates) play the raid by `RaidSense`: a coming blast comes first (into the
shelter, and stay); while the boss is shielded each takes a crystal by its place among the players present; a group
brings its crystal down to 30% and holds until every crystal is that low, then all break theirs. People would say
this out loud; bots read it from the world.

**Effect culling** (`EffectCulling`, presentation only; it never changes what happens):
- **Yours:** every flash, projectile and damage number, as before.
- **Teammates' skill, weapon and explosion flashes:** drawn at 65% size and dimmed toward the floor's grey; at most
  six of theirs on screen at once (newer ones are skipped while six are up); none further than 30 m from you.
- **Teammates' projectiles:** drawn at 65% size and dimmed; not beyond 30 m.
- **Teammates' damage numbers:** hidden once five or more players are present.
- **Never culled:** boss telegraphs, raid marks (crystal beams, the danger floor, the shelter), enemies' wind-ups,
  the HUD's raid line.
These rules apply in every gate, not only raids; with four or fewer players only the size, dimming, cap and range
apply.

## Consequences

- **Measured with ten bots** (headless, host plus nine clients, 100 ms each way, real content, from the hub):
  four crystals, split 3/3/2/2, shield broken in 28 s with no regrow, nobody caught by the blasts, the King beaten
  87 s in. Host tick 0.28 ms mean; host upload 4.4 KB/s per client during the raid, under ticket 31's 7.7 KB/s and
  the 16 KB/s guard. Ten built copies over local UDP cleared it too (headless host: 2.6 ms frames, 3.3 KB/s per
  client). See `docs/measurements/ten-bot-raid.md`.
- **Bots coordinate perfectly** and dodge every telegraph, so the fight's difficulty for people is unknown. The
  numbers (window, thresholds, blast damage, crystal and King health) are the agent's, to tune by playing.
- Crystal places and shelter places are authored per raid, from the boss room's centre. The straight-line gate
  layout (ADR 0006) means "different places" are the corners of one big hall; a raid with real side rooms needs the
  layout step ADR 0006 mentions.
- Culling is decided per viewer from what is on their screen; the host and guests cull alike. It is not tied to the
  raid, so a busy four-player gate also benefits.
- A "roles by build" raid design stays parked (spec).
