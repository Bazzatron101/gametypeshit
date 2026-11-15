# 23: Gear and tag synergies

**What to build:** Gear can be equipped, and its tags combine with skills through the modifier system to produce a demonstrably broken build.

**Blocked by:** 08: Tag-based modifiers, 22: Loot pools and duplicate selling

**Status:** done

- [x] Equipment slots and an equip flow exist (six slots; I opens the gear menu)
- [x] Gear tags feed the modifier system from the earlier ticket (worn modifiers, and `PerWornTag`: ADR 0017)
- [x] A documented combination of gear and skills produces an outsized effect (the Cinder set on Fireball: 57x)
- [x] Headless tests verify the combination (GearTest)

## Comments

**2026-11-15 (developer, recorded by agent):** Decided:
- **Slots:** six: weapon, head, body, hands, feet, trinket.
- **Order:** ticket 22 is being built now, so gear waits for it rather than building a stand-in.

**2026-11-15 (agent):** Not started in code. Notes for when 22 lands:
- **What 23 needs from 22:** only a way to get items. The rest of 23 (item definitions, a bag of owned items, equip slots, the save, snapshots, bots) does not depend on 22.
- **Why wait:** the 22 work in progress changes the same files gear needs: `CharacterRecord`/`CharacterCodec` (it takes format version 3), `PlayerState`, snapshots, `Wire`, the event codec and `GrantClearRewards`. Two changes to those files at once would collide, and both would bump the save version.
- **A design point to settle with 22:** in 22 as built so far, loot is still modifiers (`RewardPool` is a list of `ModifierDefinition`, and `SellValue` sits on the modifier). Gear needs items: a piece with a slot, tags and the modifiers it grants while worn. So 23 will either let the pool hold items as well as modifiers, or turn pool entries into items, some of which are gear. Duplicates and selling would then work on items. Easier to decide before 22 is committed.
- **Plan once 22 is in:** `ItemDefinition` (id, name, slot, tags, modifiers granted while worn, sell value), owned items on the character, an equip command (equipping grants the item's modifiers through `GrantModifier`, unequipping revokes them; `DebugModifierToggles` goes), save format 4, items in snapshots, bot teammates equipping by role, give `AttackDefinition` tags so melee gear reaches light and heavy attacks, and a headless test of a broken build.
- **ADR:** the next free number is 0016 (0015 is the walkable overworld). The 22 work may take 0016 first.

**2026-11-15 (agent):** Built, on top of 22. The rules are in `docs/adr/0017-gear-is-worn-from-a-bag-and-counts-by-tag.md` (proposed).

What was built:
- **Items:** modifiers and gear share one base, `LootDefinition`, so a gate's pool holds both. The draw, no repeats and duplicates work the same for each.
- **Gear** (`GearDefinition`): a slot, tags, and modifiers that work only while worn. Authored as `GearDefinitionAsset`s in `Assets/Data/Gear/`, listed in GameContent's new gear list. Gates have a gear pool beside the modifier pool.
- **The bag and the slots:** every piece found goes in the bag. Equip and unequip are commands (`EquipGearId`, `UnequipSlot`), carried over the wire. A piece from a gate goes on at once if its slot is empty. Events: `GearEquipped`, `GearUnequipped`.
- **Tags count:** `ModifierDefinition.PerWornTag` makes a modifier count once per worn piece with that tag. This works on gear and on gate modifiers (the modifier asset has the field too).
- **Saves:** character format 4 (bag, worn slots, and the worn-tag field). Format 3 saves load with an empty bag. Snapshots carry the bag and the slots.
- **Bot teammates** wear the best piece for their role (`CompanionRoles.GearToWear`) and swap only for something better.
- **Content:** nine pieces. The Cinder set (Staff, Hood, Robe, Gloves, Boots) and the Ember Heart for fire; the Goblin Cleaver, Warlord's Plate and Hunter's Quiver for the others. The Goblin Warren drops the Cleaver, Plate, Quiver and Ember Heart.
- **On screen:** the gear menu (`GearPanel`, I), added by the runner in every scene. The arena panel can give every piece or wear one. The gate's notice names a gear reward and its slot.
- Small fixes found on the way: autosave now also follows rewards, sales and gear changes (a duplicate or a sale was only saved on leaving), and a teammate catching up to the host's level kept no gold or duplicates; it now keeps them, and its gear.

Evidence:
- `GearTest` (24 tests): slots, swapping, taking off, nothing worn from outside the bag or while defeated; worn gear changes only skills with its tag; worn-tag counting; validation; gear from a clear; no repeats, then duplicates; saves (format 4, format 3, gear removed from the game, wrong slots); snapshots byte-identical after a round trip; the wire; teammates by role.
- **The broken build**, from the game's own content: a level-1 Wizard's Fireball, pressed for 10 s into a pack of six. Bare: 90 damage. Full Cinder set: 5,168 (57x). The six pieces worn one at a time add 458 between them; together they add 5,078, about eleven times as much.
- `ContentValidationTest`: every gear asset is listed, gear pools name listed pieces, item ids are unique across modifiers and gear, and the first gate has something for the tank, the marksman and the caster.
- `pwsh scripts/test.ps1` (EditMode): 527 passed, 0 failed. PlayMode was not run.

Not done:
- **Not checked by a person:** the gear menu, the reward notice, the arena buttons, and how the Cinder set feels.
- **Light and heavy attacks** still have no tags, so melee gear reaches only melee skills.
- **`DebugModifierToggles`** (F5 to F8) is still there; it can go once gear has been tried in the game.
- **Changed from the plan above:** gear is read when a skill is used instead of going through `GrantModifier`, so a save never mixes gear modifiers with the character's own.
- **Showing gear** on the character is ticket 34.
