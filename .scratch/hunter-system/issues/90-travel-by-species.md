# 90: Each species travels its own way

**What to build:** The mount from ticket 42 becomes a way of travelling that suits the hunter's species (ticket 81). Birds and gliders fly, fast land animals run on their own legs, and slow animals hop on a bicycle. From the developer, 2026-11-17: "birds fly and fast land animals run but if they are slow they hop onto a bike".

**Blocked by:** 81: Hunters are Australian animals, with a species bonus

**Status:** done

- [x] The travel style is part of the species data (ticket 81), so adding a species needs no code:

  | Style | Species | Look |
  |---|---|---|
  | Fly | Cockatoo, sugar glider | Wings or gliding membrane out, body level |
  | Run | Kangaroo, Tasmanian devil | Bounding or galloping on its own legs, no mount |
  | Bicycle | Wombat, koala, platypus, quokka | Pedals a bicycle |

- [x] **Same speed for everyone:** every style goes at `SimulationConfig.MountSpeed` (16 m/s), so co-op players stay together. The style is the look and the ground rules, not a speed edge.
- [x] **Run and bicycle** keep ticket 42's rules: they follow the ground, do not climb, and a hit, attack, skill or dodge puts the hunter back on foot.
- [x] **Fly:** a flyer leaves the ground and keeps a height above it, crossing cliffs, rivers and steep ground that stop runners and riders.
  - **Flight gets freer with rank.** At first a flyer only skims low over the ground at a fixed height. Each rank promotion (ticket 25) raises how high they may go and lets them climb and dive freely within it (jump to climb, crouch or a held key to dive). The heights per rank live in data and can be changed in the Inspector.
  - Landing: getting off, or being knocked off by a hit, drops the hunter to the ground below with a normal fall (the sugar glider's glide perk still applies).
  - Entering a gate still puts a flyer on foot, and nobody flies out past the map's edge. Whether flyers may cross the town walls (ticket 89) is for triage.
  - The tiles around a high flyer still stream in (ticket 40); the view from higher up may need a longer draw distance, measured, not guessed.
- [x] Who gets it is unchanged: the D-rank promotion grants "a mount" (`GrantMount`), and the style comes from the species. "Mount" in the UI becomes the species' word ("Fly", "Run", "Ride") on the R prompt and the `MountHud` messages.
- [x] Snapshots and prediction carry flight height, so a guest's flight is predicted with no correction (ADR 0004).
- [x] Bot teammates use their species' style and keep up with a flying leader by following on the ground.
- [x] Greybox looks until real art: wings or membrane from flat shapes, a bicycle from cylinders and boxes, a running pose by tilting the body; all labelled "AI" (memory rule).
- [x] Tests: each species gets its style; all styles go 16 m/s; a runner and a rider stop at a cliff where a flyer crosses it; a low-rank flyer cannot climb above its height and a higher rank can; a hit drops a flyer to the ground; a guest's flight is predicted with no correction; the style survives a save round trip (it comes from the species).

## Comments

**2026-11-17 (developer, recorded by agent):** Decided in triage:
- **Who does what:** fly: cockatoo and sugar glider; run: kangaroo and Tasmanian devil; bicycle: wombat, koala, platypus and quokka.
- **Speed:** the same for every style.
- **Flying:** "they start with flying lower and the higher the rank the better the free flight gets."
- **Slow species ride:** a bicycle.

**2026-11-17 (agent):** Built on ticket 81's species.

- **Data:**
  - Each species has a travel style (`TravelStyle`: Fly, Run, Bicycle), in the species list in `GameContent.asset`.
  - Each rank has a flight ceiling (`flightCeiling` in `Progression.asset`): E 0, D 0, C 10 m, B 20 m, A 35 m, S 60 m.
  - The skimming height (3 m) and the climb speed (6 m/s) are `SimulationConfig.FlightSkimHeight` and `FlightClimbSpeed`.
- **Simulation** (`SimulationWorld.Travel.cs`):
  - **Every style** moves at `MountSpeed` (16 m/s).
  - **Runners and riders** use ticket 42's riding ground: no climbing.
  - **Flyers:**
    - **Ground:** a `FlyingGround` treats cliffs, water and steep faces as open air. Only the map's edge (`IMapBounds`, on the tiled and heightmap grounds) and ground not yet loaded stop them.
    - **Height:** they take off to 3 m. At or below a 3 m ceiling (E and D) they only skim; from C they climb and dive within their rank's ceiling, never below 3 m.
    - **Jump:** a flyer's jump key climbs instead of hopping.
    - **Landing:** a hit, an attack, a gate or going down drops them straight to the ground below. R lands them only over ground they can stand on.
    - **Bosses:** a flyer high enough passes over a boss's charge, as a high jump does.
- **Input and wire:** `PlayerCommand.Climb` (+1, 0 or -1, held). Hold Space to climb and C to dive. It travels in two spare bits of the command's mount byte. Snapshots carry the flight height.
- **Shown:**
  - A greybox bicycle (`TravelLooks`, still named "Mount") under wombats, koalas, platypuses and quokkas.
  - Runners lean forward and bound.
  - Flyers lie level with a gentle bob, at their height. The cockatoo's wings and the glider's membrane are already on the body from ticket 81.
  - `MountHud` speaks the species' word ("You can fly now. Press R."), says how high this rank lets you climb when you take off, and says why R does nothing over water.
- **Bots** need no change: each mounts its own way when its leader does, so a non-flyer follows a flying leader on the ground.
- **Tests:** `TravelTest` (11) covers:
  - each species' style, and 16 m/s for every style;
  - a runner and a rider stop at a cliff where a flyer crosses it, and water;
  - no flying off the map;
  - skimming at D, 10 m at C, 35 m at A, and diving back to 3 m;
  - jump climbs instead of hopping;
  - a hit drops a flyer to the ground;
  - landing only on standable ground;
  - a gate puts a flyer on foot;
  - the style survives a save;
  - a guest's flight is predicted with no correction.

  The two overworld PlayMode tests now ride as a wombat, so the bicycle is drawn as "Mount".

**Left for triage:**
- **Town walls:** whether flyers may cross them (ticket 89). They can for now: walls do not exist yet.
- **High flyers and melee:** an enemy's melee still reaches a high flyer (only a boss's charge passes under them). Should high flight be out of reach?
- **Landing:** a fall from height is instant, not an animated drop.
- **Gamepad:** no climb or dive buttons yet.
- **Draw distance:** the draw and streaming distance for high flyers has not been measured.

**Evidence:**
- **First run:** found one bug. Starting a gate stood a flyer up without clearing their flight height, so they hovered 3 m up in the gate. Fixed in `GetUp`, and on arrival.
- **After the fix:** **873 EditMode** tests pass, all 11 `TravelTest` tests among them. The **45 PlayMode** tests passed on the run before the fix, which touched only the simulation, and were not run again.

Not verified: nobody has flown, run or ridden in the editor yet.
