# 0010: Gates scale their enemies by the party present when they spawn

**Status:** Accepted (2026-11-14)

## Context

The spec scales enemies "in tiers (solo, 2 to 4 players, 5 to 10 players) through health, damage,
and spawn counts", never scales players or rewards, and keeps every normal gate solo-completable
(story 73, "Deaths, scaling, and rewards"). ADR 0003 adds that a join mid-gate changes only enemies
spawned afterwards. Ticket 27 had to make this exact.

## Decision

- **Three tiers by players present** (away players do not count): solo (1), small (2 to 4), large (5
  to 10). Each tier has a health, a damage and a spawn percentage, in the game's content
  (`PartyScalingAsset`).
- **Solo is the gate as authored** (100% each), so a gate that can be cleared solo stays clearable
  solo. Content validation checks that no tier is easier than the one below it.
- **Starting numbers** (the agent's, to tune by playing): small 160% health, 115% damage, 150% enemies;
  large 250% health, 130% damage, 200% enemies.
- **The tier is taken when enemies spawn:** when a room fills, or when a boss calls its adds. Enemies
  already alive keep what they had, so a friend joining never changes a fight under way.
- **What scales:**
  - each enemy's health, kept on the enemy itself;
  - its damage, both ordinary hits and boss attacks;
  - how many enemies a room (or a boss's call for help) brings. The authored enemies come first, in
    their authored order, so solo is exactly the gate as authored. Extra enemies are copies spread
    evenly across the authored mix, each a step to one side of the one it copies and always inside
    its own room.
- **What does not:** a boss is never copied (one boss, tougher). Players' stats are never touched, and
  XP per kill is the same in any party. A bigger party meets more enemies, so the party as a whole
  earns more.
- Whole-number maths, rounding halves up, so every machine scales alike.
- Enemies spawned outside gates (the test arena, the open-field bot fight) are not scaled.

## Consequences

- The first numbers came from no playtest. A two-player run of Goblin Warren meets 29 enemies instead
  of 19, and the bots needed 41 s instead of 32 s for three players. Whether that feels right is for
  the M3 feel review (ticket 21).
- More enemies means bigger snapshots in big parties (ticket 31's bandwidth problem grows with it).
- A room filled for a party keeps its strength if the party shrinks: a player whose partner leaves
  mid-fight faces that room (or that boss) at the party's tier, alone. The next room is solo again.
  Every gate stays soloable from the start; a fight that began as a party may be harder to finish alone.
- Raid gates (ticket 29) will want their own mechanics on top; these tiers still apply to them.
