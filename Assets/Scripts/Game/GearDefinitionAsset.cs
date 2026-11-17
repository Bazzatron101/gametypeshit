using System;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Authoring asset for one piece of gear (ticket 23). Its modifiers are written into the piece
    /// itself, not as separate modifier assets, so they never show up as gate rewards or arena
    /// toggles of their own. The simulation only sees the plain <see cref="GearDefinition"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunter System/Gear Definition", fileName = "NewGear")]
    public sealed class GearDefinitionAsset : ScriptableObject
    {
        /// <summary>One modifier the piece gives while worn. The same numbers as a modifier asset.</summary>
        [Serializable]
        public sealed class WornModifier
        {
            [Tooltip("Stable id, unique within the game")] public string id = "";
            public string displayName = "";
            [Tooltip("A known tag (every skill with it), or leave empty and set skill")] public string tag = "";
            [Tooltip("One signature skill's id, instead of a tag")] public string skill = "";
            [Tooltip("When set, counts once per worn piece with this tag, and not at all with none")] public string perWornTag = "";
            [Tooltip("+50 = 50% more base damage")] public int damagePercent;
            [Tooltip("-25 = 25% shorter cooldown")] public int cooldownPercent;
            public float rangeAdd;
            public float radiusAdd;
            [Min(0)] public int extraProjectiles;
            [Min(0f)] public float projectileSpreadDegrees = 15f;
            [Min(0f)] public float explosionRadius;
            [Min(0)] public int explosionDamagePercent;

            public ModifierDefinition ToDefinition() => new ModifierDefinition
            {
                Id = id,
                Name = displayName,
                AppliesToTag = string.IsNullOrEmpty(skill) ? tag : null,
                AppliesToSkillId = string.IsNullOrEmpty(skill) ? null : skill,
                PerWornTag = string.IsNullOrEmpty(perWornTag) ? null : perWornTag,
                DamagePercent = damagePercent,
                CooldownPercent = cooldownPercent,
                RangeAdd = rangeAdd,
                RadiusAdd = radiusAdd,
                ExtraProjectiles = extraProjectiles,
                ProjectileSpreadDegrees = projectileSpreadDegrees,
                ExplosionRadius = explosionRadius,
                ExplosionDamagePercent = explosionDamagePercent,
            };
        }

        /// <summary>One stat the piece raises or lowers while worn (ticket 51).</summary>
        [Serializable]
        public sealed class WornStatBonus
        {
            public Stat stat;
            [Tooltip("+5 = five more points while worn; negative takes points away (pair it with an upside)")] public int amount;
        }

        /// <summary>One of a weapon's attacks (ticket 46): a skill-shaped hit plus a windup. An empty id means none (the default attack).</summary>
        [Serializable]
        public sealed class Attack
        {
            [Tooltip("Stable id, unique among skills and weapon attacks; empty = keep the default attack")] public string id = "";
            public string displayName = "";
            public SkillEffectKind effect = SkillEffectKind.Cone;
            [Tooltip("Known tags, so gear and modifiers reach the attack")] public string[] tags = new string[0];
            public Stat scalingStat = Stat.Strength;
            [Min(0)] public int damage = 10;
            [Min(0f), Tooltip("Cone reach, or projectile flight distance, in metres")] public float range = 2.5f;
            [UnityEngine.Range(1f, 360f)] public float arcDegrees = 120f;
            [Min(0f), Tooltip("Area radius round the player, or projectile hit radius, in metres")] public float radius = 0.5f;
            [Min(0f), Tooltip("Metres per second")] public float projectileSpeed = 20f;
            [Tooltip("A projectile that flies on through enemies, hitting each once")] public bool pierces;
            [Min(0f)] public float cooldownSeconds = 0.5f;
            [Min(0f), Tooltip("Seconds from pressing to the hit; a dodge cancels it")] public float windupSeconds;

            public WeaponAttack ToDefinition()
            {
                if (string.IsNullOrEmpty(id)) return null;
                return new WeaponAttack
                {
                    WindupSeconds = windupSeconds,
                    Hit = new SkillDefinition
                    {
                        Id = id, Name = displayName, Effect = effect, Tags = (string[])(tags ?? new string[0]).Clone(), ScalingStat = scalingStat,
                        Damage = damage, Range = range, ArcDegrees = arcDegrees, Radius = radius, ProjectileSpeed = projectileSpeed,
                        Pierces = pierces, CooldownSeconds = cooldownSeconds,
                    },
                };
            }
        }

        [SerializeField, Tooltip("Stable id used in saves and over the network. Never change it once players have it.")] private string id = "";
        [SerializeField] private string displayName = "";
        [SerializeField, TextArea] private string description = "";
        [SerializeField] private GearSlot slot;
        [SerializeField, Tooltip("How rare the piece is (ticket 61): weighs the clear draw; a Legendary glows when worn")] private Rarity rarity;
        [SerializeField, Tooltip("Weapons only (ticket 61): the class ids that can wear it, e.g. warrior. Empty = any class.")] private string[] classIds = new string[0];
        [SerializeField, Tooltip("Known tags: melee, physical, area, fire, projectile, magic, summon")] private string[] tags = new string[0];
        [SerializeField] private WornModifier[] modifiers = new WornModifier[0];
        [SerializeField, Tooltip("Stats raised or lowered while worn (ticket 51); one entry per stat")] private WornStatBonus[] statBonuses = new WornStatBonus[0];
        [Header("Weapons only (ticket 46)")]
        [SerializeField, Tooltip("Replaces the light attack while worn")] private Attack lightAttack = new Attack();
        [SerializeField, Tooltip("Replaces the heavy attack while worn")] private Attack heavyAttack = new Attack();
        [SerializeField, Min(1), Tooltip("Gold for a duplicate at a store paying full value; each store pays its own share")] private int sellValue = 100;
        [Header("Look (ticket 34)")]
        [SerializeField, Tooltip("Drawn on the wearer, placed at its slot's anchor on the body (see GearLooks). Empty = a plain stand-in shape.")]
        private GameObject look;

        public string Id => id;
        public string DisplayName => displayName;
        public GearSlot Slot => slot;
        public Rarity Rarity => rarity;
        public System.Collections.Generic.IReadOnlyList<string> ClassIds => classIds ?? new string[0];

        /// <summary>What the piece looks like when worn (ticket 34), or null for the plain stand-in. Presentation only.</summary>
        public GameObject Look => look;

        /// <summary>
        /// The piece as the simulation sees it, made once per config: a gate's pool and the game's
        /// gear list must hold the same definition (the simulation checks this).
        /// </summary>
        /// <summary>For editor tooling that makes gear in code (ticket 61). Leaves the look alone.</summary>
        public void Set(GearDefinition data, WornModifier[] wornModifiers, Attack light, Attack heavy)
        {
            id = data.Id;
            displayName = data.Name;
            description = data.Description;
            slot = data.Slot;
            rarity = data.Rarity;
            classIds = (string[])(data.ClassIds ?? new string[0]).Clone();
            tags = (string[])(data.Tags ?? new string[0]).Clone();
            sellValue = data.SellValue;
            modifiers = wornModifiers ?? new WornModifier[0];
            statBonuses = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(data.StatBonuses, b => new WornStatBonus { stat = b.Stat, amount = b.Amount }));
            lightAttack = light ?? new Attack();
            heavyAttack = heavy ?? new Attack();
        }

        /// <summary>For editor tooling: sets only the rarity and class lock, keeping everything else (ticket 61).</summary>
        public void SetRarity(Rarity tier, params string[] classes)
        {
            rarity = tier;
            classIds = classes ?? new string[0];
        }

        public GearDefinition ToDefinition(System.Collections.Generic.Dictionary<GearDefinitionAsset, GearDefinition> made)
        {
            if (made != null && made.TryGetValue(this, out var known)) return known;
            var gear = new GearDefinition
            {
                Id = id, Name = displayName, Description = description, Slot = slot, Rarity = rarity,
                ClassIds = (string[])(classIds ?? new string[0]).Clone(),
                Tags = (string[])(tags ?? new string[0]).Clone(), SellValue = sellValue,
                LightAttack = lightAttack?.ToDefinition(), HeavyAttack = heavyAttack?.ToDefinition(),
            };
            foreach (var m in modifiers ?? new WornModifier[0])
                if (m != null) gear.Modifiers.Add(m.ToDefinition());
            foreach (var b in statBonuses ?? new WornStatBonus[0])
                if (b != null) gear.StatBonuses.Add(new StatBonus(b.stat, b.amount));
            if (made != null) made[this] = gear;
            return gear;
        }
    }
}
