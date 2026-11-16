# 42: Mounts

**What to build:** Riding in the overworld. The first mount comes with the D-rank promotion and runs at 2× sprint, 16 m/s (ticket 36, ADR 0015). The mount belongs to the character (ADR 0005), so a guest brings their own; a guest still at E-rank has none and walks. There is no riding double.

**Blocked by:** 38: The overworld as a third kind of place, 40: Load the overworld in tiles around players

**Status:** ready-for-agent

- [ ] Mount and dismount commands in the simulation, predicted like other movement (ADR 0004)
- [ ] Mounted speed is a configuration value (16 m/s to start), beside walking and sprint
- [ ] Mounts work in the overworld only; entering a gate or the town dismounts
- [ ] Getting hit, or other rules decided in triage, knock a rider off
- [ ] The mount is saved on the character record and given at the D-rank promotion (ticket 25)
- [ ] Mounted riders follow the same ground rules as walkers (ticket 37); whether a mount can climb is decided in triage
- [ ] Tests: mounting, speed, dismount on entering a gate, an E-rank character has no mount, the save round trip

**2026-11-16 (developer, recorded by agent):** Triaged: build now ("this still feels very basic").
