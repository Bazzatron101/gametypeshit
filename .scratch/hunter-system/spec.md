# Spec: Hunter System (working title)

An original power-fantasy 3D action RPG in the manhwa tradition, built solo in Unity, playable alone or with up to 10 friends.

> Status: synthesized from a design interview. Items marked **[assumption]** were proposed by the interviewer and not explicitly confirmed. Items marked **[parked]** are deliberately undecided, each with a decide-by milestone.

---

## Problem Statement

A solo developer with a programming background and about 5 hours a week wants to build the game they themselves want to play: a power-fantasy action RPG in the spirit of Solo Leveling and similar manhwas, where a weak hunter becomes overwhelmingly strong through a visible, earned climb. The player also wants to play it with friends, and finds games more enjoyable in company.

The difficulty is that the wish list is studio-scale (3D action combat, co-op for up to 10 players, long dungeons, a heavy story, a deep build system), while the available capacity is roughly 250 hours a year with no deadline. Without deliberate sequencing, a project like this stalls in its second year with nothing playable. The design has to protect the core power fantasy while ordering the work so that the riskiest parts (combat feel, co-op networking, content throughput) are proven early and cheaply.

From the player's perspective, the problem is: existing power-fantasy games either are single-protagonist stories that don't support friends, or are co-op games that flatten everyone's power. They want to feel overwhelmingly strong, and to share that with friends without either side being nerfed.

---

## Solution

A premium PC game with a finite main campaign and an endgame built around deliberately broken builds.

- The player is a hunter who starts at E-rank and climbs to A-rank in the main game, with an S-rank end story and a possible SS-rank DLC. A "System" interface (levels, stats, skill unlocks, missions, promotions) makes the growth visible and celebrated.
- From a small hub, the player picks gates (dungeons) from a menu. Gates are long, checkpointed, and built from a modular room kit, ending in handcrafted bosses.
- Combat is third-person hack-and-slash with six cooldown skills, a dodge, and soft lock-on, played keyboard and mouse first.
- Power is never scaled down. Over-leveled players can trivialize low gates, and that is a feature, so coasting versus challenge is the player's choice.
- Up to 10 players can join a host's world through Steam invites. Each player brings their own character and progression. Guests support the host just by being there, while story and map progress belong to the host's world. Enemy difficulty scales in tiers by party size, and 10-player raid gates have dedicated mechanics.
- The project is delivered through five milestones, each proving one risk, with a written kill-switch before any money is spent.

---

## User Stories

### Core fantasy and progression

1. As a player, I want to begin as a weak E-rank hunter, so that the climb to power feels earned.
2. As a player, I want to see a level-up clearly announced by the System interface, so that every gain is celebrated.
3. As a player, I want a visible stat screen with six stats (Strength, Agility, Vitality, Intelligence, Sense, Luck), so that I understand what makes my hunter strong.
4. As a player, I want to spend a few free stat points each level on top of class bonuses, so that my build is my own choice but I cannot easily ruin the character.
5. As a player, I want a level cap with ranks tied to level bands, so that the climb has a clear structure.
6. As a player, I want the main campaign to end at A-rank, so that the core story has a satisfying finish.
7. As a player, I want an additional S-rank end story, so that completing the main campaign leaves something to reach for.
8. As a player, I want the level cap to be a configurable value, so that the developer can extend it for S-rank and future DLC without rewriting systems.
9. As a player, I want rank promotions to be authored story events, so that moving up a rank feels like a milestone and not a number change.
10. As a player, I want to reach the main-game cap after roughly 30 hours or more, so that the game has real length.
11. As a player, I want to return to low-rank gates and overwhelm them, so that I feel how far I have come.
12. As a player, I want optional "ascended" versions of old gates, so that old content still offers a challenge when I want one.
13. As a player, I want no power scaling on my character, so that my strength is never taken away to keep content challenging.

### Classes

14. As a player, I want to play classless through E-rank, so that I experience the early game without commitment.
15. As a player, I want to choose from three base classes at D-rank by level alone, so that I get an early build choice without a gating quest.
16. As a player, I want each base class to branch into two specialties at B-rank through an authored quest, so that the choice feels meaningful.
17. As a player, I want a hidden class unlocked at the end of the S-rank story, so that finishing the game rewards me with something new.
18. As a player, I want to play the hidden class on a fresh character slot starting from E-rank, so that I can climb again with a new identity.
19. As a player, I want all classes to share one base animation set while differing in skills and weapon feel, so that class variety does not multiply animation work.
20. As a developer, I want a class design that I can start with the recommended tree and extend later, so that the first release stays within scope.

### Combat

21. As a player, I want a free camera with soft lock-on, so that I can fight groups while still targeting precisely.
22. As a player, I want light and heavy attacks on the mouse buttons, so that basic combat is immediate.
23. As a player, I want six skill slots on keys one through six, so that my build is expressed in my hotbar.
24. As a player, I want skills governed only by cooldowns, with no mana or stamina, so that using skills feels easy and rewarding.
25. As a player, I want a dodge with brief invulnerability frames, so that timing is rewarded.
26. As a player, I want most encounters to feature a small number of smart enemies with elites and bosses, so that fights are readable even with ten players on screen.
27. As a player, I want occasional scripted swarm moments, so that I get the feeling of mowing through hordes.
28. As a player, I want new skills and bigger skill effects as I level, so that my power increase is felt in combat and not only in numbers.
29. As a player, I want skills and items to interact through shared tags, so that combinations produce surprising results.
30. As a player, I want signature skills to have hand-authored modifiers, so that the best skills have memorable upgrades.
31. As a player, I want keyboard and mouse to be the primary input scheme, so that the game is tuned for how I play.

### Loot and builds

32. As a player, I want each gate to have a fixed loot pool, so that I know what I am hunting.
33. As a player, I want gates to always grant a reward, with the specific item drawn at random from the gate's pool, so that every clear feels worthwhile but the chase stays alive.
34. As a player, I want not to receive duplicates until I own the whole pool, so that bad luck is limited.
35. As a player, I want duplicates, once the pool is complete, to be sellable for currency, so that no drop is wasted.
36. As a player, I want personal loot, so that I never compete with teammates for drops.
37. As a player, I want item synergies and skill modifiers that can combine into absurdly powerful builds at the endgame, so that late-game power feels broken in the way I enjoy.
38. As a player, I want gear and skills to be the post-cap progression, so that the endgame has a goal after the level cap.
39. As a developer, I want loot pools, items, skills, and modifiers defined as data, so that I can add and rebalance content without code changes.

### Gates and the world

40. As a player, I want to select gates from the hub menu, so that I do not spend time on travel I do not enjoy.
41. As a player, I want gates to run 30 minutes or longer with checkpoints, so that a clear feels like an accomplishment.
42. As a player, I want story gates to have fixed layouts, so that authored story beats and bosses land as designed.
43. As a player, I want repeatable farming gates to be procedurally shuffled later, so that farming stays fresh.
44. As a player, I want each gate to end in a handcrafted boss, so that clears have a climax.
45. As a player, I want the world map to open up as my rank and the story progress, so that the world grows as I do.
46. As a player, I want to start in a small hub and see the world expand over time, so that I feel the scale of my growth.
47. As a developer, I want gates assembled from a reusable modular room kit, so that I can build 40 to 60 gates within my available hours.

### Hub

48. As a player, I want a small hub showing where gates are, so that I know where to go next.
49. As a player, I want a mission board in the hub that provides intel about gates, so that I can prepare.
50. As a player, I want storage in the hub, so that I can manage items between runs.
51. As a player, I want a minimal shop in the hub, so that I can sell duplicate items. **[assumption]**
52. As a host, I want the hub to be a shared space where up to 10 players can gather, so that it doubles as the lobby.
53. As a player, I want crafting and any blacksmith function to be deferred, so that the first release stays focused. A blacksmith class is a possible later idea.

### Death and stakes

54. As a solo player, I want to respawn at the most recent checkpoint a limited number of times per gate, so that one mistake does not cost me a long run.
55. As a solo player, I want to restart at the gate entrance after using my three revives, so that stakes remain.
56. As a co-op player who dies, I want the choice to wait for a teammate revive at the next checkpoint or respawn at the closest checkpoint, so that I am never stuck waiting. (Changed 2026-11-13 from "the entrance".)
57. As a co-op player, I want a wipe to cost progress inside the gate but never the character, so that failure never destroys what I have built.

### Story and presentation

58. As a player, I want a heavy authored story told through in-engine, text-only scenes, so that the story is rich without costing an animation budget.
59. As a player, I want the System to speak in a consistent voice through messages, quests, and mission text, so that it carries tone and story.
60. As a player, I want to create my character with a simple creator, so that I can express myself.
61. As a player, I want a fixed NPC cast to carry the story, so that the story works for any number of players.
62. As a player, I want a flat-shaded low-poly look with strong lighting, so that the game has a consistent identity that is feasible for a solo developer.
63. As a player, I want music and key art to be professionally made, so that the game's identity is anchored by its strongest assets.
64. As a player, I want a friend's name shown on their character, so that I know who is playing with me.
65. As a player, I want hunter ranks to be shown as an in-fiction rank board, so that I feel the climb without any competitive backend.

### Co-op

66. As a host, I want to invite friends through Steam, so that joining requires no network setup.
67. As a host, I want to run a session for up to 10 players, so that I can play with a larger group.
68. As a guest, I want to bring my own character and progression from my own save, so that my hunter is mine in anyone's game.
69. As a player, I want my character saved locally with optional Steam Cloud backup, so that I never lose progress.
70. As a guest, I want to earn XP and personal loot in a host's gate, so that visiting is worth my time.
71. As a host, I want story and map progress to belong to my world, so that guests do not advance or spoil my campaign.
72. As a guest, I want to "support" the host simply by being there, so that I can play fully without a special role.
73. As a player, I want enemies to scale in tiers (solo, 2 to 4 players, 5 to 10 players) through health, damage, and spawn counts, so that gates stay engaging at any size.
74. As a player, I want every normal gate to be completable solo, so that the solo power fantasy is always available.
75. As a group of friends, I want 10-player raid gates with split objectives and phases, so that a large crowd matters.
76. As a group of friends, I want raid mechanics that stay readable at ten players, so that screen clutter does not ruin them.
77. As a player, I want a high-level friend to be able to carry me through a gate, so that playing with friends of different power remains fun. Rewards are not reduced.
78. As a developer, I want player movement and combat to feel instant for guests, so that co-op does not feel laggy.
79. As a developer, I want mid-gate joining decided before co-op work starts. **Decided 2026-11-13: join and rejoin at the latest checkpoint (docs/adr/0003).**

### Developer experience and project health

80. As the developer, I want the simulation separated from rendering and networking, so that I can test it headlessly and add networking without rewriting combat.
81. As the developer, I want an in-editor test arena, so that I can tune a skill in minutes without running a gate.
82. As the developer, I want bot clients that stand in for other players, so that I can test 10-player raids alone.
83. As the developer, I want version control with large-file support and an offline backup, so that a lost month is never possible.
84. As the developer, I want to lock the engine to one long-term-support version and upgrade only between milestones, so that engine changes never break mid-feature work.
85. As the developer, I want the project private until a written trigger fires, so that I can build for myself first.
86. As the developer, I want commissioning budgets of up to $5,000 to be spent only after the trigger fires, with music and key art first, so that I do not spend money on an unproven design.

---

## Implementation Decisions

### Architecture

- **Single deterministic simulation core.** All gameplay logic (combat, skills, stats, loot, deaths, checkpoints, party scaling) runs in a fixed-timestep simulation that receives input commands per tick and produces state and events. It has no dependency on rendering, scene objects, or the network. Presentation reads from it. This is the primary architectural rule and the testing seam.
- **No singleton player.** Player state must be addressable per player from the first line, so single-player is the one-player case of the multiplayer model.
- **Data-driven content from day one.** Skills, items, enemies, gates, loot pools, classes, and ranks are data definitions. A tag system lets skills and items declare properties (for example fire, projectile, melee, summon), and modifiers act on tags. Hand-authored modifiers sit on top for signature skills.
- **Level cap, rank bands, and curves are configuration values,** not code constants.

### Networking

- **Host-run sessions** with up to 10 players, invited through Steam. One player is the host.
- **Fully server-authoritative with client-side prediction and reconciliation.** This is the most expensive option and was chosen deliberately. It makes the simulation-core rules above mandatory.
- **Library:** Fish-Net is the front-runner on the strength of built-in prediction and area-of-interest culling. Unity's first-party Netcode for GameObjects is not preferred because it lacks built-in prediction. **The choice is confirmed or changed in a spike at M2, not decided now.** Steam transport and relay are to be used. **[assumption: library research was from web sources and is unverified for current versions]**
- **Area-of-interest culling and effect culling rules** must be defined before the first raid is built, because ten players' effects can flood the screen.
- **Characters live in each player's own save,** with Steam Cloud as an optional backup. Story and map progress live in the host's world.

### Progression model

- **Ranks:** E, D, C, B, A (main game), then S (end story). SS is a possible future DLC. Ranks are tied to level bands, with some promotions gated by quests.
- **Provisional level bands:** E 1 to 15, D 16 to 35, C 36 to 55, B 56 to 75, A 76 to 95, S 96 to 100. Main-game cap 95, raised to 100 by the S story. **[assumption, tunable]**
- **Stats:** Strength, Agility, Vitality, Intelligence, Sense, Luck. Allocation is hybrid (a few free points per level plus class bonuses). Luck can nudge drop odds.
- **Classes:** classless through E. Three base classes at D (level-gated). Two specialties per base class at B (quest-gated), six in total. A hidden class at the end of S, played on a fresh slot. Classes share one base animation set. **Start with the recommended tree and extend later.**
- **Post-cap progression** is gear and skill modifiers.

### Combat

- Third-person, keyboard and mouse first. Free camera with soft lock-on. Light and heavy attacks, a six-slot skill bar, a dodge with brief invulnerability frames. Cooldowns only, with no mana or stamina.
- Hack-and-slash plus skills. Progression expresses itself through new abilities and larger effects rather than new animation sets.
- Encounters mix a few smart enemies (including elites and bosses) with occasional scripted swarm moments. Large networked swarms are out of the first release.

### Loot

- Personal loot. Every gate has a static, designer-defined pool. Clears give a guaranteed reward, with the specific item drawn at random from the pool.
- **Bad-luck protection:** items are drawn without replacement until the whole pool is owned. After that, duplicates can be sold. Using duplicates as upgrades is a possible later addition.
- Broken builds come from tag-based item synergies and skill modifiers, not raw stat inflation.
- No crafting in the first release.

### Gates and world

- Gates run 30 minutes or longer, with checkpoints and a handcrafted boss.
- Story gates are fixed layouts built from a modular room kit. Repeatable farming gates become procedurally shuffled later.
- The first gates should be kept short to speed up testing and content throughput.
- The hub is small, shared, and host-owned. It contains a gate board with mission intel, storage, and a minimal shop. Gates are chosen from the hub menu. The map opens by rank promotions and story beats.

### Deaths, scaling, and rewards

- Solo: three checkpoint revives per gate, then restart at the entrance.
- Co-op: a dead player chooses between waiting for a checkpoint revive or respawning at the closest checkpoint (the activated checkpoint nearest the living party; decided 2026-11-13). A wipe costs progress inside the gate only.
- No player-power scaling. Rewards are not reduced for over-leveled players.
- Enemy scaling in tiers (solo, 2 to 4, 5 to 10) through health and damage multipliers plus spawn counts. Every normal gate stays solo-completable. Raid gates (10 players) use split objectives and phases. A "roles by build" idea for raids is optional, if a good design emerges.

### Presentation

- Flat-shaded low-poly art with simple lighting. The genre's identity is carried by the System UI, lighting, and text-only in-engine story scenes.
- A simple character creator. A fixed NPC cast carries the heavy story.
- The System UI ships in stages: level-up and skill unlock prompts at M1, the mission board at M3, rank promotion screens at M4, and a friends' clear leaderboard last. Rank is in-fiction only, with no backend.

### Tooling and project health

- Data-driven content, an in-editor test arena, and bot clients that stand in for other players, with bots budgeted at M2.
- Git with large-file support and a remote, plus a periodic offline copy. A single locked long-term-support engine version upgraded only between milestones. A universal-style render pipeline suited to flat-shaded low-poly. **[assumption: exact versions unverified]**

### Milestones

1. **M1:** greybox single-player: capsule characters, one attack, one enemy type, a level-up screen, and the level-up and skill unlock UI. Built on the simulation-core rules above.
2. **M2:** two-player Steam co-op on the same slice, with host authority, plus a 10-bot stress test. Netcode library confirmed in a spike. Mid-gate joining decided beforehand.
3. **M3:** the hub, one modular gate, one handcrafted boss, and the mission board. Story premise decided beforehand.
4. **M4:** loot pools, skill unlocks, the first job change, and rank promotions.
5. **M5:** the first 10-player raid gate.

### Kill switch

After M3, if friends ask to play again without being prompted, start spending the commission budget and consider going public. If not, redesign before building more. The developer should rewrite this trigger in their own words before starting.

### Parked decisions

| Decision | Decide by |
|---|---|
| Story premise: drama about the people, or about the world and why it is the way it is. Candidate framing: the System itself as the mystery. | Before M3 |
| Mid-gate joining: gates lock, or join at checkpoints | Decided 2026-11-13: join and rejoin at checkpoints (ADR 0003) |
| Blacksmith class and crafting | M4 or later |
| Hidden class details | After the main game exists |
| SS-rank DLC | After the main game exists |
| "Roles by build" raid design | When a good design emerges |

---

## Testing Decisions

### What makes a good test

Test external behavior through the simulation boundary: given inputs and data, assert observable outcomes (state, events, rewards). Do not test internal implementation details, rendering, or engine objects.

### The seam

**One seam: the headless simulation boundary.** Tests send per-tick input commands for one or more players and assert on resulting state and events. The same boundary serves single-player, co-op (through bot clients), and content validation.

### What to test through the seam

- **Progression:** level-ups grant the right stats and unlocks, rank promotions trigger at the right levels, and quest-gated promotions require their quests.
- **Combat and skills:** cooldowns, dodge invulnerability windows, and tag-based modifier interactions produce expected damage and effects.
- **Loot:** guaranteed rewards on clear, draws without replacement until the pool is owned, and duplicate handling afterward.
- **Death and checkpoints:** three solo revives then restart, co-op revive-or-respawn choice, and wipes costing in-gate progress only.
- **Party scaling:** enemy health, damage, and spawn counts at each tier, and solo-completability of normal gates.
- **Co-op via bots:** N bot clients (up to 10) exercise raid mechanics, authority rules, and reconciliation without human players.
- **Content validation:** every skill, item, enemy, and gate definition loads and passes consistency checks.

### Prior art

None, since this is a greenfield project. Unity's own co-op sample projects are the closest reference for multiplayer structure, but they are not a testing model.

### Not covered by automated tests

Feel and enjoyment, which are checked by hand with the milestone feel tests below.

### Feel tests (manual)

| Milestone | Test |
|---|---|
| M1 | Levelling up makes me feel an increase in power. Using skills feels easy and rewarding to pull off. |
| M3 | Clearing a dungeon is enjoyable. |
| After M3 | Friends ask to play again unprompted (the kill-switch trigger). |

---

## Out of Scope

- Licensed or existing IP. The game is original and borrows only genre tropes.
- Crafting and blacksmith systems in the first release.
- True PvP, global leaderboards, and any online backend.
- Trading between players, shared loot pools, and a player economy beyond selling duplicates.
- Dedicated servers and anti-cheat, since sessions are host-run among friends.
- Animated or voiced cutscenes. Story is told through text-only in-engine scenes.
- Large networked enemy swarms with hundreds of enemies.
- Open-world streaming. Gates are entered through the hub menu.
- Player-power scaling, level-scaling enemies, and rank-locked joining.
- Mobile, console, and free-to-play or live-service models.
- New Game Plus and carry-over systems. The hidden class uses a fresh slot.
- The SS-rank DLC, and the full class tree beyond the starting recommendation.

---

## Further Notes

- **Capacity reality.** At roughly 250 hours a year, 30+ hours of content at about 30 minutes per gate means 40 to 60 gates plus bosses. That is feasible only with the modular room kit, data-driven tooling, and a long timeline. M3 is where the pace will be proven. If gates take too long, shorten them or reduce count before cutting systems.
- **Scope risk.** Three of the most expensive options were chosen together: 3D action combat, fully server-authoritative co-op with prediction, and a heavy story. This was a deliberate choice. The milestone ladder exists to surface any problem early, and the first things to cut if the project stalls are procedural farming gates, the full class tree, and raid complexity.
- **Co-op audience.** The developer expects six to ten regular players, so 10-player raids are a core feature, not a stretch goal. The 10-bot stress test at M2 protects against discovering a scaling problem late.
- **Co-op and the power fantasy.** Because there is no power scaling, a strong friend can trivialize a weaker host's gate. This was accepted as the player's choice, in the manner of Monster Hunter. Guests earn personal loot and XP but story progress stays with the host.
- **Art identity.** Without cel shading, manhwa flavor must come from the System UI, lighting, and key art. Commissioning music and key art first fits this.
- **Unverified items.** Netcode library comparisons, the current long-term-support engine version, and the render pipeline choice came from quick research or recommendation and should be checked at the start of the project and again at M2.
- **Tracker.** No issue tracker was configured when this was written. To publish it, run the setup skill for the project's repo and file this spec with the `ready-for-agent` label.
