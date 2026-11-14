using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Authoring asset for one class (ticket 24). Skills name their class by pointing at one of
    /// these; the simulation only sees the plain <see cref="ClassDefinition"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunter System/Class Definition", fileName = "NewClass")]
    public sealed class ClassDefinitionAsset : ScriptableObject
    {
        [SerializeField, Tooltip("Stable, unique id. Saves refer to it; never rename it once players have characters.")] private string id = "";
        [SerializeField] private string displayName = "";
        [SerializeField, TextArea, Tooltip("Shown when a player picks a class")] private string description = "";

        [Header("Each level gained, on top of the free points")]
        [SerializeField, Min(0)] private int strength;
        [SerializeField, Min(0)] private int agility;
        [SerializeField, Min(0)] private int vitality;
        [SerializeField, Min(0)] private int intelligence;
        [SerializeField, Min(0)] private int sense;
        [SerializeField, Min(0)] private int luck;

        [Header("Movement")]
        [SerializeField, Min(10), Tooltip("Jump height, % of the base jump. A jump higher than an enemy clears it (the Warlord stands 3.5 m; the base jump is 0.9 m).")]
        private int jumpHeightPercent = 100;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;

        public ClassDefinition ToDefinition()
        {
            var stats = new int[PlayerStats.Count];
            stats[(int)Stat.Strength] = strength;
            stats[(int)Stat.Agility] = agility;
            stats[(int)Stat.Vitality] = vitality;
            stats[(int)Stat.Intelligence] = intelligence;
            stats[(int)Stat.Sense] = sense;
            stats[(int)Stat.Luck] = luck;
            return new ClassDefinition { Id = id, Name = displayName, Description = description, StatsPerLevel = stats, JumpHeightPercent = jumpHeightPercent };
        }

        /// <summary>For editor tooling that creates classes in code.</summary>
        public void CopyFrom(ClassDefinition d)
        {
            id = d.Id;
            displayName = d.Name;
            description = d.Description ?? "";
            strength = d.StatPerLevel((int)Stat.Strength);
            agility = d.StatPerLevel((int)Stat.Agility);
            vitality = d.StatPerLevel((int)Stat.Vitality);
            intelligence = d.StatPerLevel((int)Stat.Intelligence);
            sense = d.StatPerLevel((int)Stat.Sense);
            luck = d.StatPerLevel((int)Stat.Luck);
            jumpHeightPercent = d.JumpHeightPercent;
        }
    }
}
