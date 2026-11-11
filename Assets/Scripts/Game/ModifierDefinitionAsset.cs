using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>Authoring asset for one modifier. The simulation only sees the plain <see cref="ModifierDefinition"/>.</summary>
    [CreateAssetMenu(menuName = "Hunter System/Modifier Definition", fileName = "NewModifier")]
    public sealed class ModifierDefinitionAsset : ScriptableObject
    {
        private enum TargetKind
        {
            Tag,
            Skill,
        }

        [SerializeField, Tooltip("Stable, unique id")] private string id = "";
        [SerializeField] private string displayName = "";
        [SerializeField, TextArea] private string description = "";

        [Header("Applies to")]
        [SerializeField, Tooltip("Tag: every skill with the tag. Skill: one signature skill (hand-authored).")] private TargetKind appliesTo = TargetKind.Tag;
        [SerializeField, Tooltip("A known tag: melee, physical, area, fire, projectile, magic, summon; or a skill id")] private string target = "";

        [Header("Numbers")]
        [SerializeField, Tooltip("+50 = 50% more base damage")] private int damagePercent;
        [SerializeField, Tooltip("-25 = 25% shorter cooldown")] private int cooldownPercent;
        [SerializeField, Tooltip("Metres added to cone reach and projectile range")] private float rangeAdd;
        [SerializeField, Tooltip("Metres added to area radius and projectile hit radius")] private float radiusAdd;

        [Header("Behaviour")]
        [SerializeField, Min(0), Tooltip("Projectile skills fire this many more bolts")] private int extraProjectiles;
        [SerializeField, Min(0f)] private float projectileSpreadDegrees = 15f;
        [SerializeField, Min(0f), Tooltip("Above 0: every hit bursts onto other enemies this close")] private float explosionRadius;
        [SerializeField, Min(0), Tooltip("Burst damage as a percentage of the hit")] private int explosionDamagePercent;

        public string Id => id;
        public string DisplayName => displayName;

        public ModifierDefinition ToDefinition() => new ModifierDefinition
        {
            Id = id,
            Name = displayName,
            Description = description,
            AppliesToTag = appliesTo == TargetKind.Tag ? target : null,
            AppliesToSkillId = appliesTo == TargetKind.Skill ? target : null,
            DamagePercent = damagePercent,
            CooldownPercent = cooldownPercent,
            RangeAdd = rangeAdd,
            RadiusAdd = radiusAdd,
            ExtraProjectiles = extraProjectiles,
            ProjectileSpreadDegrees = projectileSpreadDegrees,
            ExplosionRadius = explosionRadius,
            ExplosionDamagePercent = explosionDamagePercent,
        };

        /// <summary>For editor tooling that creates modifiers in code.</summary>
        public void CopyFrom(ModifierDefinition d)
        {
            id = d.Id;
            displayName = d.Name;
            description = d.Description;
            appliesTo = d.AppliesToSkillId != null ? TargetKind.Skill : TargetKind.Tag;
            target = d.AppliesToSkillId ?? d.AppliesToTag ?? "";
            damagePercent = d.DamagePercent;
            cooldownPercent = d.CooldownPercent;
            rangeAdd = d.RangeAdd;
            radiusAdd = d.RadiusAdd;
            extraProjectiles = d.ExtraProjectiles;
            projectileSpreadDegrees = d.ProjectileSpreadDegrees;
            explosionRadius = d.ExplosionRadius;
            explosionDamagePercent = d.ExplosionDamagePercent;
        }
    }
}
