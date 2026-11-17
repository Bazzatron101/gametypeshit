# 53: Side quests that help the people of the world

**What to build:** Optional side quests where the player helps the people of the world. The main missions mostly end well, but a player who skips the side quests has only helped themselves, not the people, and the world shows the difference.

**Blocked by:** 20: Mission board and text scenes

**Status:** ready-for-agent

- [ ] Side quests exist apart from the main missions and can be skipped
- [ ] Doing a side quest helps someone in the world, and the world shows it
- [ ] A player who skipped them sees that they only helped themselves
- [ ] Progress belongs to the host's world and is saved with it, like the story's scenes (ADR 0009)
- [ ] The tone fits the game's: some dark moments, but most end with the player helping people

## Notes

From the developer (2026-11-16, ticket 20): "the tone of the game will have some dark moments but most of the missions will have a good ending to them with you doing something to help people but if someone skips the side quests they will have only helped themselves and not the people in the world".

**To settle in triage:**
- **What a side quest is:** a gate run with a person to help (for example clear a leak threatening a village), a hub errand, or both? Where does the player find them: the gate board, NPCs in towns, or both?
- **How the world shows the difference:** a different line in a later scene, changed NPCs or towns, a different ending, or a reward? This is the core of the idea and decides how big this ticket is.
- **When the player learns they "only helped themselves":** at the end of the main campaign (the demon king), along the way, or never said outright?
- **The first one:** which side quest comes first, and at what rank?
- **Co-op:** a guest who does side quests in a host's world helps that world, not their own (ADR 0009). Is that right?
- **Words:** written by the developer; blank "write me" markers until then (ADR 0009).

## Comments

**2026-11-17 (developer, recorded by agent):** Decided in triage:
- **A side quest is a gate run with someone to help, a hub errand, or both.** Players find them on the gate board and from people in towns.
- **The world shows the difference in all four ways:**
  - later story scenes change their lines;
  - the people helped, and their towns, change;
  - the campaign's ending changes with how much the player helped;
  - each side quest gives a reward.
- **It is never said outright.** The game never tells the player they "only helped themselves"; the world shows it.
- **The first side quest is E-rank:** a leak threatening a Greenmarch village, right after the Goblin Warren training mission.
- **Co-op:** a guest's side quests help the host's world only (ADR 0009).
- **Words:** blank "write me" markers for the developer (ADR 0009).

Note for whoever builds it: this is big. Consider splitting it: side-quest progress, the first quest and its reward first; changing scene lines and towns next; the ending last, once the demon king's story exists.

**2026-11-17 (developer, recorded by agent):** **Some side missions are solo.** A side mission can be marked solo: no bot teammates and no other players. Only the host goes in; guests in a co-op session stay behind until it is done. Which missions are solo is set on each mission (data, not code). The other side missions follow the normal party rules.
