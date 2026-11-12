# 15: Character save and load

**What to build:** A character persists in its own save with optional Steam Cloud backup, and a guest brings their own character into a host's session. Story and map progress belong to the host's world.

**Blocked by:** 06: XP, levels, six stats and the level-up UI, 13: Two-player Steam co-op

**Status:** ready-for-agent

- [ ] Character stats, level, skills, and gear save and load correctly
- [x] Steam Cloud backup is optional and restorable
- [x] A guest joins a host's session with their own character and keeps XP and progress earned there
- [x] Host-world progress is stored separately from guest characters

## Comments

**2026-11-13 (agent):** Built; decisions are in ADR 0005.

Developer's choices:
- The test arena never saves.
- One character for now.
- Steam Cloud is off by default while on test app 480.

What was built:
- **`CharacterRecord`:** level, XP, points, stats, skills, bar and modifiers, with a versioned byte format.
- **Save files:** `CharacterSaves` swaps the file in one step, keeps the previous save as `.bak`, and moves unreadable files aside.
- **Autosave:** within a second of progress, and on leaving or quitting.
- **Guests:** a guest sends their character on joining. Their own game saves what they earn.
- **Host world:** `WorldSaves` keeps it in a separate folder.
- **Menu:** the co-op menu shows your hunter, a Steam Cloud toggle and a two-click Restore.

Evidence:
- Headless tests cover the record, the files, the autosave rule and the session.
- Scene tests show that Greybox progress persists and that the arena keeps nothing.
- The two-copy test shows the guest earning a level in the host's session and finding it in their own save. The host's world file is on the host only.

Boxes left open:
- **Box 1 (gear):** stats, level and skills save and load. There is no gear in the game yet; it joins the record when items exist (M4).
- **Box 2 (Steam Cloud):** backup and restore are built and tested against a stand-in cloud. They have not been tried against real Steam. The developer should switch the toggle on in the co-op menu and try Restore.

**2026-11-13 (agent), after review:**
- **Fixed:**
  - **Rejoin:** the copy of the character that is further along now wins. Before, the session's copy always won, which could take a guest backwards if they had progressed elsewhere.
  - **Guest saves** come from the host's latest snapshot, not the guest's prediction.
  - **A save that cannot be opened** (locked) is left alone and saving is off for that run. Before, it was treated as corrupt and later overwritten. The same applies when an unreadable save cannot be moved aside.
  - **Steam Cloud:** switching backup on no longer overwrites a different hunter already in the cloud. Restore shows the cloud hunter's level, disarms after a few seconds, and keeps a lasting `.before-restore` copy.
  - **Quit:** the final save happens before Steam shuts down. The character and world saves are tried separately, and no save error can escape a quit.
  - **Settings file:** read and write failures are handled.
  - **Restore** is refused while a character is in play.
  - **The two-copy test** reads the save without touching it.
- **Left as is (judgement calls):**
  - `CharacterSaves` owns the file, the cloud and the setting together.
  - The codecs repeat their header and count checks.
  - PlayMode tests each call `TestSaves.UseFreshFolder()`. A test that forgets would use the real save folder.
  - `WorldRecord` is saved but nothing fills it until gates exist (17).
- **Known limits:**
  - Saves made outside the co-op scene are not copied to Steam Cloud, because Steam only starts there.
  - Two characters with the same level and XP but different stat choices count as the same on rejoin.

**2026-11-13 (developer test, recorded by agent):** Steam Cloud tried against real Steam (test app 480): the cloud save worked, and the level and stat points came back correctly. Box 2 is ticked on that report. Box 1 stays open only for gear, which does not exist yet.

