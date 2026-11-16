using System;
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
        [SerializeField, Tooltip("The weapon a character of this class is issued if it has none (ticket 46). Must be in the content's gear list.")] private GearDefinitionAsset startingWeapon;
        [SerializeField, Min(10), Tooltip("Jump height, % of the base jump. A jump higher than an enemy clears it (the Warlord stands 3.5 m; the base jump is 0.9 m).")]
        private int jumpHeightPercent = 100;

        [Header("Specialties (ticket 26)")]
        [SerializeField, Tooltip("The class's trials, one per tier: clearing one as a hunter of this class, at the gate's rank, lets them pick that tier's specialty. Each gate needs a rank (D, B)")]
        private GateDefinitionAsset[] trialGates = new GateDefinitionAsset[0];
        [SerializeField, Tooltip("The class's specialty tree: two at D, and two at B under each D one. Each pick is permanent. A specialty with no skills or passives yet is shown but cannot be picked")]
        private Specialty[] specialties = new Specialty[0];

        /// <summary>One branch of the class (ticket 26). Its skills are skill assets naming its id.</summary>
        [Serializable]
        public struct Specialty
        {
            [Tooltip("Stable, unique across all classes. Saves refer to it; never rename it once players have characters")] public string id;
            public string displayName;
            [Tooltip("The tier: the rank whose trial unlocks the pick (\"D\" or \"B\")")] public string rank;
            [Tooltip("At B: the id of the D specialty this one branches from. Empty at D")] public string parent;
            [TextArea, Tooltip("Shown when a hunter picks a specialty")] public string description;
            [Tooltip("Always in effect for a hunter with this specialty. Each must also be in the content's modifier list")] public ModifierDefinitionAsset[] passives;
        }

        public System.Collections.Generic.IReadOnlyList<GateDefinitionAsset> TrialGates => trialGates ?? new GateDefinitionAsset[0];
        public System.Collections.Generic.IReadOnlyList<Specialty> Specialties => specialties ?? new Specialty[0];

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
            var definition = new ClassDefinition { Id = id, Name = displayName, Description = description, StatsPerLevel = stats, JumpHeightPercent = jumpHeightPercent,
                StartingWeaponId = startingWeapon != null ? startingWeapon.Id : null };
            foreach (var gate in trialGates ?? new GateDefinitionAsset[0])
                if (gate != null) definition.TrialGateIds.Add(gate.Id);
            foreach (var s in specialties ?? new Specialty[0])
            {
                var specialty = new SpecialtyDefinition
                {
                    Id = s.id, Name = s.displayName, Description = s.description, ClassId = id,
                    Rank = string.IsNullOrWhiteSpace(s.rank) ? null : s.rank, ParentId = string.IsNullOrWhiteSpace(s.parent) ? null : s.parent,
                };
                foreach (var passive in s.passives ?? new ModifierDefinitionAsset[0])
                    if (passive != null) specialty.Passives.Add(passive.ToDefinition());
                definition.Specialties.Add(specialty);
            }
            return definition;
        }

        /// <summary>For editor tooling: the class's trials and its specialty tree (ticket 26).</summary>
        public void SetSpecialties(GateDefinitionAsset[] trials, Specialty[] tree)
        {
            trialGates = trials;
            specialties = tree;
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
