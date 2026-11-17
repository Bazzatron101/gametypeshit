# 0024: Hunters are Australian animals

**Status:** Accepted (2026-11-17)

## Context

Ticket 81 makes every hunter an upright animal-folk character. The developer wants a memorable hook that
also makes co-op players easy to tell apart, and ready-made low-poly animal art fits the flat-shaded style
(ADR 0002). Classes already give stats per level (ADR 0012), so a species must sit beside the class, not
replace it.

## Decision

- **Australian natives only.** Eight to start: kangaroo, Tasmanian devil, sugar glider, wombat, koala,
  cockatoo, platypus and quokka.
- **Chosen once, and permanent,** like the class and the name (ADR 0012, ADR 0019). The order when making a
  hunter: name and looks, then species, then class. A hunter saved before species (character format 7 or
  older) is asked for one once and keeps its class and progress.
- **One shared body and skeleton.** Species differ only in heads, ears, tails and proportions, so worn gear
  (ticket 34) and future animations fit every species. Body colour becomes fur or feather colour; the build
  stays.
- **A small stat bonus that grows by rank:** +2 to the species' stat at E-rank, and 1 more at each rank
  promotion. It adds to class bonuses and free points. It is worked out from the hunter's own rank (their
  level's band) every time, never written into the saved stats, so it grows by itself and a change to the
  numbers reaches every hunter.
- **One small perk each.** A nudge you notice, not a build-decider. Perks are a fixed set of kinds
  (`SpeciesPerk`), each with an amount and, for some, a threshold. A new species picks a kind and its
  numbers, so adding a species is data only. All numbers can be changed in the Inspector, in the game
  content's species list.
- **Perks run in the simulation,** like other combat rules: on the host, and predicted by clients from the
  same state. Snapshots carry each player's species and when they last fought (for the koala's healing).
- **No species is the only right pick for a class:** every class has at least two species whose stat it grows.

## Consequences

- Some perks reach for systems that do not exist yet. The platypus "senses hidden things": today that is
  hidden gates in the overworld; hidden enemies and traps should use the same reach when they are added.
  The sugar glider "glides when falling": jumps are the only falls today, so its way down is slower.
- Saves move to character format 8. The game refuses a species it does not know in a snapshot, as it does a
  class; in a save it is dropped and the hunter is asked again.
- The greybox species parts are made in code, so there is no art file to label "AI" yet. Real heads and
  tails replace them.
- Ticket 90 (travel by species) builds on the species: cockatoos and sugar gliders fly, kangaroos and Tasmanian devils
  run, the rest ride a bicycle, all at the mount's speed. A flyer skims at 3 m at first; each rank's `FlightCeiling`
  lets them climb higher (10 m at C up to 60 m at S). Flyers cross cliffs and water, never the map's edge.
