using System;
using System.Collections.Generic;
using System.Linq;

namespace HunterSystem.Simulation
{
    /// <summary>Gear (ticket 23): the bag, the six slots, and what worn gear adds to skills.</summary>
    public sealed partial class SimulationWorld
    {
        /// <summary>Every piece of gear in the game, by id. Saves, snapshots and events name gear by id.</summary>
        private Dictionary<string, GearDefinition> _gearById;

        private void IndexAndValidateGear()
        {
            _gearById = new Dictionary<string, GearDefinition>(StringComparer.Ordinal);
            var weaponHits = new List<SkillDefinition>();
            foreach (var gear in _config.Gear ?? Array.Empty<GearDefinition>())
            {
                if (gear == null) throw new ArgumentException("A piece of gear is missing (null).", nameof(_config));
                void Require(bool ok, string problem)
                {
                    if (!ok) throw new ArgumentException($"Gear '{gear.Id}': {problem}", nameof(_config));
                }

                Require(!string.IsNullOrWhiteSpace(gear.Id), "has no Id.");
                Require(!string.IsNullOrWhiteSpace(gear.Name), "has no Name.");
                Require(!_gearById.ContainsKey(gear.Id), "is defined twice.");
                Require(Enum.IsDefined(typeof(GearSlot), gear.Slot), $"slot {(int)gear.Slot} is not a slot.");
                Require(gear.SellValue >= 1, "SellValue must be at least 1.");
                foreach (var tag in gear.Tags ?? Array.Empty<string>())
                    Require(_knownTags.Contains(tag), $"tag '{tag}' is not a known tag.");
                foreach (var modifier in gear.Modifiers)
                {
                    try { ValidateModifier(modifier); }
                    catch (ArgumentException e) { throw new ArgumentException($"Gear '{gear.Id}': {e.Message}", nameof(_config), e); }
                }
                var bonusStats = new HashSet<Stat>();
                foreach (var bonus in gear.StatBonuses)
                {
                    Require(Enum.IsDefined(typeof(Stat), bonus.Stat), $"stat {(int)bonus.Stat} is not a stat.");
                    Require(bonus.Amount != 0, $"its {bonus.Stat} bonus is 0.");
                    Require(bonusStats.Add(bonus.Stat), $"has more than one {bonus.Stat} bonus.");
                }
                // A minus always comes with an upside (ticket 51): a piece whose stats add up to a loss must do something else.
                Require(gear.StatBonuses.Sum(b => b.Amount) >= 0 || gear.Modifiers.Count > 0 || gear.LightAttack != null || gear.HeavyAttack != null,
                    "its stat bonuses add up to a loss and it has nothing else to make it worth wearing (no modifiers, no attacks).");
                foreach (var attack in new[] { gear.LightAttack, gear.HeavyAttack })
                {
                    if (attack == null) continue;
                    Require(gear.Slot == GearSlot.Weapon, "only a weapon has attacks.");
                    Require(attack.Hit != null, "an attack has no hit.");
                    Require(attack.WindupSeconds >= 0f, "an attack's WindupSeconds is negative.");
                    Require(_config.Skills.All(s => s.Id != attack.Hit.Id), $"its attack '{attack.Hit.Id}' shares its Id with a skill.");
                    weaponHits.Add(attack.Hit);
                }
                _gearById[gear.Id] = gear;
            }
            foreach (var c in _config.Classes ?? Array.Empty<ClassDefinition>())
                if (c?.StartingWeaponId != null && GearById(c.StartingWeaponId)?.Slot != GearSlot.Weapon)
                    throw new ArgumentException($"Class '{c.Id}': starting weapon '{c.StartingWeaponId}' is not a weapon in the game's gear list.", nameof(_config));
            ValidateSkills(weaponHits, _config.Progression.LevelCap, _knownTags, _config.Classes, weaponHits: true);

            // A gate's pool names the game's own gear, so a drop and a save mean the same piece.
            foreach (var gate in _config.Gates ?? Array.Empty<GateDefinition>())
            foreach (var gear in gate.RewardPool.OfType<GearDefinition>())
                if (!_gearById.TryGetValue(gear.Id ?? "", out var known) || known != gear)
                    throw new ArgumentException($"Gate '{gate.Id}' drops gear '{gear.Id}', which is not in the game's gear list.", nameof(_config));
        }

        /// <summary>A piece of gear by id, or null if the game has none by that id.</summary>
        public GearDefinition GearById(string id) => id != null && _gearById.TryGetValue(id, out var gear) ? gear : null;

        /// <summary>
        /// Puts a piece into the player's bag (a test arena, a reward). It takes effect at the start of
        /// the next tick, like <see cref="GrantModifier"/>. A piece the player already has is not added
        /// twice. With <paramref name="equip"/>, it is also worn, replacing whatever was in its slot.
        /// </summary>
        public void GiveGear(PlayerId id, string gearId, bool equip = false)
        {
            var player = FindPlayer(id);
            var gear = GearById(gearId) ?? throw new ArgumentException($"No gear has the id '{gearId}'.", nameof(gearId));
            Enqueue(tick =>
            {
                if (!player.Bag.Contains(gear)) player.Bag.Add(gear);
                if (equip) TryEquip(player, gear.Id, tick);
            });
        }

        /// <summary>Whether the player has this piece in their bag (worn or not).</summary>
        public bool HasGear(PlayerId id, string gearId) => FindPlayer(id).Bag.Any(g => g.Id == gearId);

        /// <summary>Wears a piece from the bag in its slot. What was there goes back to the bag. A defeated player cannot change gear.</summary>
        private void TryEquip(Player player, string gearId, int tick)
        {
            if (!player.IsAlive) return;
            var gear = player.Bag.FirstOrDefault(g => g.Id == gearId);
            if (gear == null) return;
            var slot = (int)gear.Slot;
            if (player.Worn[slot] == gear) return;
            if (player.Worn[slot] != null) _events.Add(new GearUnequipped(tick, player.Id, gear.Slot, player.Worn[slot].Id));
            player.Worn[slot] = gear;
            RefreshMaxHealthAfterGearChange(player);
            _events.Add(new GearEquipped(tick, player.Id, gear.Slot, gear.Id));
        }

        private void TryUnequip(Player player, GearSlot slot, int tick)
        {
            if (!player.IsAlive || !Enum.IsDefined(typeof(GearSlot), slot)) return;
            var worn = player.Worn[(int)slot];
            if (worn == null) return;
            player.Worn[(int)slot] = null;
            RefreshMaxHealthAfterGearChange(player);
            _events.Add(new GearUnequipped(tick, player.Id, slot, worn.Id));
        }

        /// <summary>
        /// A character with no weapon at all, worn or in the bag, is issued its class's (ticket 46),
        /// and wears it. Quietly, like starter skills: it comes with the character.
        /// </summary>
        private void IssueStartingWeapon(Player player)
        {
            var weapon = GearById(player.Class?.StartingWeaponId);
            if (weapon == null || player.Worn[(int)GearSlot.Weapon] != null || player.Bag.Any(g => g.Slot == GearSlot.Weapon)) return;
            player.Bag.Add(weapon);
            player.Worn[(int)GearSlot.Weapon] = weapon;
        }

        /// <summary>A new piece from a gate goes on at once if its slot is empty, so a first find is felt straight away.</summary>
        private void GainGear(Player player, GearDefinition gear, int tick)
        {
            player.Bag.Add(gear);
            if (player.Worn[(int)gear.Slot] == null) TryEquip(player, gear.Id, tick);
        }

        /// <summary>Whether the player already has this item: a modifier they keep, or gear in their bag.</summary>
        private static bool Owns(Player player, LootDefinition item) => item switch
        {
            GearDefinition gear => player.Bag.Any(g => g.Id == gear.Id),
            _ => player.Modifiers.Any(m => m.Id == item.Id),
        };

        /// <summary>How many pieces the player wears that carry <paramref name="tag"/>.</summary>
        private static int WornWithTag(Player player, string tag)
        {
            var count = 0;
            foreach (var gear in player.Worn)
                if (gear != null && gear.HasTag(tag)) count++;
            return count;
        }

        /// <summary>Every modifier in effect on the player: their own, then those of the gear they wear, slot by slot.</summary>
        private static IEnumerable<ModifierDefinition> ModifiersInEffect(Player player)
        {
            foreach (var m in player.Modifiers) yield return m;
            foreach (var specialty in player.Specialties)
                foreach (var m in specialty.Passives) yield return m; // always on (ticket 26)
            foreach (var gear in player.Worn)
                if (gear != null)
                    foreach (var m in gear.Modifiers) yield return m;
        }
    }
}
