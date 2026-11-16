# 48: A gear screen that shows what you have

**What to build:** Rework the gear menu (`GearPanel`, I) so you can easily see what you own and what you wear, and equip and unequip pieces. From the developer's playtest, 2026-11-15: the inventory and gear "feels weird", and the gear system needs more work "so you can better see what you have and equip and unequip things".

**Blocked by:** 23: Gear and tag synergies

**Status:** done

- [x] The six worn slots are shown clearly, with what is in each
- [x] The bag lists every owned piece, and what a piece does (its modifiers and tags) is easy to read
- [x] Equipping and unequipping is direct (for example a click on a piece or a slot)
- [x] It is clear what changes when you swap one piece for another
- [x] Works the same for the host and a guest

## Comments

**2026-11-15 (agent):** Opened from the playtest note on ticket 23. Ticket 34 (gear drawn on the character) would help you see what you wear too.

**2026-11-16 (developer, recorded by agent):** Triaged: build now ("this still feels very basic").

**2026-11-16 (agent):** Built. The gear screen (I) is reworked (`GearPanel`, words in `GearText`).
- **Left, "Wearing":** the six slots, each with the piece worn and what it does in plain words (a weapon's light and heavy attacks with damage, reach and speed; each modifier as one line, like "+15% damage to fire attacks and skills"). "Take off" on each. A slot's button shows how many spares you have for it; click it to see only those.
- **Right, "Bag":** filter buttons (All, Weapon, Head, Body, Hands, Feet, Trinket), then every spare piece with its slot and its main effect, and an "Equip" button.
- **Details:** click a piece to see it in full (description, tags, every effect) and how it compares with what you wear in that slot: weapons side by side ("Slash → Shot (12 → 9 damage, 3.2 → 16 m reach ...)"), other pieces as green gains and red losses.
- Host and guest use the same screen; equip and unequip are still commands to the host.
- Tests: `GearScreenTest` (5: weapon lines, modifier lines, comparing weapons, comparing armour, the filtered bag). EditMode: 637 passed; PlayMode: 36 passed.
- Not done: gear drawn on the character (ticket 34), and gear changing stats like Vitality (gear only has skill modifiers today).
- **To check:** in the arena (every weapon is in the bag there) press I, filter, pick pieces and compare.

**2026-11-16 (developer, recorded by agent):** Tried it: "very basic and will need a touch up but it is good for now." Closed as done. A visual and feel touch-up comes later; no specifics yet. Gear that changes stats is ticket 51.
