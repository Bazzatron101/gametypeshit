# 33: Five bot teammates who play their roles and learn

**What to build:** Five bot teammates for the host who play to their class's role and get better the more they play (requested by the developer, 2026-11-14). The rules are in `docs/adr/0014-bot-teammates-play-their-roles-and-learn.md`.

**Blocked by:** None

**Status:** done

- [x] Five teammates, one of each class plus one that evens the front and back lines
- [x] Each plays its role: tank holds enemies, Rogue flanks and finishes, Archer and Wizard fight from range
- [x] Experience from play raises proficiency, which changes how they react, aim, use skills and position
- [x] They level with the player, place points by role, and are saved with the host's world
- [x] A toggle on the co-op menu (on by default); automated runs opt in with `-companions N`

## Comments

**2026-11-14 (agent):** Built.

- `Assets/Scripts/Netcode/Companions/`: `CompanionBrain` (the play), `CompanionRole` (roles and stat plans),
  `CompanionRecord` (experience, proficiency and the save format), and `CompanionParty` (the roster and catching up).
- `HostSession.AddCompanion` runs them on the host as ordinary players. `SimulationRunner.StartHost` loads
  them, adds them and saves them. `CoopLauncher` has the toggle, the `-companions N` argument, and a status line.
- `CompanionTest` (16 tests): the roster for every player class, proficiency growth, the save round trip,
  catching up, the host session, and three runs of the first gate.

Evidence from the first gate (a melee bot leads in place of the player):

| Teammates | Clear | Damage taken by teammates | Boss nearest the tank |
|---|---|---|---|
| Rookies | 40 s | 388 | 46% of the boss fight |
| Elites | 40 s | 78 | 83% |

Not done:
- How it feels with a person leading. Try it.
- Teammates do not step aside when a guest joins a full session.
- The world view does not show names.
- This builds on the uncommitted aim-with-the-camera work (`PlayerCommand.AimYawDegrees`), which teammates use to aim skills.

**2026-11-14 (developer, recorded by agent):** Played with the teammates: "thats really enjoyable and it works well".

**2026-11-17 (agent, asked by the developer to close what can be closed):** Closed: you played with the teammates ("thats really enjoyable and it works well") and every box is ticked.
