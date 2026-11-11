# 08: Tag-based modifiers

**What to build:** Skills and items can carry tags (for example fire, projectile, melee, summon), and modifiers act on tags. One demoable synergy changes how a skill behaves, proving the system behind broken builds.

**Blocked by:** 07: Data-driven skills with the six-slot bar

**Status:** ready-for-agent

- [x] A modifier definition keyed to a tag changes the behavior of every skill with that tag
- [x] At least one concrete synergy is demonstrable in the game
- [x] Hand-authored per-skill modifiers can coexist with tag-based ones
- [x] Headless tests assert the modified outcome and that untagged skills are unaffected

## Comments

**2026-11-12 (agent):** Implemented.
- Data: `ModifierDefinition` targets exactly one of a tag (`AppliesToTag`) or one skill (`AppliesToSkillId`, the hand-authored kind). Its effects are damage % and cooldown % (both add; the cooldown floor is 10%), range and radius adds, extra projectiles (an even fan), and explosions (every direct hit bursts onto nearby enemies). Authored as `ModifierDefinitionAsset`s in `Assets/Data/Modifiers/`.
- Resolution: each skill use builds a per-player `ResolvedSkill` from the shared definition plus that player's matching modifiers. Shared data is never changed. `SkillUsed` reports the resolved range and radius.
- Bursts: per skill use, an enemy takes at most one direct hit and one burst, and no burst if the use hit it directly. Bolts from one press count as one use. Bursts use the full hit damage, even on a killing blow, and never chain. `DamageDealt.IsSecondary` marks burst damage.
- Tags: `SkillTags.Standard` / `SimulationConfig.KnownTags` (melee, physical, area, fire, projectile, magic, summon). Unknown tags on skills or modifiers are rejected as typos.
- Granting: `GrantModifier` / `RevokeModifier` validate immediately but apply at the start of the next tick, with `ModifierGranted` / `ModifierRevoked` events, so changes are ordered against commands and reproducible.
- Demo (Greybox): F5 Searing (fire +50%), F6 Split Shot (projectiles fire 3), F7 Ember Core (fire hits burst for 50%), F8 Deep Rend (hand-authored: Rending Slash +50% damage, -25% cooldown, +1 m reach). The synergy: F6 + F7 makes Fire Bolt fire three bolts that each explode. The HUD lists the toggles and which are on.
- Tests: `SimulationModifiersTest` (tag modifiers change every tagged skill, untagged skills are unaffected, spread, bursts, no chaining, no double-dipping, full-hit bursts, synergy, hand-authored plus tag stacking, shared data untouched, queued grants and events, validation); `ContentValidationTest` (every skill, modifier and enemy asset in the project loads and validates); `GreyboxModifiersTest` (PlayMode: Split Shot through the toggles puts three bolts on screen). A deliberate removal of the double-dip rule was caught.

Open notes for later tickets:
- **Not checked by a human:** F5 to F8 in the live game, the explosion flashes, and the HUD modifiers panel.
- **23 (gear tag synergies) / 22 (loot):** gear should call `GrantModifier`/`RevokeModifier` on equip and unequip; remove `DebugModifierToggles` then. Light and heavy attacks still carry no tags, so "melee" modifiers do not reach them. Give `AttackDefinition` tags when gear needs it (story 29: skills and items interact through tags).
- **Netcode (12):** grants are queued on the host's world. With prediction, they need to travel as authoritative events or commands; the queued-at-tick model is ready for that.
- **Small:** with several bolts, a burst from an early bolt can land on an enemy that a later bolt then hits directly (time order). Damage rounds twice (modifier % then stat %, both halves up); this is consistent, but worth knowing when balancing.
