# 50: Move off Steam test app 480 to the game's own App ID

**What to build:** The game uses Valve's test app 480 (Spacewar) for everything Steam does: co-op hosting and joining, invites and Steam Cloud. Switch it to the game's own App ID once the developer has one. **The purpose is external testing:** people outside the developer's own PCs can get a build through Steam, install it, and play co-op together, without the game posing as Spacewar. This is not the release; a public store page can come later.

**Blocked by:** None (the App ID itself is the developer's step: see below)

**Status:** ready-for-human (get the App ID first)

**The developer's step, before any code:** join Steamworks (partner.steamgames.com), pay the Steam Direct fee for this game, and note the App ID it is given. Then set up Steam Cloud for it in Steamworks: the space per user and the file it may store (the character save). Testers get the game through Steam's Playtest feature only: they request access and the developer lets them in (the developer's choice). Set up the Playtest in Steamworks; it is its own app with its own App ID, linked to the main one. The agent cannot do this.

- [ ] The Playtest's App ID (the one testers install) is set in one place, and both the game and the build use it: `CoopLauncher.SteamAppId` and the `steam_appid.txt` that `CoopPlayerBuild` writes
- [ ] Hosting, joining by SteamID, and invites work on the new App ID; an invite from a closed game launches this game, not Spacewar (ADR 0004)
- [ ] Steam Cloud backup and restore work on the new App ID, and the "off by default" choice is revisited (ADR 0005)
- [ ] A build can be uploaded to the Playtest (SteamPipe), and a tester outside the developer's PCs requests access, is let in, installs it from Steam and plays co-op with the developer
- [ ] Docs no longer describe 480 as current: ADRs 0004 and 0005 get a note, and tickets 13 and 15 point here

## Comments

**2026-11-16 (agent):** Opened at the developer's request. Where 480 is used today:
- `Assets/Scripts/Online/CoopLauncher.cs`: `SteamAppId = 480`, passed to `SteamClient.Init`.
- `Assets/Editor/CoopPlayerBuild.cs`: writes `steam_appid.txt` containing `480` next to the built player.
- ADR 0004: invites from a closed game launch Spacewar on 480, so they only work from inside a running game.
- ADR 0005: Steam Cloud is off by default while on 480; revisit with the game's own ID.
- Ticket 13 lists "move off test app 480 before release".

Notes for whoever builds it:
- Keep 480 usable for local testing without a key, for example a setting or command-line override, until the new ID is set up in Steamworks for everyone testing.
- Cloud copies made under 480 are **not** carried over (the developer's choice, 2026-11-16): they were tests. Local saves are not tied to the App ID and keep working.
- A built game started outside Steam needs `steam_appid.txt` with the new ID; a release build from Steam does not, and it should not ship in the uploaded build.

**2026-11-16 (developer, recorded by agent):** This is for external testing, not the release: a way for other people to get the game through Steam and test it. Steam Cloud copies from 480 do not need carrying over.

**2026-11-16 (developer, recorded by agent):** Testers get access both ways: Steam keys the developer hands out, and Steam Playtest. Note for the build: a Playtest is its own app in Steamworks with its own App ID, linked to the main one. Check how co-op works between the two before relying on it: whether a key tester (main App ID) and a Playtest tester can join each other, or whether everyone in one session has to be on the same one.

**2026-11-16 (developer, recorded by agent):** Changed: **Playtest only, no keys.** Testers request access. So the question above goes away: everyone, the developer included, plays the Playtest app, and the game runs on the Playtest's App ID. The main App ID is for the store page and the release later.
