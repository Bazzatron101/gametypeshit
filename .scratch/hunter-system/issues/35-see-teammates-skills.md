# 35: See what teammates' skills are doing

**What to build:** Skills used by other players and bot teammates are easy to see and tell apart, so you can see that they are doing something (developer's note from the M3 review, ticket 21). Before this, every skill showed only as a quarter-second grey flash (`SkillEffectsView`).

**Blocked by:** None

**Status:** ready-for-human (see it in a run)

- [x] Each skill has a recognisable effect, not one grey flash for all
- [x] ~~It is clear whose skill it was (the skill's name over the player who used it)~~ Not wanted (developer, 2026-11-14): "you dont need to show skill name". The colour says which class used it.
- [ ] Projectiles, areas and cones from teammates read clearly in a busy six-player fight (judge by playing)

## Comments

**2026-11-14 (agent):** Built.
- **Colour by class** (`SkillLook`): Warrior red, Rogue purple, Archer green, Wizard blue, and skills with no class pale gold. Each skill is shifted slightly from its class colour so two skills of one class differ. Projectiles, area discs and cone slabs are all tinted; explosions are orange.
- **Longer flashes:** half a second instead of a quarter. A flash opens out quickly to full size, holds, then thins away.
- No names or text over players.
- Tests: `SkillLookTest` (3, EditMode); the PlayMode `GreyboxSkillsTest` and `GreyboxModifiersTest` still pass.
