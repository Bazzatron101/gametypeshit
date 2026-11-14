# 0014: Bot teammates play their roles and learn as they play

**Status:** Accepted (2026-11-14)

## Context

The developer asked for "5 bot teammates that play better as the game goes on and they all play to
their roles the best way possible". The bots from ticket 14 (`BotBrain`) stand in for other players
in tests: they rush the nearest enemy, whatever their class, and always play the same.

## Decision

- **Who:** the host brings **five teammates** (`CompanionParty`): one of each class, plus a fifth
  that evens out the front line (Warrior, Rogue) and the back line (Archer, Wizard) with the player's
  own class. A party of six is then three and three. They have names (Garrick, Vex, Wren, Orin, and
  Brannoc or Kestrel for the fifth).
- **Roles,** from the class. Enemies go for the nearest player, so a role is mostly where it stands:
  - **Tank** (Warrior): goes in first and stands closer than anyone (0.8 m), takes the boss head-on, and
    goes for the enemy nearest a squishier ally. It builds Vitality and Strength.
  - **Skirmisher** (Rogue): finishes weak enemies, kills a boss's adds first, and fights the boss from
    behind. It builds Agility and Luck.
  - **Marksman** (Archer): focuses the weakest enemy in range from about 8 m and backs off from anything
    within 3.5 m. It builds Agility and Sense.
  - **Caster** (Wizard): aims at the thickest pack from about 7 m and keeps long-cooldown spells for
    bosses and crowds. It builds Intelligence.
  - Everyone follows the host in formation (tank in front, ranged behind) and marks ready in the hub at
    once. When the host is down, they push on through the gate, which brings the host back at the next
    checkpoint.
- **Getting better:** each teammate has **experience** (kills, bosses, clears, falls, dodges, time
  fighting). Its **proficiency** grows from 0.2 (rookie) towards 1, quickly at first and then levelling
  off: about 0.45 after the first gate, 0.75 after four and 0.9 after eight. Proficiency sets:
  - how often it notices a telegraphed hit (44% as a rookie, nearly always at its best);
  - how often it swings and thinks about skills (a slow beat as a rookie, every chance at its best);
  - target choice (the nearest enemy below 0.4, its role's choice above);
  - skill judgement (from 0.5: areas wait for two enemies, it charges when no hit is due, and a dodge
    while charging becomes a perfect-dodge counter; from 0.7: long-cooldown skills wait for a boss or a crowd);
  - boss play (from 0.5: the back line walks out of marked areas, and the front line dodges through at
    the last moment);
  - how tightly it keeps its place.
- **Levels:** XP is shared, so teammates level with the player. A teammate behind the host's level when
  a session starts catches up ("it trained while you were away"). It gains its class's stats for the
  levels and the free points, which it places by its role.
- **Where they run:** on the host, inside `HostSession`, as ordinary players. Their commands go through
  `World.Step` like anyone's, guests see them in snapshots, and they count for party-size scaling (ADR
  0010) and towards the 10-player limit. With all five, four places are left for guests.
- **Saving:** `worlds/teammates.hsp` beside the host's world, with their characters and experience. It is
  saved when a gate is cleared and when the session ends. Teammates left at home keep their save.
- **Switching them on:** a toggle on the co-op menu, on by default. Automated runs (`-coop host`) bring
  none unless given `-companions N`, so the stress and gate scripts keep their party sizes.

## Consequences

- With six players the 5-to-10 tier applies (enemies at 250% health). In the first gate a level-1 party of
  six clears in about 40 s without a fall, whether the teammates are rookies or elites. Elites take about
  a fifth of the damage, and their tank holds the boss about 80% of the time (rookies: under half). Clear
  speed is about the same. Whether that feels right is for the feel review (ticket 21).
- A full session with teammates refuses a sixth guest. Teammates do not yet step aside for people joining.
- Teammates have no names in the world view yet (the HUD shows player numbers). The co-op panel lists them
  with their role and rank.
