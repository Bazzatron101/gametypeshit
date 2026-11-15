# 39: Terrain pipeline and a 1 km test patch

**What to build:** The way overworld terrain gets made, proved on one tile-split patch of about 1 km² before the whole map is built. Most terrain is generated, with hand-placed gates, towns, landmarks and the road on top (ADR 0015, "content cost is the main risk"). The pipeline bakes the simulation's ground data (ticket 37) from the same terrain, so the art and the rules cannot drift.

**Blocked by:** 37: Give the simulation real ground

**Status:** needs-triage

- [ ] Terrain is split into square tiles (size a setting; about 250 to 500 m to start) in Unity Terrain, URP flat shading per ADR 0002
- [ ] Generated height from a source the developer can paint or trace from `docs/maps/overworld-reference.webp`, with hand edits kept when it is regenerated
- [ ] An editor step bakes each tile's heightmap and walkability for the simulation; steepness past a configured slope is unwalkable, water is unwalkable, painted climbable faces are marked
- [ ] Hand-placed points (gates, town edges, the road, landmarks) are authored as markers the simulation can read, not only as scene objects
- [ ] A 1 km² test patch with a hill, a steep ridge with a pass, a climbable face, a stretch of shore, a road and two gate markers, walkable in a build
- [ ] Hours spent on the patch are recorded, to estimate the full map's cost (ticket 45)

## Notes

- Far tiles need lower-detail versions; the patch should show whether Unity Terrain's own detail settings are enough.
