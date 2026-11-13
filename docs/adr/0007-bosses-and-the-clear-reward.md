# 0007: Bosses read the room; the clear reward is a modifier from the gate's pool

**Status:** Accepted (2026-11-13)

## Context

Ticket 18 asks for a handcrafted boss with "distinct, readable attack patterns" that ends the gate
and grants "a guaranteed placeholder reward", working solo and in two-player co-op. Loot and items
arrive in M4, so the reward needed a stand-in that will not have to be thrown away. The developer
chose the first boss's moveset, its enrage and the reward on 2026-11-13.

## Decision

**Bosses**
- A boss is an enemy with a `BossDefinition`: a set of telegraphed attacks, a slow turn rate, and
  an enrage. It is placed in a gate by a room piece, like any other enemy, so wipes and restarts
  treat it the same way.
- Every boss attack is **aimed when it starts and marked on the floor until it is over**. A sweep or
  a slam lands on whoever is inside its marked area when the windup ends. A charge's lane is marked
  only as far as it will really run (a wall may cut it short) and stays marked while it runs; it hits
  each player it passes, once. The same `Covers` test hits players, draws the telegraph and guides bots.
- **The boss picks the attack whose answer is obvious from where players stand** (developer's choice
  of moveset: the Warlord):
  - a **slam** (a circle all round it) when two or more players crowd it, or its target has got
    behind it: the answer is to get out;
  - a **sweep** (an arc in front) when its target is in front and in reach: the answer is to get
    behind it;
  - a **charge** (a straight lane) at a target in front and out of reach: the answer is to step aside.
    A charge stops at walls.
- It turns slowly (60° a second for the Warlord), so getting behind it is a real answer. It strikes
  only at targets in front of it; someone behind and out of reach makes it turn towards them first.
- **Enrage** (developer's choice): once, at half health. From then on its windups and cooldowns take
  70% of the time, and its adds (four goblins) join. The adds belong to the boss's room, so a wipe
  takes them away with it.
- Telegraphs are drawn from the world's state, not from events, so the host, a guest and a late
  joiner see the same marks at the same moment.

**Completing the gate**
- A gate with a boss is cleared when its bosses are defeated, whether or not every room was. A gate
  without one is cleared when every room is (ADR 0006). The way onward is always open, so a party may
  skip rooms; the boss is the goal.
- When the boss falls, whatever still fights in its room (its adds) goes with it, and its room counts
  as cleared, so no later wipe can bring the boss back.
- Once the gate is cleared, dying costs nothing: a solo player gets up at the latest checkpoint
  without using a revive, the gate never restarts, and the reward is never given twice.

**The clear reward** (developer's choice: a modifier from the gate's pool)
- Each gate lists a reward pool of modifiers. On the clear, every player present draws one they do
  not already have; it is added to their character and saved with it (ADR 0005).
- The draw is a fixed function of the tick, the player and the attempt, so every machine that steps
  that tick draws the same modifier, and a guest's prediction never disagrees with the host.
- No repeats until the whole pool is owned. After that the clear gives nothing; selling or upgrading
  duplicates arrives with loot (spec, M4).
- Away players are not present, so they are not rewarded (ADR 0003: "goes to everyone present").

## Consequences

- This is the shape loot will take (spec stories 32 and 33: a fixed pool, a random draw, no repeats
  until it is owned). When items exist, the pool can hold items as well as modifiers, and the draw
  and bad-luck rule stay.
- Modifiers are permanent. A few clears of a gate give a character its whole pool, so pools must stay
  small and modifiers must be balanced as permanent upgrades, not as drops.
- Bots dodge every telegraph perfectly. They beat the Warlord with no defeats (three bots in 32 s, one
  in 50 s), which proves the fight can be won solo and in a party, but says nothing about difficulty
  for a person. That needs the developer's hands.
- Damage numbers and the boss's health are not scaled by party size yet (ticket 27).
