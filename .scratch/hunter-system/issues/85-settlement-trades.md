# 85: Each place has a trade from where it stands

**What to build:** Give every settlement a trade that suits its surroundings, with buildings and props that show it. Split from ticket 82. The developer: "a town for lumber or mining or fishing and having buildings that match their locations".

**Blocked by:** 84: More building kinds (parent: 82)

**Status:** ready-for-agent

- [ ] **The trade comes from the place:** by the forest, lumber; by the mountains or steep ground, mining; by the sea, a river or a lake, fishing; on open flat land, farming (ticket 86). Worked out from the ground and the nature at bake time, and it can be set by hand per place in `ContinentLayout`
- [ ] **Lumber:** a sawmill, log piles, a woodcutter's yard, felled stumps at the forest edge
- [ ] **Mining:** a mine entrance in the nearest rock face, ore carts on a track, a smelter or forge, spoil heaps
- [ ] **Fishing:** a jetty or docks out over the water, boats, fish-drying racks, a net shed. Any place by the water gets docks, whatever its trade
- [ ] A place's trade is named on the map or the board (for example "Pinewatch, a lumber town")
- [ ] Tests: each trade is chosen where it should be on the continent, and its buildings stand where they belong (docks over water and reachable from land, a mine at a rock face)
