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
        [SerializeField, Tooltip("The class that learns it; empty = every class")] private ClassDefinitionAsset characterClass;
        [SerializeField, Tooltip("A specialty id from the class's specialties (ticket 26): only hunters with that specialty learn it. Empty = the whole class")] private string specialty = "";
        [SerializeField, Tooltip("Free-form tags for modifiers, e.g. fire, projectile, melee, area")] private string[] tags = new string[0];
        [SerializeField] private Stat scalingStat = Stat.Strength;

        [Header("Effect")]
        [SerializeField] private SkillEffectKind effect = SkillEffectKind.Cone;
        [SerializeField, Min(0)] private int damage = 20;
        [SerializeField, Min(0f), Tooltip("Cone reach, or projectile flight distance, in metres")] private float range = 3f;
        [SerializeField, Range(1f, 360f), Tooltip("Cone width")] private float arcDegrees = 120f;
        [SerializeField, Min(0f), Tooltip("Area radius, or projectile hit radius, in metres")] private float radius = 3f;
        [SerializeField, Min(0f), Tooltip("Metres per second")] private float projectileSpeed = 15f;
        [SerializeField, Tooltip("A projectile that flies on through enemies, hitting each one once")] private bool pierces;
        [SerializeField, Min(0), Tooltip("Health restored instead of damage (ticket 56): a bolt heals the first ally it reaches, an area every player in it. Set damage to 0")] private int healing;
        [SerializeField, Min(0), Tooltip("Drain life (ticket 58): the caster heals this % of the health the skill's direct hits remove")] private int lifeStealPercent;

        [Header("Summon (effect Summon, ticket 57): damage is each hit's")]
        [SerializeField, Min(1), Tooltip("A summon's health, raised by Sense")] private int summonHealth = 60;
        [SerializeField, Min(0.1f), Tooltip("Seconds it lasts, if nothing fells it first")] private float summonLifetimeSeconds = 20f;
        [SerializeField, Min(0.1f), Tooltip("Metres it hits from")] private float summonAttackRange = 1.5f;
        [SerializeField, Min(0.05f), Tooltip("Seconds between its hits")] private float summonAttackSeconds = 1f;
        [SerializeField, Min(0f), Tooltip("Metres per second")] private float summonMoveSpeed = 4f;
        [SerializeField, Range(1, 3), Tooltip("How many one use calls (a summoner keeps at most three)")] private int summonCount = 1;

        [Header("Charge (hold the key)")]
        [SerializeField, Min(0f), Tooltip("Seconds of holding for a full charge; 0 = this skill cannot be charged")] private float chargeSeconds;
        [SerializeField, Min(0), Tooltip("At full charge: +100 doubles the radius, cone reach or projectile hit radius")] private int chargedSizePercent;
        [SerializeField, Min(0), Tooltip("At full charge: +100 doubles the damage")] private int chargedDamagePercent;
        [SerializeField, Min(1), Tooltip("Level at which the charge unlocks (never before the skill itself)")] private int chargeUnlockLevel = 1;

        public string Id => id;
        public string DisplayName => displayName;
        public ClassDefinitionAsset Class => characterClass;
        public string Specialty => string.IsNullOrWhiteSpace(specialty) ? null : specialty;

        public SkillDefinition ToDefinition() => new SkillDefinition
        {
            Id = id,
            Name = displayName,
            CooldownSeconds = cooldownSeconds,
            UnlockLevel = unlockLevel,
            ClassId = characterClass != null ? characterClass.Id : null,
            SpecialtyId = string.IsNullOrWhiteSpace(specialty) ? null : specialty,
            Tags = (string[])tags.Clone(),
            ScalingStat = scalingStat,
            Effect = effect,
            Damage = damage,
            Range = range,
            ArcDegrees = arcDegrees,
            Radius = radius,
            ProjectileSpeed = projectileSpeed,
            Pierces = pierces,
            Healing = healing,
            LifeStealPercent = lifeStealPercent,
            SummonHealth = summonHealth,
            SummonLifetimeSeconds = summonLifetimeSeconds,
            SummonAttackRange = summonAttackRange,
            SummonAttackSeconds = summonAttackSeconds,
            SummonMoveSpeed = summonMoveSpeed,
            SummonCount = summonCount,
            ChargeSeconds = chargeSeconds,
            ChargedSizePercent = chargedSizePercent,
            ChargedDamagePercent = chargedDamagePercent,
            ChargeUnlockLevel = chargeUnlockLevel,
        };

        /// <summary>For editor tooling: the class that learns this skill.</summary>
        public void SetClass(ClassDefinitionAsset value) => characterClass = value;

        /// <summary>For editor tooling: moving an existing skill when classes arrived (ticket 24).</summary>
        public void SetForClasses(string name, int unlock, Stat scaling, int baseDamage)
        {
            displayName = name;
            unlockLevel = unlock;
            scalingStat = scaling;
            damage = baseDamage;
        }

        /// <summary>For editor tooling that creates skills in code.</summary>
        public void CopyFrom(SkillDefinition d)
        {
            id = d.Id;
            displayName = d.Name;
            cooldownSeconds = d.CooldownSeconds;
            unlockLevel = d.UnlockLevel;
            specialty = d.SpecialtyId ?? "";
            tags = (string[])d.Tags.Clone();
            scalingStat = d.ScalingStat;
            effect = d.Effect;
            damage = d.Damage;
            range = d.Range;
            arcDegrees = d.ArcDegrees;
            radius = d.Radius;
            projectileSpeed = d.ProjectileSpeed;
            pierces = d.Pierces;
            healing = d.Healing;
            lifeStealPercent = d.LifeStealPercent;
            summonHealth = d.SummonHealth;
            summonLifetimeSeconds = d.SummonLifetimeSeconds;
            summonAttackRange = d.SummonAttackRange;
            summonAttackSeconds = d.SummonAttackSeconds;
            summonMoveSpeed = d.SummonMoveSpeed;
            summonCount = d.SummonCount;
            chargeSeconds = d.ChargeSeconds;
            chargedSizePercent = d.ChargedSizePercent;
            chargedDamagePercent = d.ChargedDamagePercent;
            chargeUnlockLevel = d.ChargeUnlockLevel;
        }
    }
}
