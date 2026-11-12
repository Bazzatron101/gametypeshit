# 10: M1 feel review

**What to build:** The developer plays the M1 slice and judges it against the written feel tests before any co-op work starts. This is a decision, not an implementation.

**Blocked by:** 04: Dodge, enemy damage and player defeat, 05: Camera, keyboard and mouse controls, soft lock-on, 07: Data-driven skills with the six-slot bar, 09: Test arena

**Status:** ready-for-human

- [x] Levelling up makes the developer feel an increase in power
- [x] Using skills feels easy and rewarding to pull off
- [x] Verdict recorded: continue to co-op, or adjust the combat and progression first, with notes on what to change

## Comments

**2026-11-13 (developer, recorded by agent):** Verdict: **continue to co-op.** "The game feels good." Levelling up and using skills both pass the M1 feel tests.
- Feel tuning carries on as the developer plays more; it is not a blocker for M2. Tuning lives in data: `Assets/Data/PlayerTuning.asset`, `Progression.asset`, and the skill, modifier and enemy assets, so changes apply in every scene. The test arena (`Assets/Scenes/Arena.unity`) is the place to try them.
- Open feel questions carried forward (from tickets 05, 30): charge tap grace and charge times, move speed while charging, whether the post-dash invulnerability tail should count as a perfect dodge, dodge-cancelled heavy cooldown, input buffering during cooldowns.
