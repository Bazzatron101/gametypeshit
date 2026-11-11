# 04: Dodge, enemy damage and player defeat

**What to build:** Enemies can hurt the player, the player can dodge with brief invulnerability frames, and the player has a defeat state.

**Blocked by:** 03: Light attack and one enemy

**Status:** ready-for-agent

- [ ] Enemy attacks reduce player health and emit damage events
- [ ] Dodge grants a short invulnerability window during which damage is ignored
- [ ] Player defeat is a defined state, ready to be extended by the death rules later
- [ ] Headless tests cover dodge-negates-damage inside the window and not outside it
