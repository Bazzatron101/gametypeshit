# 34: Gear shows on the character

**What to build:** Equipped gear is drawn on the character (a weapon in hand and visible armour pieces), so hunters and bot teammates look different from each other and change as they gear up (developer's note from the M3 review, ticket 21).

**Blocked by:** 23: Gear and tag synergies

**Status:** ready-for-human

- [x] Each equipped item has a look, drawn on the character in every scene
- [x] Other players and bot teammates are seen with their own gear (it travels in snapshots)
- [x] Bot teammates equip gear that suits their role

## Comments

**2026-11-16 (developer, recorded by agent):** Triaged: build now ("this still feels very basic").

**2026-11-16 (agent):** Built. Worn gear is now drawn on the character.
- **Looks:** each gear asset has a new `look` field (a prefab). `GearLooks` places it on the body at its slot's spot: weapon in the right hand, head on top, body on the chest, gloves on both hands, boots on both feet, trinket on the chest (the quiver hangs on the back). Looks are children of the body, so they follow the build, the dodge squash, lying down when defeated, and riding. A piece with no look gets a plain stand-in shape coloured from its id, so nothing worn is ever invisible.
- **Drawing:** `PlayerView` redraws a slot only when what is worn there changes (by id). Every scene uses `PlayerView` (greybox, arena, overworld, continent, and co-op through `OnlineViews`), so gear shows everywhere. Guests draw from host snapshots, which already carried worn gear.
- **Bot teammates** already dressed for their role (ADR 0017, `CompanionRoles.GearToWear`); they now show it too. Different classes start with different weapons, so a party looks different at a glance.
- **Placeholder art (labelled "AI"):** 12 prefabs in `Assets/Art/Gear` (one per piece) and 10 flat materials in `Assets/Art/Gear/Materials`, built from primitives by `Hunter System > Gear > Make Placeholder Gear Looks` (`GearLookSetup`). Running it again rebuilds only its own placeholders; a look you set by hand is never overwritten. To replace one, put your model's prefab in the gear asset's `look` field, built around the slot's spot (the hand for weapons, and so on; see `GearLooks.Anchor`).
- Tests: `GearOnCharacterTest` (EditMode, 5: every piece has a look with no colliders, weapons look different, the look catalog, each slot has its own place, and host, guest and a bot teammate each see everyone's own gear over the simulated network). `GearOnCharacterSceneTest` (PlayMode, 2: wearing, swapping and taking off in the greybox; the host and three bot teammates in the co-op scene each drawn with what they wear). EditMode: 682 passed; PlayMode: 40 passed.
- **To check:** in the arena, press I and wear pieces one at a time, then the whole Cinder set. Check that each piece sits right on slim, average and broad builds, and while dodging, defeated and riding. Host with bot teammates and see them in their own gear. The shapes are rough stand-ins: find them with `l:AI` in the Project window. I only looked at a still render, not the game.
