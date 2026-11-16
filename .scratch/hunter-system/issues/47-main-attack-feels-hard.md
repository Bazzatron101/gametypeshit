# 47: The main attack feels hard to use

**What to build:** Make the main (light) attack easy and satisfying to land. From the developer's co-op playtest, 2026-11-15: "using the main attack feels very difficult".

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [x] Find out what makes it hard (see the questions below)
- [ ] Tune or change the attack so it lands reliably, in data where possible (`PlayerTuning.asset`, the weapon assets)
- [ ] The developer tries it again and it feels good

## Questions for the developer

1. Which weapon were you using: sword, bow, staff, or none?
2. What makes it hard: hitting the enemy at all (aim, range, the camera), the timing (windup, how fast it repeats), not being able to tell whether it hit, or something else?
3. Solo, as host, or as the guest?

## Comments

**2026-11-15 (agent):** Opened from the playtest notes on tickets 13 and 46.

**2026-11-15 (developer, recorded by agent):** Answers:
1. **The sword,** playing a Warrior.
2. **The range** is too short, and it was **hard to tell whether a hit landed.** Animations will make hits easier to see.
3. **As the host,** so this is not lag or prediction.

Next: give the sword's light slash more reach (`IronSword.asset`, tests in `WeaponTest`), and make a landed hit clearer until animations come (a hit flash or a short pause on the enemy, a sound, a damage number).
