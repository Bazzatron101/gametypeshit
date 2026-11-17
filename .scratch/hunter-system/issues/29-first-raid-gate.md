# 29: First 10-player raid gate

**What to build:** A raid gate for up to ten players with split objectives (groups of two or three in different places) and a phase change, run with the ten-bot harness. Effect-culling rules keep it readable.

**Blocked by:** 14: Bot clients and 10-bot stress test, 18: Handcrafted boss and gate clear reward, 27: Party-size enemy scaling

**Status:** ready-for-human (play the raid with friends)

- [x] Raid supports up to ten players through the existing session flow
- [x] Split objectives require groups to act at the same time in different places
- [x] A final phase uses a positional mechanic everyone handles together
- [x] Effect-culling rules are defined and applied so the screen stays readable
- [x] Runs with ten bots without breaking frame time or bandwidth targets recorded earlier

## Comments

**2026-11-17 (developer, recorded by agent):** Decided:
- **Split objective:** crystals broken together.
- **Final phase:** gather in a shelter from a room-wide blast.
- **Culling:** your own effects bright, teammates' faint.
- **Boss:** a new Goblin King.

The rules are in `docs/adr/0023-raids-split-to-break-the-shield-and-shelter-together.md`.

**2026-11-17 (agent):** Built.

What was built:
- **Raids in the simulation:** a gate can carry a `RaidDefinition`. Its boss's room runs three phases:
  - **Shielded:** one crystal per three players present (ten get four). The shield falls only if every crystal breaks within 8 s of the first; otherwise the broken ones grow back.
  - **Open:** the boss fights as usual.
  - **Final, from 35% health:** the boss channels a room-wide blast. Anyone outside the green shelter loses 80% of their health; dodging doesn't help. The shelter moves each time.
  - Wipes and solo falls start the raid over. Its state is in snapshots, and six new events are on the wire.
- **Content:** the Goblin King, a Shield Crystal, the Raid Barracks and Throne Hall rooms, and the gate `goblin-king-raid` ("The Goblin King's Throne"), on the board at any rank.
- **On screen:**
  - A beam over each standing crystal, and crystals drawn pale blue.
  - The room glows red as a blast nears, with the shelter as a green disc.
  - The boss bar shows "SHIELDED", with a raid line under it (crystals broken and time left, or the blast countdown), plus notices.
- **Effect culling (`EffectCulling`), in every gate:**
  - Teammates' flashes and projectiles are drawn smaller and dimmer: at most 6 flashes at once, none beyond 30 m.
  - Teammates' damage numbers are hidden once 5 or more players are present.
  - Telegraphs and raid marks are never culled.
- **Bots (`RaidSense`):** they shelter first. Each takes a crystal by its place among the players present, and brings it to 30%. They hold until every crystal is that low, then break them together. Bot teammates do the same.

Evidence:
- **`RaidTest`, 29 headless tests:** crystal counts, the shield, regrowing at both edges of the window, blasts (in, out, dodging), the channel, wipes, solo falls, the clear, and snapshots mid-shield and mid-blast.
- **`RaidBotTest`, ten bots through the hub:**
  - Four crystals split 3/3/2/2; the shield broke at 28 s with no regrow.
  - Two blasts caught 0 of 20; there were no defeats, and the King was cleared at 87 s.
  - Host tick 0.27 ms, upload 4.4 KB/s per client, no corrections during the fight.
- **`EffectCullingTest`.**
- **All tests pass:** 825 EditMode and 40 PlayMode.
- **Ten built copies over local UDP** cleared the raid, and every check passed (headless host: 2.6 ms frames, 3.3 KB/s per client). Write-up: `docs/measurements/ten-bot-raid.md`.

Not done:
- **Windowed-host frame time is unmeasured.** The windowed host quit about 90 s in, twice, with no result or crash report. The same setup passes Goblin Warren. Cause unknown.
- **Difficulty is untested by a person.** Bots coordinate perfectly. All the numbers are starting values.
- `ClientSession.LargestCorrection` counts host placements (entering a gate, going home) as corrections. The raid test measures with the new `LastCorrection` instead.

**2026-11-17 (agent, asked by the developer):** Ran the ten-player raid again after ticket 61.
- **Headless, ten bots through the hub:** passed, unchanged (shield at 28 s, 0 of 20 caught by blasts, cleared at 87 s; host tick 0.28 ms; 4.4 KB/s per client).
- **Ten built copies, headless host:** passed every check. Host 2.5 ms frames, 3.4 KB/s per client.
- **Ten built copies, windowed host:** passed every check this time. Host frame time 15.0 ms mean, 56 ms p99. On the same day the ticket-14 windowed script measured 16.4 ms mean and 35 ms p99 on this machine. Upload 3.2 KB/s per client.
- **Fixed:** the network test now counts gear in the bag as a kept reward. Ticket 61 put gear in the raid's pool, so the old modifier-only check failed.
- **Still unknown:** why the first two windowed hosts quit. It did not happen again. The p99 frame time is high with ten copies on one laptop. A host with nine remote clients would be the fair measure.

