# 80: Lighting and post-processing pass

**What to build:** Make today's greybox look intentional without new models. Do it with lighting, post-processing and material tuning only, so every scene (hub, overworld, gates, the raid) has a mood instead of flat default lighting. This is the free first step before buying or making real art. It stays inside the flat-shaded low-poly style (ADR 0002) on URP. The overall mood is **warm and moody**.

**Blocked by:** none

**Status:** ready-for-agent

- [ ] One shared post-processing look on a global Volume: tonemapping, warm colour grading, gentle bloom, light vignette. Motion blur, film grain, lens distortion and chromatic aberration stay off.
- [ ] Ambient occlusion (URP SSAO renderer feature) on `PC_Renderer`, so the bases of shapes sit on the ground
- [ ] Shadow tuning in `PC_RPAsset`: distance, cascades and soft shadows sized to the camera, with no shimmer when the camera moves
- [ ] Anti-aliasing on (MSAA or SMAA); edges are clean on the hard facets
- [ ] Hub and overworld: golden low sun, soft warm ambient, light distance fog
- [ ] A gate names a lighting mood asset (sun, ambient, fog, Volume overrides) as data; changing it is one field, with no scene editing. Ticket 77's theme kits can reuse these moods later.
- [ ] A mood for each gate now, all within the warm and moody look: Goblin Warren, Thornwood Hollow, the class trials (one shared mood is fine) and the Goblin King raid. Gates feel darker and closer than the hub and overworld, and fights stay easy to read.
- [ ] Materials tuned so the flat colours read well under the new lighting: smoothness and metallic set on purpose, not left at default
- [ ] Glow where it helps gameplay: skill effects, Legendary gear (`LegendaryGlow`) and gate portals use emission so bloom picks them up
- [ ] A Graphics Quality option (Low / Medium / High) in the settings menu, saved between sessions:
  - High: everything above
  - Medium: SSAO off, shorter and lower-resolution shadows
  - Low: also bloom off and lower render scale
  - The default is High on PC and Low on mobile, and changing it takes effect without a restart
- [ ] `Mobile_RPAsset` still runs
- [ ] No large frame-time cost on High: compare before and after in the arena with a full party and many enemies, and record the numbers in a comment
- [ ] Before/after screenshots of the hub, the overworld, each gate and the raid in a comment

## Notes

Context: `Assets/Settings/` holds `PC_RPAsset` (HDR on, MSAA off, 2048 shadow map, 50 m shadow distance), `DefaultVolumeProfile` and the template's `SampleSceneProfile`. Visuals are built from primitives in code (`GreyboxVisuals`, `GearLooks`, `PlayerView` and others), so material changes may land in code as well as in the material assets under `Assets/Art/`.

## Comments

**2026-11-17 (developer, recorded by agent):** Decided in triage:
- **Mood:** warm and moody.
- **Per-gate lighting:** each gate gets its own lighting mood now, not after the theme kits.
- **Quality setting:** add a Low / Medium / High graphics option to the settings menu.
