# 51: Gear that changes stats

**What to build:** Let a piece of gear raise (or lower) the six stats (Strength, Agility, Vitality, Intelligence, Sense, Luck) while it is worn, so that armour can do things like "+5 Vitality". Today gear only carries skill modifiers aimed at a tag or a skill (ticket 23), so a helmet cannot add health and boots cannot add speed.

**Blocked by:** 23: Gear and tag synergies (done)

**Status:** ready-for-agent

- [ ] A gear definition can list stat bonuses (stat and amount), set in the gear asset like its modifiers
- [ ] Worn gear's stat bonuses count everywhere a stat already has an effect (Strength and Intelligence and Agility damage scaling, Vitality max health, Agility move speed), and stop counting when the piece is taken off
- [ ] Bonuses from gear stay separate from points the player spent: they never change free points or the character's saved stats
- [ ] The stat screen (Tab) shows the total with the gear part apart (for example "Vitality 14 (+4 gear)")
- [ ] The gear screen (I, ticket 48) lists a piece's stat bonuses in plain words and includes them in the compare view (green gains, red losses)
- [ ] At least a few existing armour pieces get stat bonuses, so it shows up in play
- [ ] Works the same for the host and a guest (snapshots carry what is needed; the stats come out the same on both)
- [ ] Putting on a Vitality piece is never a free heal: current health stays the same when max health goes up, and is cut to the new max when it goes down
- [ ] Bonuses can be negative, but a piece with a minus also has a clear upside that makes it worth wearing (content validation flags a piece whose bonuses add up to a net loss with nothing else to offer)
- [ ] Sense and Luck bonuses are allowed; they show on the stat and gear screens even though the stats do nothing yet
- [ ] Bots and companions weigh stat bonuses (by their role's stats) when picking gear
- [ ] Headless tests: equip and unequip change the effective stat; max health and speed follow; no heal from swapping; spent points and the save are unchanged; snapshot round trip

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
