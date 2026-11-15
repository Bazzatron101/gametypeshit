# 45: Build the full overworld terrain

**What to build:** The whole ~10.5 × 7.5 km map from `docs/maps/overworld-reference.webp`, using the pipeline from ticket 39: generated ground for the six regions, with the guild town, the other towns and castles, the ~25 gates, landmarks, waystones and the road placed by hand (ticket 36's region table).

**Blocked by:** 39: Terrain pipeline and a 1 km test patch, 40: Load the overworld in tiles around players

**Status:** needs-triage

- [ ] Every region's ground generated and baked into tiles, with the sea, the mountain ranges, passes and climbable faces where ticket 36 puts them
- [ ] Every settlement and gate from ticket 36 placed as a marker; the nine missing gate spots once the developer marks them on the map
- [ ] The road joins the guild town to the east coast; a crossing measures about 40 minutes walking and about 13 mounted (ticket 36), checked in a build
- [ ] Origin at the map's centre (ADR 0015)
- [ ] Hours spent recorded against the estimate from ticket 39

## Notes

- If playtests find the map too close, grow it by scaling the terrain (keep the drawing), or wind the roads more, which is cheaper (ticket 36).
