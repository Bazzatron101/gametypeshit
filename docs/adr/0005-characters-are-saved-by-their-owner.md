# 0005: A character is saved by its owner's game; the host takes it on trust

**Status:** Accepted (2026-11-13)

## Context

The spec says "Characters live in each player's own save, with Steam Cloud as an optional backup. Story
and map progress live in the host's world", and that guests keep the XP and loot they earn in a host's
gate (stories 68 to 70). Sessions are host-run among friends; anti-cheat and dedicated servers are out
of scope. Ticket 15 had to decide who holds a character, who writes it, and what happens on rejoin.

## Decision

- **A character is a small record** (`CharacterRecord`): level, XP, unspent points, stats, known skills, the
  bar, modifiers. Position, health and cooldowns are not in it; a character arrives fresh. Gear joins
  the record when items exist, under a new format version.
- **The owner's game saves it.** Each copy saves its own local player's character to its own disk:
  offline and on the host, from its world; on a guest, from the host's latest snapshot
  (`ClientSession.ConfirmedCharacter`), never from the guest's own prediction. The host never writes a
  guest's save and keeps no copy after the session.
- **A guest sends their character when joining** (in `Hello`), and the host brings it into the session
  as sent. Values outside the rules are brought inside them, so a damaged save cannot break a session,
  but the record is not checked for honesty.
- **A character the host cannot read is refused,** not replaced with a new one. Otherwise the guest's
  game would save the replacement over their real character.
- **On rejoin, the copy that is further along wins.** The host kept the character with what it earned
  there (ADR 0003), and normally that is the newer one. If the one the returning guest sends is further
  along (a higher level, or the same level with more XP: they played elsewhere or restored a backup in
  between), it replaces the session's copy, so the guest's next save cannot take them backwards. Two
  copies with the same level and XP count as the same, and the session's stands.
- **Saving:** automatically, within a second of progress (XP, a level, a stat point, a skill or its
  slot, a modifier), and at once on leaving a session, changing scene or quitting. Each save replaces
  the file in one step and keeps the previous one as `.bak`. A file whose contents cannot be understood
  is moved aside, never overwritten, and the backup is tried. A file that cannot be opened at all
  (locked by another program or a second copy of the game), or cannot be moved aside, is left exactly as
  it is and nothing is saved for the rest of that run: losing one session's progress is better than
  overwriting a save that is probably fine.
- **Steam Cloud is a backup the player controls.** Off by default (decided by the developer, while the
  game uses test app 480; the default is a constant to revisit with the game's own app ID).
  - When on, every save made while Steam is running is also copied to the cloud. Steam only starts in
    the co-op scene today, so saves made in other scenes are not copied until the next save there.
  - Neither side silently replaces the other. Restoring is an explicit, two-click action that shows the
    cloud hunter's level first; the save it replaces is kept as `.bak` and as a lasting
    `.before-restore` copy.
  - Switching backup on does not upload over a different hunter already in the cloud (a new PC): it
    stays off until the player restores, or chooses, with two clicks, to replace the cloud's copy.
  - On quit the save happens before Steam is shut down.
- **The host's world** (`WorldRecord`: cleared gates, later story and map progress) is a separate file in
  a separate folder, written only by the host's game. Nothing fills it yet; the gate runtime (17) will.
- **The test arena and bots never save.** The arena brings the character in but keeps nothing done there
  (decided by the developer); bots run with `-no-save`.
- **One character per player for now** (decided by the developer); slots arrive with the character
  creator (28).

## Consequences

- A guest can edit their save and bring an impossible character; a host can hand out anything. Accepted:
  friends-only sessions, no anti-cheat (spec, out of scope).
- If a guest's game crashes mid-session, they lose at most the last second of progress locally. The
  host still holds their character until the session ends, so rejoining gets it back.
- A guest who leaves and whose final save fails keeps an older character at home, while the session
  holds the newer one only until it ends.
- Skills are stored by id. A skill removed from the game is dropped from characters that had it, and
  skills added below a character's level are learned when it is next brought in.
- Saves are binary with a version byte. A save from a newer version of the game is refused and kept
  aside, so going back a version cannot destroy it.
- Two copies of the game on one PC share one save folder unless started with `-save-dir`.
