# 47: The main attack feels hard to use

**What to build:** Make the main (light) attack easy and satisfying to land. From the developer's co-op playtest, 2026-11-15: "using the main attack feels very difficult".

**Blocked by:** None (can start immediately)

**Status:** done

- [x] Find out what makes it hard (see the questions below)
- [x] Tune or change the attack so it lands reliably, in data where possible (`PlayerTuning.asset`, the weapon assets)
- [x] The developer tries it again and it feels good

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

**2026-11-16 (agent):** Built.
- **Reach:** the Iron Sword's slash goes from 2.4 m to 3.2 m, and its thrust from 3.6 m to 4.4 m (`IronSword.asset`). Soft lock-on reaches 4.5 m (was 4) so it turns you toward anything the thrust can hit (`PlayerTuning.asset`). All data; change them in the Inspector to taste.
- **Seeing a hit land:** every hit shows a damage number over the enemy, rising and fading (`DamageNumbers`, added by the runner in every scene). Your own hits are big and yellow and pop in; other players' and teammates' are small and white. The enemy's white flash lasts a little longer (0.15 s), and its body swells for a moment when hit (`EnemyView`).
- Not done: a hit sound (the game has no audio yet) and a hit pause.
- Tests: PlayMode `ArenaTest.TheSwordReachesAnEnemyAFewMetresAwayAndTheHitShowsItsNumber` (a goblin 3 m away, out of the old reach, is hit, and your number shows). EditMode: 637 passed; PlayMode: 36 passed.
- **To check:** play the arena as a Warrior and see whether the slash now feels easy to land, and whether the numbers are too much.

**2026-11-17 (developer playtest, recorded by agent):** "The weapon did feel better to use." The longer reach works.
