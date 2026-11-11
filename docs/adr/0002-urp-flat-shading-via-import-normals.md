# 0002: URP, with flat shading done at model import

**Status:** Accepted (2026-11-11)

## Context

The art direction is flat-shaded low-poly: every face is lit as one flat colour, with no
smooth gradients across edges. There are three common ways to get that look in Unity:

1. A custom shader that derives face normals from screen-space derivatives.
2. Authoring split normals in the 3D tool for every model.
3. Recalculating normals at import with a 0 degree smoothing angle, which makes every edge hard.

## Decision

- **Universal Render Pipeline (URP)**, using the template's `PC_RPAsset` and `Mobile_RPAsset`.
  URP is cheap, which helps on mid-range PCs and with up to 10 players and many enemies on screen.
  It works with the stock Lit and Simple Lit shaders.
- **Flat shading through import settings (option 3).** `Assets/Editor/LowPolyImportRules.cs`
  forces `Normals: Calculate` with a 0 degree smoothing angle on every model under
  `Assets/Art/Models/`. Stock URP shaders then render hard facets, with no custom shader to maintain.
- Models outside that folder (third-party assets, for example) keep their own import settings.

## Consequences

- Artists and asset sources don't need to author normals themselves. Dropping a model into
  `Assets/Art/Models/` is enough.
- Hard edges split vertices, so meshes carry more vertices. That is negligible at low-poly counts.
- A model that should be smooth must live outside `Assets/Art/Models/`.
