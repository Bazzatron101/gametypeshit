# 08: Tag-based modifiers

**What to build:** Skills and items can carry tags (for example fire, projectile, melee, summon), and modifiers act on tags. One demoable synergy changes how a skill behaves, proving the system behind broken builds.

**Blocked by:** 07: Data-driven skills with the six-slot bar

**Status:** ready-for-agent

- [ ] A modifier definition keyed to a tag changes the behavior of every skill with that tag
- [ ] At least one concrete synergy is demonstrable in the game
- [ ] Hand-authored per-skill modifiers can coexist with tag-based ones
- [ ] Headless tests assert the modified outcome and that untagged skills are unaffected
