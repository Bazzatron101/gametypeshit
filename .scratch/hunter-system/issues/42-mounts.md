# 42: Mounts

**What to build:** Riding in the overworld. The first mount comes with the D-rank promotion and runs at 2× sprint, 16 m/s (ticket 36, ADR 0015). The mount belongs to the character (ADR 0005), so a guest brings their own; a guest still at E-rank has none and walks. There is no riding double.

**Blocked by:** 38: The overworld as a third kind of place, 40: Load the overworld in tiles around players

**Status:** ready-for-human (ride the test patch: press F2 for a mount, then R)

- [x] Mount and dismount commands in the simulation, predicted like other movement (ADR 0004)
- [x] Mounted speed is a configuration value (16 m/s to start), beside walking and sprint
- [x] Mounts work in the overworld only; entering a gate or the town dismounts
- [x] Getting hit, or other rules decided in triage, knock a rider off
- [x] The mount is saved on the character record and given at the D-rank promotion (ticket 25): saved, with a `GrantMount` hook for ticket 25 to call; ranks do not exist yet
- [x] Mounted riders follow the same ground rules as walkers (ticket 37); whether a mount can climb is decided in triage
- [x] Tests: mounting, speed, dismount on entering a gate, an E-rank character has no mount, the save round trip

**2026-11-16 (developer, recorded by agent):** Triaged: build now ("this still feels very basic").

**2026-11-16 (developer, recorded by agent):** Three choices before building:
- **No fighting from the saddle:** a hit, or starting an attack, a skill or a dodge, puts the rider on foot. Jumping is allowed.
- **Mounts do not climb:** climbable faces are cliffs to a rider; dismount to climb.
- **Until ranks exist,** a `GrantMount` hook (ticket 25 calls it at the D-rank promotion) and a button in the test arena give a mount. New characters have none.

**2026-11-16 (agent):** Built. EditMode 651 passed.

- **Simulation** (`SimulationWorld.Mounts.cs`): `GrantMount(player)`, a `ToggleMount` press in `PlayerCommand` (like jump), and the rules. A rider moves at `SimulationConfig.MountSpeed` (16 m/s), whatever sprint or agility say. Getting on needs a mount, the overworld, outside the town (`OverworldDefinition.TownRadius`, 40 m; the baked town marker's reach), and not being mid-jump, dodge, windup or charge. A hit (enemy or boss), an attack, a skill or a dodge dismounts first, and the action still happens. Going down, riding into the town or entering a gate also put the rider on foot. Riders settle on the ground with climbable faces as cliffs (`NoClimbingGround`).
- **Events:** `MountChanged` (on or off) and `MountGranted`, in the codec.
- **Saved:** `CharacterRecord.HasMount` (character format 6; older saves load with none). A character arrives on foot. Snapshots carry who owns a mount and who is riding, so guests predict it.
- **Input and wire:** R toggles the mount (`LocalInputCommandSource`, `PlayerCommandBuffer`). Commands carry it in one more byte. A command with a mount press is never shed from a backlog.
- **Shown:** a rider sits higher on a brown greybox block (`PlayerView`) until there is a model.
- **For testing until ranks exist:** the test arena has a "Give a mount" button (riding needs an overworld, so it is for the save, not for riding there). In an overworld scene, F2 gives the local player a mount, in development builds only, on the host or offline.
- **Tests:** `MountTest` (14): no mount for a new character, granting, mounting and leaving, 16 m/s with sprint held, no riding in the town and riding into it dismounts, a gate dismounts, no riding without an overworld, attacking and dodging dismount (the dodge still happens), jumping does not, a hit knocks a rider off, a mount does not climb but a walker does, saved with the character, an old save has none, a snapshot keeps the rider, and a guest's ride predicted with no correction.

**Left for later:**
- The patch scene needed no rebuild: its baked town marker already has a 40 m reach, which the overworld now passes on as the town radius.
- No gamepad button for the mount yet; R on the keyboard only.
- Bot teammates do not ride yet (ticket 43: they must keep up on a mount).
- A real mount model and animations.
