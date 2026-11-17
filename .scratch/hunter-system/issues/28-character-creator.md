# 28: Character creator

**What to build:** A simple character creator for the player's hunter, with the chosen name displayed above the character in co-op.

**Blocked by:** 15: Character save and load

**Status:** done

- [x] A simple creator offers a few appearance options and a name
- [x] The choice is saved with the character
- [x] Other players see the name above the character

## Comments

**2026-11-15 (developer, recorded by agent):** Decided:
- **Looks:** a body colour (eight to pick from) and a build (slim, average, broad).
- **When:** its own screen, before the class picker.
- **Changes later:** looks can be changed in the hub; the name is permanent.
- **Nameplates:** over other players and bot teammates, not over your own hunter.

**2026-11-15 (agent):** Built; decisions are in ADR 0019.
- **The creator** (`CharacterCreator`): a name field, colour swatches and build buttons. It opens for a hunter with no name, before the class picker, and play and going online wait for it. A hunter from before names is asked once and keeps everything else.
- **Restyling:** press L in the hub. It goes to the simulation as a `ChangeLook` command, works only in the hub, and the name stays.
- **Saved and shared:** character format 5 adds the name and look; older saves load and ask for a name. Snapshots carry everyone's name and look. A look change autosaves.
- **On screen:** the body takes the chosen colour and build (`CharacterLooks`). `Nameplates` show every other hunter's and bot's name, fading with distance.
- **Bot teammates** are named from their records, each in a different colour; the tank is broad, the rogue slim.
- **Automated runs** (`-coop`) take `-name NAME`, else "Bot SEED" for `-bot` runs, else "Hunter".

Evidence:
- EditMode `CharacterCreatorTest` covers names, looks, saves (and format 4 loading), snapshots, the hub-only command, the wire, the event codec, the autosave, the command buffer, nameplates and bot teammates. The full EditMode suite passes (597/597, on top of the overworld commit for ticket 38).
- PlayMode `CharacterCreatorSceneTest`: in the Greybox scene, a new hunter is named and dressed, then picks a class, and it is all there after a reload. A hunter from before names is asked once and keeps its level and class. In the Coop hub, looks change and two bot teammates' names show while yours does not. The full PlayMode suite passes (31/31).

Not checked by playing: how the creator, swatches and nameplates look on screen, and nameplates between two real players over Steam.

**2026-11-17 (developer playtest, recorded by agent):** "It is cute and I like it with the different colours and body sizes." A friend seeing your name is covered by the two-PC test (ticket 13).
