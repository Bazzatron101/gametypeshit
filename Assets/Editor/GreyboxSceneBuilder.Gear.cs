using System.Collections.Generic;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using UnityEditor;
using UnityEngine;

namespace HunterSystem.Editor
{
    /// <summary>
    /// More gear (ticket 61): four armour sets, one per style (heavy plate, medium, light leathers,
    /// robes), and two weapons for each class, a Rare and a Legendary, locked to that class. All the
    /// numbers live here, so a piece's description is worked out from what it does. Every piece is
    /// made once; after that the Inspector's values stand, as with all content.
    ///
    /// Where each drops (each gate keeps its own list, so a piece can be farmed):
    /// heavy and medium sets from Thornwood Hollow (E); light leathers and robes, and every
    /// Legendary weapon, from the Goblin King's raid; each class's Rare weapon from its D trial and
    /// its Legendary from its B trial.
    /// </summary>
    public static partial class GreyboxSceneBuilder
    {
        private sealed class GearData
        {
            public GearDefinition Piece;
            public GearDefinitionAsset.WornModifier[] Modifiers = new GearDefinitionAsset.WornModifier[0];
            public GearDefinitionAsset.Attack Light;
            public GearDefinitionAsset.Attack Heavy;
            public string DropsFrom; // "thornwood", "raid", "trial-d:<class>", "trial-b:<class>"
        }

        private static GearDefinitionAsset.WornModifier Mod(string id, string name, string tag, int damagePercent = 0, int cooldownPercent = 0, float rangeAdd = 0f, float radiusAdd = 0f, int extraProjectiles = 0) =>
            new GearDefinitionAsset.WornModifier { id = id, displayName = name, tag = tag, damagePercent = damagePercent, cooldownPercent = cooldownPercent, rangeAdd = rangeAdd, radiusAdd = radiusAdd, extraProjectiles = extraProjectiles };

        private static GearDefinitionAsset.Attack Hit(string id, string name, SkillEffectKind effect, Stat stat, int damage, float cooldown, string[] tags,
            float range = 2.5f, float arc = 120f, float radius = 0.5f, float speed = 20f, bool pierces = false, float windup = 0f) =>
            new GearDefinitionAsset.Attack
            {
                id = id, displayName = name, effect = effect, scalingStat = stat, damage = damage, cooldownSeconds = cooldown, tags = tags,
                range = range, arcDegrees = arc, radius = radius, projectileSpeed = speed, pierces = pierces, windupSeconds = windup,
            };

        private static readonly string[] MeleeTags = { "melee", "physical" };
        private static readonly string[] ShotTags = { "projectile", "physical" };
        private static readonly string[] BoltTags = { "projectile", "magic" };
        private static readonly string[] BlastTags = { "area", "magic" };

        private static GearData Armour(string id, string name, GearSlot slot, Rarity rarity, string[] tags, (Stat stat, int amount)[] stats, string dropsFrom,
            params GearDefinitionAsset.WornModifier[] modifiers)
        {
            var piece = new GearDefinition { Id = id, Name = name, Slot = slot, Rarity = rarity, Tags = tags, SellValue = SellValueOf(rarity) };
            foreach (var (stat, amount) in stats) piece.StatBonuses.Add(new StatBonus(stat, amount));
            return new GearData { Piece = piece, Modifiers = modifiers, DropsFrom = dropsFrom };
        }

        private static GearData Weapon(string id, string name, string classId, Rarity rarity, string[] tags, (Stat stat, int amount)[] stats,
            GearDefinitionAsset.Attack light, GearDefinitionAsset.Attack heavy, params GearDefinitionAsset.WornModifier[] modifiers)
        {
            var piece = new GearDefinition { Id = id, Name = name, Slot = GearSlot.Weapon, Rarity = rarity, Tags = tags, ClassIds = new[] { classId }, SellValue = SellValueOf(rarity) };
            foreach (var (stat, amount) in stats) piece.StatBonuses.Add(new StatBonus(stat, amount));
            var dropsFrom = (rarity == Rarity.Legendary ? "trial-b:" : "trial-d:") + classId;
            return new GearData { Piece = piece, Modifiers = modifiers, Light = light, Heavy = heavy, DropsFrom = dropsFrom };
        }

        private static int SellValueOf(Rarity rarity) => rarity switch { Rarity.Common => 60, Rarity.Rare => 120, Rarity.Epic => 220, _ => 400 };

        private static (Stat, int)[] S(params (Stat, int)[] stats) => stats;

        /// <summary>The four armour sets. Head and feet are Common, hands Rare, the body Epic with a bonus of its own.</summary>
        private static readonly GearData[] SetData =
        {
            // Heavy plate: Strength and Vitality, for the front line.
            Armour("ironwall-helm", "Ironwall Helm", GearSlot.Head, Rarity.Common, MeleeTags, S((Stat.Vitality, 2)), "thornwood"),
            Armour("ironwall-greaves", "Ironwall Greaves", GearSlot.Feet, Rarity.Common, MeleeTags, S((Stat.Strength, 2)), "thornwood"),
            Armour("ironwall-gauntlets", "Ironwall Gauntlets", GearSlot.Hands, Rarity.Rare, MeleeTags, S((Stat.Strength, 3), (Stat.Vitality, 2)), "thornwood"),
            Armour("ironwall-cuirass", "Ironwall Cuirass", GearSlot.Body, Rarity.Epic, MeleeTags, S((Stat.Vitality, 5), (Stat.Strength, 3), (Stat.Agility, -1)), "thornwood",
                Mod("ironwall-cuirass-bulwark", "Ironwall Cuirass: bulwark", "melee", damagePercent: 10)),

            // Medium: Agility and Vitality, for fighters on the move.
            Armour("wayfarer-hat", "Wayfarer's Hat", GearSlot.Head, Rarity.Common, new[] { "physical" }, S((Stat.Agility, 2)), "thornwood"),
            Armour("wayfarer-boots", "Wayfarer's Boots", GearSlot.Feet, Rarity.Common, new[] { "physical" }, S((Stat.Vitality, 2)), "thornwood"),
            Armour("wayfarer-gloves", "Wayfarer's Gloves", GearSlot.Hands, Rarity.Rare, new[] { "physical" }, S((Stat.Agility, 3), (Stat.Vitality, 2)), "thornwood"),
            Armour("wayfarer-coat", "Wayfarer's Coat", GearSlot.Body, Rarity.Epic, new[] { "physical", "projectile" }, S((Stat.Agility, 4), (Stat.Vitality, 3)), "thornwood",
                Mod("wayfarer-coat-quickdraw", "Wayfarer's Coat: quick draw", "projectile", cooldownPercent: -10)),

            // Light leathers: Agility and Luck, for strikers who live by dodging.
            Armour("nightweave-hood", "Nightweave Hood", GearSlot.Head, Rarity.Common, MeleeTags, S((Stat.Luck, 2)), "raid"),
            Armour("nightweave-slippers", "Nightweave Slippers", GearSlot.Feet, Rarity.Common, MeleeTags, S((Stat.Agility, 2)), "raid"),
            Armour("nightweave-grips", "Nightweave Grips", GearSlot.Hands, Rarity.Rare, MeleeTags, S((Stat.Agility, 3), (Stat.Luck, 2)), "raid"),
            Armour("nightweave-vest", "Nightweave Vest", GearSlot.Body, Rarity.Epic, MeleeTags, S((Stat.Agility, 4), (Stat.Luck, 3)), "raid",
                Mod("nightweave-vest-flurry", "Nightweave Vest: flurry", "melee", cooldownPercent: -10)),

            // Robes: Intelligence and Sense, for casters, healers and summoners.
            Armour("starweave-circlet", "Starweave Circlet", GearSlot.Head, Rarity.Common, new[] { "magic" }, S((Stat.Intelligence, 2)), "raid"),
            Armour("starweave-sandals", "Starweave Sandals", GearSlot.Feet, Rarity.Common, new[] { "magic" }, S((Stat.Sense, 2)), "raid"),
            Armour("starweave-wraps", "Starweave Wraps", GearSlot.Hands, Rarity.Rare, new[] { "magic" }, S((Stat.Intelligence, 3), (Stat.Sense, 2)), "raid"),
            Armour("starweave-robe", "Starweave Robe", GearSlot.Body, Rarity.Epic, new[] { "magic" }, S((Stat.Intelligence, 4), (Stat.Sense, 3)), "raid",
                Mod("starweave-robe-focus", "Starweave Robe: focus", "magic", damagePercent: 12)),
        };

        /// <summary>Two weapons per class: a Rare (from its D trial) and a Legendary (from its B trial, and the raid) with two bonuses of its own.</summary>
        private static readonly GearData[] WeaponData =
        {
            // Warrior
            Weapon("knights-longsword", "Knight's Longsword", "warrior", Rarity.Rare, MeleeTags, S((Stat.Strength, 3)),
                Hit("longsword-slash", "Slash", SkillEffectKind.Cone, Stat.Strength, 14, 0.4f, MeleeTags, range: 3.4f, arc: 150f),
                Hit("longsword-thrust", "Thrust", SkillEffectKind.Cone, Stat.Strength, 34, 0.9f, MeleeTags, range: 4.6f, arc: 35f)),
            Weapon("worldsplitter", "Worldsplitter", "warrior", Rarity.Legendary, MeleeTags, S((Stat.Strength, 5), (Stat.Vitality, 4)),
                Hit("worldsplitter-cleave", "Cleave", SkillEffectKind.Cone, Stat.Strength, 18, 0.45f, MeleeTags, range: 3.6f, arc: 170f),
                Hit("worldsplitter-quake", "Quake", SkillEffectKind.Area, Stat.Strength, 40, 1.4f, new[] { "melee", "physical", "area" }, radius: 3.5f, windup: 0.25f),
                Mod("worldsplitter-might", "Worldsplitter: might", "melee", damagePercent: 15),
                Mod("worldsplitter-reach", "Worldsplitter: reach", "area", radiusAdd: 1f)),

            // Rogue (no starting weapon: these are its first)
            Weapon("twin-daggers", "Twin Daggers", "rogue", Rarity.Rare, MeleeTags, S((Stat.Agility, 3)),
                Hit("daggers-flick", "Flick", SkillEffectKind.Cone, Stat.Agility, 8, 0.2f, MeleeTags, range: 2.2f, arc: 100f),
                Hit("daggers-backstab", "Backstab", SkillEffectKind.Cone, Stat.Agility, 30, 0.8f, MeleeTags, range: 2.4f, arc: 40f)),
            Weapon("whisperfang", "Whisperfang", "rogue", Rarity.Legendary, MeleeTags, S((Stat.Agility, 5), (Stat.Luck, 4)),
                Hit("whisperfang-flick", "Flick", SkillEffectKind.Cone, Stat.Agility, 10, 0.18f, MeleeTags, range: 2.4f, arc: 110f),
                Hit("whisperfang-fan", "Fan of Knives", SkillEffectKind.Projectile, Stat.Agility, 22, 0.9f, ShotTags, range: 12f, radius: 0.4f, speed: 30f),
                Mod("whisperfang-edge", "Whisperfang: edge", "melee", damagePercent: 15),
                Mod("whisperfang-fan-spread", "Whisperfang: spread", "projectile", extraProjectiles: 2)),

            // Archer
            Weapon("yew-longbow", "Yew Longbow", "archer", Rarity.Rare, ShotTags, S((Stat.Agility, 3)),
                Hit("longbow-shot", "Arrow", SkillEffectKind.Projectile, Stat.Agility, 11, 0.45f, ShotTags, range: 18f, radius: 0.35f, speed: 32f),
                Hit("longbow-drawn-shot", "Drawn Shot", SkillEffectKind.Projectile, Stat.Agility, 30, 1.2f, ShotTags, range: 24f, radius: 0.45f, speed: 42f, pierces: true)),
            Weapon("galeheart", "Galeheart", "archer", Rarity.Legendary, ShotTags, S((Stat.Agility, 5), (Stat.Sense, 4)),
                Hit("galeheart-shot", "Gale Arrow", SkillEffectKind.Projectile, Stat.Agility, 13, 0.4f, ShotTags, range: 20f, radius: 0.4f, speed: 36f, pierces: true),
                Hit("galeheart-volley", "Volley", SkillEffectKind.Projectile, Stat.Agility, 26, 1.2f, ShotTags, range: 22f, radius: 0.45f, speed: 40f),
                Mod("galeheart-wind", "Galeheart: wind", "projectile", damagePercent: 15),
                Mod("galeheart-volley-spread", "Galeheart: spread", "projectile", extraProjectiles: 1)),

            // Wizard
            Weapon("oak-staff", "Oak Staff", "wizard", Rarity.Rare, BoltTags, S((Stat.Intelligence, 3)),
                Hit("oak-bolt", "Magic Bolt", SkillEffectKind.Projectile, Stat.Intelligence, 13, 0.55f, BoltTags, range: 12f, speed: 17f),
                Hit("oak-blast", "Arcane Blast", SkillEffectKind.Area, Stat.Intelligence, 26, 1.6f, BlastTags, radius: 3.4f)),
            Weapon("starfall-staff", "Starfall Staff", "wizard", Rarity.Legendary, new[] { "magic", "fire" }, S((Stat.Intelligence, 5), (Stat.Sense, 4)),
                Hit("starfall-bolt", "Star Bolt", SkillEffectKind.Projectile, Stat.Intelligence, 15, 0.5f, new[] { "projectile", "magic", "fire" }, range: 13f, speed: 18f),
                Hit("starfall-blast", "Starfall", SkillEffectKind.Area, Stat.Intelligence, 34, 1.6f, new[] { "area", "magic", "fire" }, radius: 3.8f, windup: 0.2f),
                Mod("starfall-power", "Starfall Staff: power", "magic", damagePercent: 15),
                Mod("starfall-flare", "Starfall Staff: flare", "fire", radiusAdd: 0.75f)),

            // Monk
            Weapon("iron-knuckles", "Iron Knuckles", "monk", Rarity.Rare, MeleeTags, S((Stat.Agility, 2), (Stat.Vitality, 2)),
                Hit("knuckles-jab", "Jab", SkillEffectKind.Cone, Stat.Agility, 11, 0.25f, MeleeTags, range: 2.3f),
                Hit("knuckles-hook", "Hook", SkillEffectKind.Cone, Stat.Agility, 28, 0.7f, MeleeTags, range: 2.8f, arc: 90f)),
            Weapon("dragonscale-wraps", "Dragonscale Wraps", "monk", Rarity.Legendary, MeleeTags, S((Stat.Agility, 5), (Stat.Vitality, 4)),
                Hit("dragonscale-jab", "Dragon Jab", SkillEffectKind.Cone, Stat.Agility, 13, 0.22f, MeleeTags, range: 2.4f),
                Hit("dragonscale-palm", "Dragon Palm", SkillEffectKind.Area, Stat.Agility, 32, 0.8f, new[] { "melee", "physical", "area" }, radius: 2.8f),
                Mod("dragonscale-fury", "Dragonscale Wraps: fury", "melee", damagePercent: 15),
                Mod("dragonscale-pace", "Dragonscale Wraps: pace", "melee", cooldownPercent: -10)),

            // Cleric
            Weapon("silver-sceptre", "Silver Sceptre", "cleric", Rarity.Rare, BoltTags, S((Stat.Sense, 3)),
                Hit("silver-bolt", "Holy Bolt", SkillEffectKind.Projectile, Stat.Intelligence, 12, 0.55f, BoltTags, range: 12f, speed: 17f),
                Hit("silver-radiance", "Radiance", SkillEffectKind.Area, Stat.Intelligence, 24, 1.6f, BlastTags, radius: 3.4f)),
            Weapon("dawnbringer", "Dawnbringer", "cleric", Rarity.Legendary, BoltTags, S((Stat.Sense, 5), (Stat.Intelligence, 4)),
                Hit("dawnbringer-bolt", "Dawn Bolt", SkillEffectKind.Projectile, Stat.Intelligence, 14, 0.5f, BoltTags, range: 13f, speed: 18f),
                Hit("dawnbringer-radiance", "Sunburst", SkillEffectKind.Area, Stat.Intelligence, 30, 1.5f, BlastTags, radius: 3.8f),
                Mod("dawnbringer-light", "Dawnbringer: light", "magic", damagePercent: 15),
                Mod("dawnbringer-halo", "Dawnbringer: halo", "area", radiusAdd: 0.75f)),

            // Warlock
            Weapon("bone-rod", "Bone Rod", "warlock", Rarity.Rare, BoltTags, S((Stat.Intelligence, 3)),
                Hit("bone-bolt", "Hex Bolt", SkillEffectKind.Projectile, Stat.Intelligence, 13, 0.55f, BoltTags, range: 12f, speed: 17f),
                Hit("bone-burst", "Dark Burst", SkillEffectKind.Area, Stat.Intelligence, 26, 1.6f, BlastTags, radius: 3.4f)),
            Weapon("voidcaller", "Voidcaller", "warlock", Rarity.Legendary, BoltTags, S((Stat.Intelligence, 5), (Stat.Luck, 4)),
                Hit("voidcaller-bolt", "Void Bolt", SkillEffectKind.Projectile, Stat.Intelligence, 15, 0.5f, BoltTags, range: 13f, speed: 18f, pierces: true),
                Hit("voidcaller-burst", "Void Burst", SkillEffectKind.Area, Stat.Intelligence, 34, 1.6f, BlastTags, radius: 3.6f),
                Mod("voidcaller-hunger", "Voidcaller: hunger", "magic", damagePercent: 15),
                Mod("voidcaller-haste", "Voidcaller: haste", "magic", cooldownPercent: -10)),

            // Summoner
            Weapon("spirit-staff", "Spirit Staff", "summoner", Rarity.Rare, new[] { "magic", "summon" }, S((Stat.Intelligence, 2), (Stat.Sense, 2)),
                Hit("spirit-bolt", "Spirit Bolt", SkillEffectKind.Projectile, Stat.Intelligence, 13, 0.55f, BoltTags, range: 12f, speed: 17f),
                Hit("spirit-burst", "Spirit Burst", SkillEffectKind.Area, Stat.Intelligence, 26, 1.6f, BlastTags, radius: 3.4f)),
            Weapon("soulbinder", "Soulbinder", "summoner", Rarity.Legendary, new[] { "magic", "summon" }, S((Stat.Intelligence, 5), (Stat.Sense, 4)),
                Hit("soulbinder-bolt", "Soul Bolt", SkillEffectKind.Projectile, Stat.Intelligence, 14, 0.5f, BoltTags, range: 13f, speed: 18f),
                Hit("soulbinder-burst", "Soul Burst", SkillEffectKind.Area, Stat.Intelligence, 30, 1.5f, BlastTags, radius: 3.6f),
                Mod("soulbinder-pact", "Soulbinder: pact", "summon", damagePercent: 20),
                Mod("soulbinder-call", "Soulbinder: quick call", "summon", cooldownPercent: -10)),
        };

        /// <summary>
        /// Makes every ticket 61 piece that is missing, puts each new one in its gates' drop lists, and
        /// returns the whole gear list (existing pieces first) for the game's content.
        /// </summary>
        private static GearDefinitionAsset[] GetOrCreateMoreGear(IReadOnlyList<GearDefinitionAsset> current, GateDefinitionAsset thornwood, GateDefinitionAsset raid,
            IReadOnlyDictionary<string, GateDefinitionAsset[]> trials)
        {
            var made = new List<GearDefinitionAsset>();
            foreach (var data in SetData.Concat(WeaponData))
            {
                var created = false;
                var asset = GetOrCreate<GearDefinitionAsset>("Gear", data.Piece.Name, a =>
                {
                    data.Piece.Description = DescribeGear(data);
                    a.Set(data.Piece, data.Modifiers, data.Light, data.Heavy);
                    created = true;
                });
                made.Add(asset);
                if (!created) continue; // the Inspector's drop lists stand once a piece exists

                var gates = new List<GateDefinitionAsset>();
                if (data.DropsFrom == "thornwood") gates.Add(thornwood);
                else if (data.DropsFrom == "raid") gates.Add(raid);
                else if (data.DropsFrom.StartsWith("trial-"))
                {
                    var classId = data.DropsFrom.Substring(data.DropsFrom.IndexOf(':') + 1);
                    if (trials.TryGetValue(classId, out var own)) gates.Add(data.DropsFrom.StartsWith("trial-d") ? own[0] : own[1]);
                    if (data.Piece.Rarity == Rarity.Legendary) gates.Add(raid);
                }
                foreach (var gate in gates.Where(g => g != null))
                {
                    gate.AddToGearPool(new[] { asset });
                    EditorUtility.SetDirty(gate);
                }
            }

            var everyGear = AssetDatabase.FindAssets("t:GearDefinitionAsset").Select(g => AssetDatabase.LoadAssetAtPath<GearDefinitionAsset>(AssetDatabase.GUIDToAssetPath(g)));
            return current.Where(g => g != null).Concat(made).Concat(everyGear).Where(g => g != null).Distinct().ToArray();
        }

        /// <summary>A plain description from the piece's own numbers, so the words always match what it does.</summary>
        private static string DescribeGear(GearData data)
        {
            var parts = new List<string>();
            if (data.Piece.ClassIds.Length > 0) parts.Add($"For the {Capitalise(data.Piece.ClassIds[0])} only.");
            if (data.Light != null && data.Heavy != null) parts.Add($"Light attack: {data.Light.displayName}; heavy attack: {data.Heavy.displayName}.");
            foreach (var m in data.Modifiers)
            {
                var what = $"{m.tag} skills";
                if (m.damagePercent != 0) parts.Add($"{Capitalise(what)} deal {m.damagePercent}% more damage.");
                if (m.cooldownPercent != 0) parts.Add($"{Capitalise(what)} recover {-m.cooldownPercent}% faster.");
                if (m.radiusAdd != 0) parts.Add($"{Capitalise(what)} reach {m.radiusAdd:0.##} m wider.");
                if (m.extraProjectiles != 0) parts.Add($"{Capitalise(what)} fire {m.extraProjectiles} more.");
            }
            return parts.Count > 0 ? string.Join(" ", parts) : $"{data.Piece.Rarity} {data.Piece.Slot.ToString().ToLowerInvariant()} gear.";
        }

        private static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
