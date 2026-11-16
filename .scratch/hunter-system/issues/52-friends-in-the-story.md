# 52: The player's friends as story characters

**What to build:** The bot teammates (ticket 33) become the player's friends in the story: named characters who set out with the player at the start (ticket 20's arrival scene), take part in the story, and talk with the player. Over the story, their conversations reveal the main character's past and what they are looking for while adventuring: the missing older sibling (ticket 16).

**Blocked by:** 33: Bot teammates (done), 20: Mission board and text scenes

**Status:** needs-triage

- [ ] Each friend has a name and a personality the writing can lean on
- [ ] Friends can speak in text scenes (as a speaker, like the guild NPC)
- [ ] Conversations between the player and their friends reveal the main character's past and the search for the sibling, spread across the story rather than told all at once
- [ ] The friends show their names in the world (ticket 33 notes that the world view shows no names)
- [ ] Works in co-op: the story still makes sense when real friends play alongside, or instead of, the bots

## Notes

From the developer (2026-11-16, ticket 20): "you start your adventure with your friends(bots) who will play part in the story ... some dialog between you and your friends will show the past of the main character and what they are looking for whilst adventuring".

**To settle in triage:**
- **How many friends are story characters?** Ticket 33 has five bots (one per class plus one extra). Are all five named characters, or only some?
- **When bots are turned off or replaced by real players** (the co-op toggle, or guests joining a full session), do the friends still appear in scenes? The spec wants a fixed cast that "works for any number of players" (story 61). One option: the friends always speak in scenes even when they are not fighting with you.
- **Whose past?** In co-op every player has their own character, but the story is the host's world (ADR 0009). Is the main character the host's character, or is the past shared by everyone?
- **Where the dialog happens:** only in text scenes in the hub, or also short lines during runs (for example on entering or clearing a gate)?
- **Do friends' class and role stay tied to their bot**, so a friend who is the tank in the story is also the tank in fights?
- **Words:** written by the developer; blank "write me" markers until then (ADR 0009).
