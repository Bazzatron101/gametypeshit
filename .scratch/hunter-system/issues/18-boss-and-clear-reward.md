# 18: Handcrafted boss and gate clear reward

**What to build:** A handcrafted boss fight that ends the gate and grants a guaranteed placeholder reward.

**Blocked by:** 17: Gate runtime

**Status:** ready-for-human (play the Warlord solo and with a friend)

- [x] Boss has distinct, readable attack patterns
- [x] Defeating the boss completes the gate and grants a reward
- [x] The fight works solo and in two-player co-op

## Comments

**2026-11-13 (developer, recorded by agent):** Decided:
- **Moveset:** a Goblin Warlord that sweeps, slams and charges.
- **Enrage:** at half health.
- **Reward:** a modifier from the gate's pool.

The rules are in `docs/adr/0007-bosses-and-the-clear-reward.md`.

**2026-11-13 (agent):** Built.

What was built:
- **The boss in the simulation:** `BossDefinition`, with attacks that are aimed at the start and hit the marked area. It chooses slam, sweep or charge from where the players stand, turns slowly, and enrages once (faster, four goblins join).
- **Clearing the gate:** defeating the boss clears it. Each player present draws a modifier they don't have from the gate's pool. The draw is the same on every machine.
- **Content:** `BossDefinitionAsset`; the Warlord Chamber room piece and an Antechamber checkpoint before it; Goblin Warren's reward pool.
- **On screen:** `BossTelegraphView` marks each attack on the floor and fills it as the hit approaches. The boss is drawn larger, with a health bar ("ENRAGED"), and there are notices for the enrage and the reward.
- **Bots** dodge out of marked areas.

Evidence:
- 29 boss tests, including snapshots mid-charge. Breaking each rule fails its test.
- Bots beat the Warlord headless: three in 32 s, one alone in 50 s. The boss used all three attacks across the two runs and enraged once in each.
- `scripts/coop-gate-test.ps1`: a built host and guest cleared the gate, boss included, over local UDP. Each got a different reward and kept it.
- A scene test checks the telegraph, the health bar and the reward notice.

Not done:
- **Difficulty is untested by a person.** Bots dodge perfectly.
- **Party-size scaling** is ticket 27.
- **Loot proper** is M4.

**2026-11-13 (agent), after review:**
- **Fixed:**
  - **The charge** follows its drawn lane: the lane is marked only as far as a wall allows and stays marked while the charge runs. A charge needs its target in front, so the boss turns first.
  - **When the boss falls,** its goblins go with it and its room counts as cleared. A wipe after the clear can no longer bring it back.
  - **After the clear,** a solo death costs nothing: no revive used, no restart, no second reward.
  - **A dead boss** stops its attack and calls no adds, even if it enraged in the same tick.
  - **Snapshots** keep a boss's adds sharing one definition after a restore. Host and guest snapshots stay identical.
  - **The telegraph's meshes** are cached by shape, so guests no longer leak them.
  - **A charge with no speed** ends at once. Content validation requires charge speed.
- **Left as is (judgement calls):**
  - The boss hit repeats the ordinary enemy hit.
  - Attack fields travel as a group rather than as one type.
  - The boss's facing uses floating-point trigonometry. A guest's boss can differ slightly until the next snapshot corrects it; the reward draw does not depend on it.
  - **Once the pool is owned the clear gives nothing.** That is deferred to loot in M4, as ADR 0007 says.
- **Evidence:** 375 EditMode and 22 PlayMode tests pass. Bots beat the boss headless, and a built host and guest cleared the gate over local UDP with rewards kept.

