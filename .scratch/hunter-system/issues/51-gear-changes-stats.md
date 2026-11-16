# 51: Gear that changes stats

**What to build:** Let a piece of gear raise (or lower) the six stats (Strength, Agility, Vitality, Intelligence, Sense, Luck) while it is worn, so that armour can do things like "+5 Vitality". Today gear only carries skill modifiers aimed at a tag or a skill (ticket 23), so a helmet cannot add health and boots cannot add speed.

**Blocked by:** 23: Gear and tag synergies (done)

**Status:** ready-for-human (try the Warlord's Plate: Tab shows the Vitality from gear; I compares pieces)

- [x] A gear definition can list stat bonuses (stat and amount), set in the gear asset like its modifiers
- [x] Worn gear's stat bonuses count everywhere a stat already has an effect (Strength and Intelligence and Agility damage scaling, Vitality max health, Agility move speed), and stop counting when the piece is taken off
- [x] Bonuses from gear stay separate from points the player spent: they never change free points or the character's saved stats
- [x] The stat screen (Tab) shows the total with the gear part apart (for example "Vitality 14 (+4 gear)")
- [x] The gear screen (I, ticket 48) lists a piece's stat bonuses in plain words and includes them in the compare view (green gains, red losses)
- [x] At least a few existing armour pieces get stat bonuses, so it shows up in play
- [x] Works the same for the host and a guest (snapshots carry what is needed; the stats come out the same on both)
- [x] Putting on a Vitality piece is never a free heal: current health stays the same when max health goes up, and is cut to the new max when it goes down
- [x] Bonuses can be negative, but a piece with a minus also has a clear upside that makes it worth wearing (content validation flags a piece whose bonuses add up to a net loss with nothing else to offer)
- [x] Sense and Luck bonuses are allowed; they show on the stat and gear screens even though the stats do nothing yet
- [x] Bots and companions weigh stat bonuses (by their role's stats) when picking gear
- [x] Headless tests: equip and unequip change the effective stat; max health and speed follow; no heal from swapping; spent points and the save are unchanged; snapshot round trip

## Notes

- **Where it plugs in:** every stat effect goes through `SimulationWorld.PointsAboveStart` (`SimulationWorld.Progression.cs`). Adding worn gear's bonuses there should reach all the current effects at once.
- **Gear definition:** `GearDefinition` in `Assets/Scripts/Simulation/Gear.cs`, with its asset in `Assets/Scripts/Game/GearDefinitionAsset.cs`.
- **To settle in triage:**
  - **Health when Vitality changes:** when you put on or take off a Vitality piece, does current health stay the same, keep its percentage, or (when taking it off) get clamped to the new max? Spending a Vitality point today arrives "filled". A Vitality piece shouldn't be a free heal you can repeat by swapping.
  - **Negative bonuses:** allowed (for example a heavy chestpiece with -2 Agility)? If so, can a stat go below its starting value?
  - **Sense and Luck:** they still have no effect (ticket 06). Gear bonuses to them would show but do nothing until they do. Leave them out for now, or allow them?
  - **Bots and companions:** do their gear picks (by role) need to weigh stat bonuses?

## Comments

**2026-11-16 (agent):** Opened from ticket 48, which left this out ("gear changing stats like Vitality (gear only has skill modifiers today)").

**2026-11-16 (developer, recorded by agent):** Triaged, the four questions answered:
1. **Health:** putting gear on must not be a free heal. The agent's reading: current health stays the same when max health goes up, and is cut to the new max when it goes down. Change this if a different rule feels better in play.
2. **Negative bonuses:** allowed, "but there should be something that makes the item worth wearing". A minus always comes with an upside. Not settled: whether a stat can go below its starting value. The agent's default is yes, but the effects keep sensible floors (max health at least 1, move speed and damage above zero).
3. **Sense and Luck:** allowed on gear now; "we will give them a purpose later on".
4. **Bots:** yes, they weigh stat bonuses when picking gear.

**2026-11-17 (agent):** Built.

What was built:
- **Gear lists stat bonuses** (`StatBonuses` on `GearDefinition`; `statBonuses` on the gear asset, one entry per stat, set in the Inspector).
- **They count everywhere a stat has an effect.** `PointsAboveStart` now counts the character's own stat plus worn gear's (`EffectiveStat`), so Strength, Intelligence and Agility damage, Vitality max health and Agility move speed all follow gear at once. Taking a piece off takes its bonus away.
- **Own stats stay apart.** `Player.Stats` is still only the start, class stats and spent points; free points and the save never change. `PlayerState` has `GearStats` (gear's part) and `TotalStat(stat)`.
- **No free heal.** Equipping or unequipping recomputes max health: current health stays when the max rises and is cut to the new max when it falls.
- **Floors** for a stat pulled far below its start: max health at least 1; move speed and stat-scaled damage at least a tenth.
- **Content rules** (checked when the game starts, like the other gear rules): a bonus must be a real stat and not 0, at most one per stat, and a piece whose bonuses add up to a loss must also have a modifier or an attack.
- **Stat screen (Tab):** the total, with gear's part beside it in green or red, e.g. `14 (+4 gear)`.
- **Gear screen (I):** each bonus as a line ("+4 Vitality", "+2 Sense (no effect yet)"). The compare view shows each stat's net change ("+ 3 Vitality" green, "− 2 Agility" red). For an empty slot it no longer says "you only gain" when the piece takes a stat away.
- **Bots** score a piece's bonuses to the stats their role puts points into, point for point. A bot leaves a slot empty rather than wear a piece that scores below zero for its role.
- **Content:** Warlord's Plate +4 Vitality, -1 Agility; Cinder Robe +3 Intelligence; Cinder Hood +2 Intelligence; Cinder Boots +2 Agility; Hunter's Quiver +2 Sense.
- **Host and guest:** snapshots already carry worn gear and max health, so the stats come out the same on both. No format change.

Evidence: `GearStatsTest` (23 tests) covers equip and unequip, pieces adding up, max health, speed, Strength damage, the floors, Sense and Luck, no heal from swapping, cut to the new max, spent points and the save unchanged, a loaded character, the snapshot round trip, the content rules, the real armour, the gear screen's lines and compare view, and bots. The full EditMode run: 740 of 743 pass. The 3 failures are in `ContentValidationTest`, in the ticket 54 specialty content another session is adding at the same time (its new skills and trial scenes not yet in the content list), not in this ticket's code.

Not done: the numbers are starting values, to tune by playing. Gear descriptions were left as written; the bonuses show as their own lines under them.
