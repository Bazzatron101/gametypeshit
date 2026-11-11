using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>Authoring asset for one skill. The simulation only sees the plain <see cref="SkillDefinition"/>.</summary>
    [CreateAssetMenu(menuName = "Hunter System/Skill Definition", fileName = "NewSkill")]
    public sealed class SkillDefinitionAsset : ScriptableObject
    {
        [SerializeField, Tooltip("Stable, unique id. Saves and modifiers refer to it; do not rename lightly.")] private string id = "";
        [SerializeField] private string displayName = "";
        [SerializeField, Min(0f)] private float cooldownSeconds = 5f;
        [SerializeField, Min(1), Tooltip("1 = a starter skill")] private int unlockLevel = 1;
        [SerializeField, Tooltip("Free-form tags for modifiers, e.g. fire, projectile, melee, area")] private string[] tags = new string[0];
        [SerializeField] private Stat scalingStat = Stat.Strength;

        [Header("Effect")]
        [SerializeField] private SkillEffectKind effect = SkillEffectKind.Cone;
        [SerializeField, Min(0)] private int damage = 20;
        [SerializeField, Min(0f), Tooltip("Cone reach, or projectile flight distance, in metres")] private float range = 3f;
        [SerializeField, Range(1f, 360f), Tooltip("Cone width")] private float arcDegrees = 120f;
        [SerializeField, Min(0f), Tooltip("Area radius, or projectile hit radius, in metres")] private float radius = 3f;
        [SerializeField, Min(0f), Tooltip("Metres per second")] private float projectileSpeed = 15f;

        public string Id => id;
        public string DisplayName => displayName;

        public SkillDefinition ToDefinition() => new SkillDefinition
        {
            Id = id,
            Name = displayName,
            CooldownSeconds = cooldownSeconds,
            UnlockLevel = unlockLevel,
            Tags = (string[])tags.Clone(),
            ScalingStat = scalingStat,
            Effect = effect,
            Damage = damage,
            Range = range,
            ArcDegrees = arcDegrees,
            Radius = radius,
            ProjectileSpeed = projectileSpeed,
        };

        /// <summary>For editor tooling that creates skills in code.</summary>
        public void CopyFrom(SkillDefinition d)
        {
            id = d.Id;
            displayName = d.Name;
            cooldownSeconds = d.CooldownSeconds;
            unlockLevel = d.UnlockLevel;
            tags = (string[])d.Tags.Clone();
            scalingStat = d.ScalingStat;
            effect = d.Effect;
            damage = d.Damage;
            range = d.Range;
            arcDegrees = d.ArcDegrees;
            radius = d.Radius;
            projectileSpeed = d.ProjectileSpeed;
        }
    }
}
