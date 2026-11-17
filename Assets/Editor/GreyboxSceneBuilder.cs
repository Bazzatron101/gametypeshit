using System.Collections.Generic;
using System.Linq;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using HunterSystem.Game;
using HunterSystem.Online;
using HunterSystem.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HunterSystem.Editor
{
    /// <summary>
    /// Builds the two greybox scenes from shared parts and keeps the game's content list current:
    /// <list type="bullet">
    /// <item>Assets/Scenes/Greybox.unity: the first fight. A player, three Goblins, the System HUD,
    /// skill visuals and the F5-F8 modifier toggles.</item>
    /// <item>Assets/Scenes/Arena.unity: the test arena. A player and the arena panel (F1) to spawn
    /// enemies and set level, loadout and modifiers.</item>
    /// </list>
    /// Data assets are created once and then left alone, so Inspector tuning survives a rebuild.
    /// GameContent lists the player tuning, progression, and every skill, modifier and enemy asset in
    /// the project; both scenes use it.
    /// Regenerate from the menu or with:
    /// Unity -batchmode -quit -projectPath . -executeMethod HunterSystem.Editor.GreyboxSceneBuilder.BuildAll
    /// </summary>
    public static partial class GreyboxSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Greybox.unity";
        public const string ArenaScenePath = "Assets/Scenes/Arena.unity";
        public const string CoopScenePath = "Assets/Scenes/Coop.unity";
        private const string MaterialsFolder = "Assets/Art/Materials";
        private const string DataFolder = "Assets/Data";
        private const string ProgressionPath = "Assets/Data/Progression.asset";
        private const string PlayerTuningPath = "Assets/Data/PlayerTuning.asset";
        private const string HubPath = "Assets/Data/Hub.asset";
        private const string PartyScalingPath = "Assets/Data/PartyScaling.asset";
        private const string ContentPath = "Assets/Data/GameContent.asset";
        private static readonly Color GoblinColour = new Color(0.8f, 0.2f, 0.2f);

        /// <summary>Demo modifiers, as created on a fresh project.</summary>
        private static readonly ModifierDefinition[] DemoModifierData =
        {
            new ModifierDefinition
            {
                Id = "searing", Name = "Searing", Description = "Fire skills deal 50% more damage.",
                AppliesToTag = SkillTags.Fire, DamagePercent = 50,
            },
            new ModifierDefinition
            {
                Id = "split-shot", Name = "Split Shot", Description = "Projectile skills fire three bolts in a fan.",
                AppliesToTag = SkillTags.Projectile, ExtraProjectiles = 2, ProjectileSpreadDegrees = 20f,
            },
            new ModifierDefinition
            {
                Id = "ember-core", Name = "Ember Core", Description = "Every fire hit bursts, burning nearby enemies for half the damage.",
                AppliesToTag = SkillTags.Fire, ExplosionRadius = 2.5f, ExplosionDamagePercent = 50,
            },
            new ModifierDefinition
            {
                Id = "deep-rend", Name = "Deep Rend", Description = "Rending Slash: 50% more damage, 25% shorter cooldown, 1 m longer reach.",
                AppliesToSkillId = "rending-slash", DamagePercent = 50, CooldownPercent = -25, RangeAdd = 1f,
            },
        };

        /// <summary>
        /// The four classes (ticket 24), in the order the picker shows them. Each level gives two
        /// points in the class's main stats on top of the free ones. Only the Rogue jumps high
        /// enough to clear an enemy: 450% of the 0.9 m base is 4.05 m, over the Warlord's 3.5 m.
        /// </summary>
        private static readonly ClassDefinition[] ClassData =
        {
            new ClassDefinition
            {
                Id = "warrior", Name = "Warrior", StatsPerLevel = ClassStats(str: 1, vit: 1),
                Description = "Front line. Heavy blades and the health to stand in the fight. +1 Strength, +1 Vitality each level.",
            },
            new ClassDefinition
            {
                Id = "rogue", Name = "Rogue", StatsPerLevel = ClassStats(agi: 1, luck: 1), JumpHeightPercent = 450,
                Description = "Quick knives up close, and a leap that clears an enemy's charge. +1 Agility, +1 Luck each level.",
            },
            new ClassDefinition
            {
                Id = "archer", Name = "Archer", StatsPerLevel = ClassStats(agi: 1, sense: 1),
                Description = "Keeps its distance: arrows, javelins and volleys. +1 Agility, +1 Sense each level.",
            },
            new ClassDefinition
            {
                Id = "wizard", Name = "Wizard", StatsPerLevel = ClassStats(intel: 1, sense: 1),
                Description = "Starts with one weak Fireball and grows into the strongest spells in the game. +1 Intelligence, +1 Sense each level.",
            },
            new ClassDefinition
            {
                // Ticket 55. Fights unarmed: quick palms and kicks up close, and a wave of force for reach.
                Id = "monk", Name = "Monk", StatsPerLevel = ClassStats(agi: 1, vit: 1), StartingWeaponId = "hand-wraps",
                Description = "Fights with bare hands: fast strikes, kicks, and a wave of force. +1 Agility, +1 Vitality each level.",
            },
            new ClassDefinition
            {
                // Ticket 56. Holy smites (Intelligence) and healing bolts that fly to an ally (Sense).
                Id = "cleric", Name = "Cleric", StatsPerLevel = ClassStats(intel: 1, sense: 1), StartingWeaponId = "acolyte-sceptre",
                Description = "A holy caster who keeps the party standing: smites that burn, and bolts of light that heal the ally they reach. +1 Intelligence, +1 Sense each level.",
            },
            new ClassDefinition
            {
                // Ticket 58. Dark spells that drain life: every hit heals the Warlock for part of it.
                Id = "warlock", Name = "Warlock", StatsPerLevel = ClassStats(intel: 1, luck: 1), StartingWeaponId = "hexed-rod",
                Description = "A dark caster who lives on what it takes: its spells heal it for part of the damage they deal. +1 Intelligence, +1 Luck each level.",
            },
            new ClassDefinition
            {
                // Ticket 57. Calls creatures that fight for it: their hits rise with Intelligence, their health with Sense.
                Id = "summoner", Name = "Summoner", StatsPerLevel = ClassStats(intel: 1, sense: 1), StartingWeaponId = "binding-staff",
                Description = "Calls creatures to fight for it, up to three at once, each for a while or until it falls. +1 Intelligence, +1 Sense each level.",
            },
        };

        private static int[] ClassStats(int str = 0, int agi = 0, int vit = 0, int intel = 0, int sense = 0, int luck = 0)
        {
            var stats = new int[PlayerStats.Count];
            stats[(int)Stat.Strength] = str;
            stats[(int)Stat.Agility] = agi;
            stats[(int)Stat.Vitality] = vit;
            stats[(int)Stat.Intelligence] = intel;
            stats[(int)Stat.Sense] = sense;
            stats[(int)Stat.Luck] = luck;
            return stats;
        }

        /// <summary>
        /// Every skill as created on a fresh project, in bar order (starters first, then by unlock level).
        /// After that the assets are the source of truth: rebuilding never overwrites them, except
        /// once to move a skill made before classes onto its class (see <see cref="GetOrCreateSkill"/>).
        /// </summary>
        private static readonly SkillDefinition[] SkillData =
        {
            // Warrior
            new SkillDefinition
            {
                Id = "rending-slash", Name = "Rending Slash", ClassId = "warrior", Effect = SkillEffectKind.Cone, Damage = 30, Range = 3f, ArcDegrees = 160f,
                CooldownSeconds = 4f, UnlockLevel = 1, ScalingStat = Stat.Strength, Tags = new[] { "melee", "physical" },
                ChargeSeconds = 0.8f, ChargedDamagePercent = 100, ChargeUnlockLevel = 4,
            },
            new SkillDefinition
            {
                Id = "ground-slam", Name = "Ground Slam", ClassId = "warrior", Effect = SkillEffectKind.Area, Damage = 20, Radius = 3.5f,
                CooldownSeconds = 6f, UnlockLevel = 1, ScalingStat = Stat.Strength, Tags = new[] { "melee", "area", "physical" },
                ChargeSeconds = 1f, ChargedSizePercent = 100, ChargeUnlockLevel = 6,
            },
            // Rogue
            new SkillDefinition
            {
                Id = "twin-fangs", Name = "Twin Fangs", ClassId = "rogue", Effect = SkillEffectKind.Cone, Damage = 22, Range = 2.2f, ArcDegrees = 90f,
                CooldownSeconds = 2.5f, UnlockLevel = 1, ScalingStat = Stat.Agility, Tags = new[] { "melee", "physical" },
                ChargeSeconds = 0.6f, ChargedDamagePercent = 80, ChargeUnlockLevel = 4,
            },
            // Archer
            new SkillDefinition
            {
                Id = "quick-shot", Name = "Quick Shot", ClassId = "archer", Effect = SkillEffectKind.Projectile, Damage = 16, Range = 14f, Radius = 0.4f,
                ProjectileSpeed = 24f, CooldownSeconds = 1.5f, UnlockLevel = 1, ScalingStat = Stat.Agility, Tags = new[] { "projectile", "physical" },
            },
            new SkillDefinition
            {
                // Before classes this was a level-5 Strength skill for everyone; it is the Archer's heavy shot now.
                Id = "hunters-javelin", Name = "Hunter's Javelin", ClassId = "archer", Effect = SkillEffectKind.Projectile, Damage = 30, Range = 16f, Radius = 0.6f,
                ProjectileSpeed = 22f, CooldownSeconds = 5f, UnlockLevel = 1, ScalingStat = Stat.Agility,
                Tags = new[] { "projectile", "physical" },
                ChargeSeconds = 1f, ChargedDamagePercent = 100, ChargeUnlockLevel = 8,
            },
            // Wizard
            new SkillDefinition
            {
                // The wizard's only starter, and weak: magic grows with the wizard (it was Fire Bolt before classes).
                Id = "fire-bolt", Name = "Fireball", ClassId = "wizard", Effect = SkillEffectKind.Projectile, Damage = 18, Range = 12f, Radius = 0.5f,
                ProjectileSpeed = 15f, CooldownSeconds = 2f, UnlockLevel = 1, ScalingStat = Stat.Intelligence,
                Tags = new[] { "fire", "projectile", "magic" },
            },
            // Unlocks, by level
            new SkillDefinition
            {
                Id = "shadow-flurry", Name = "Shadow Flurry", ClassId = "rogue", Effect = SkillEffectKind.Area, Damage = 28, Radius = 2.5f,
                CooldownSeconds = 7f, UnlockLevel = 3, ScalingStat = Stat.Agility, Tags = new[] { "melee", "area", "physical" },
            },
            new SkillDefinition
            {
                Id = "arrow-rain", Name = "Arrow Rain", ClassId = "archer", Effect = SkillEffectKind.Area, Damage = 26, Radius = 4f,
                CooldownSeconds = 8f, UnlockLevel = 3, ScalingStat = Stat.Agility, Tags = new[] { "area", "physical" },
                ChargeSeconds = 1f, ChargedSizePercent = 60, ChargeUnlockLevel = 7,
            },
            new SkillDefinition
            {
                Id = "flame-burst", Name = "Flame Burst", ClassId = "wizard", Effect = SkillEffectKind.Area, Damage = 35, Radius = 4f,
                CooldownSeconds = 8f, UnlockLevel = 3, ScalingStat = Stat.Intelligence, Tags = new[] { "fire", "area", "magic" },
                ChargeSeconds = 1f, ChargedSizePercent = 60, ChargeUnlockLevel = 7,
            },
            new SkillDefinition
            {
                Id = "shield-bash", Name = "Shield Bash", ClassId = "warrior", Effect = SkillEffectKind.Cone, Damage = 35, Range = 2f, ArcDegrees = 90f,
                CooldownSeconds = 6f, UnlockLevel = 5, ScalingStat = Stat.Strength, Tags = new[] { "melee", "physical" },
            },
            new SkillDefinition
            {
                Id = "throwing-knives", Name = "Throwing Knives", ClassId = "rogue", Effect = SkillEffectKind.Projectile, Damage = 25, Range = 10f, Radius = 0.4f,
                ProjectileSpeed = 20f, CooldownSeconds = 4f, UnlockLevel = 5, ScalingStat = Stat.Agility, Tags = new[] { "projectile", "physical" },
            },
            new SkillDefinition
            {
                Id = "arcane-orb", Name = "Arcane Orb", ClassId = "wizard", Effect = SkillEffectKind.Projectile, Damage = 45, Range = 12f, Radius = 1.2f,
                ProjectileSpeed = 9f, CooldownSeconds = 6f, UnlockLevel = 8, ScalingStat = Stat.Intelligence, Tags = new[] { "projectile", "magic" },
                ChargeSeconds = 1f, ChargedDamagePercent = 80, ChargeUnlockLevel = 11,
            },
            new SkillDefinition
            {
                Id = "whirlwind", Name = "Whirlwind", ClassId = "warrior", Effect = SkillEffectKind.Area, Damage = 30, Radius = 3.2f,
                CooldownSeconds = 7f, UnlockLevel = 10, ScalingStat = Stat.Strength, Tags = new[] { "melee", "area", "physical" },
                ChargeSeconds = 1.2f, ChargedSizePercent = 80, ChargeUnlockLevel = 12,
            },
            new SkillDefinition
            {
                Id = "eviscerate", Name = "Eviscerate", ClassId = "rogue", Effect = SkillEffectKind.Cone, Damage = 60, Range = 2.5f, ArcDegrees = 60f,
                CooldownSeconds = 9f, UnlockLevel = 10, ScalingStat = Stat.Agility, Tags = new[] { "melee", "physical" },
                ChargeSeconds = 0.8f, ChargedDamagePercent = 100, ChargeUnlockLevel = 13,
            },
            new SkillDefinition
            {
                Id = "volley", Name = "Volley", ClassId = "archer", Effect = SkillEffectKind.Cone, Damage = 40, Range = 10f, ArcDegrees = 40f,
                CooldownSeconds = 9f, UnlockLevel = 10, ScalingStat = Stat.Agility, Tags = new[] { "projectile", "physical" },
                ChargeSeconds = 1f, ChargedSizePercent = 60, ChargeUnlockLevel = 13,
            },
            new SkillDefinition
            {
                Id = "inferno-wave", Name = "Inferno Wave", ClassId = "wizard", Effect = SkillEffectKind.Cone, Damage = 45, Range = 6f, ArcDegrees = 70f,
                CooldownSeconds = 10f, UnlockLevel = 15, ScalingStat = Stat.Intelligence, Tags = new[] { "fire", "magic" },
                ChargeSeconds = 1f, ChargedSizePercent = 60, ChargeUnlockLevel = 18,
            },
            new SkillDefinition
            {
                // The capstone: no charge, it is already enormous.
                Id = "monarchs-decree", Name = "Monarch's Decree", ClassId = "wizard", Effect = SkillEffectKind.Area, Damage = 80, Radius = 6f,
                CooldownSeconds = 20f, UnlockLevel = 20, ScalingStat = Stat.Intelligence, Tags = new[] { "magic", "area" },
            },

            // Monk (ticket 55): two starters, then a skill at 3, 5, 10 and 15.
            new SkillDefinition
            {
                Id = "palm-strike", Name = "Palm Strike", ClassId = "monk", Effect = SkillEffectKind.Cone, Damage = 24, Range = 2.5f, ArcDegrees = 100f,
                CooldownSeconds = 3f, ScalingStat = Stat.Agility, Tags = new[] { "melee", "physical" },
            },
            new SkillDefinition
            {
                Id = "rising-kick", Name = "Rising Kick", ClassId = "monk", Effect = SkillEffectKind.Cone, Damage = 32, Range = 3f, ArcDegrees = 70f,
                CooldownSeconds = 5f, ScalingStat = Stat.Agility, Tags = new[] { "melee", "physical" },
            },
            new SkillDefinition
            {
                Id = "flurry-of-fists", Name = "Flurry of Fists", ClassId = "monk", Effect = SkillEffectKind.Area, Damage = 26, Radius = 2.5f,
                CooldownSeconds = 6f, UnlockLevel = 3, ScalingStat = Stat.Agility, Tags = new[] { "melee", "physical", "area" },
            },
            new SkillDefinition
            {
                // Reach for a fighter with none: a wave of force, not magic.
                Id = "force-wave", Name = "Force Wave", ClassId = "monk", Effect = SkillEffectKind.Projectile, Damage = 28, Range = 10f, Radius = 0.7f, ProjectileSpeed = 16f,
                CooldownSeconds = 6f, UnlockLevel = 5, ScalingStat = Stat.Agility, Tags = new[] { "projectile", "physical" },
            },
            new SkillDefinition
            {
                Id = "hundred-palms", Name = "Hundred Palms", ClassId = "monk", Effect = SkillEffectKind.Cone, Damage = 55, Range = 3f, ArcDegrees = 120f,
                CooldownSeconds = 9f, UnlockLevel = 10, ScalingStat = Stat.Agility, Tags = new[] { "melee", "physical" },
                ChargeSeconds = 1f, ChargedDamagePercent = 80, ChargeUnlockLevel = 12,
            },
            new SkillDefinition
            {
                Id = "dragons-descent", Name = "Dragon's Descent", ClassId = "monk", Effect = SkillEffectKind.Area, Damage = 60, Radius = 4f,
                CooldownSeconds = 12f, UnlockLevel = 15, ScalingStat = Stat.Agility, Tags = new[] { "melee", "physical", "area" },
            },

            // Cleric (ticket 56): smites scale with Intelligence, heals with Sense. Mending Light flies to an ally; Sanctuary heals everyone near, the Cleric too.
            new SkillDefinition
            {
                Id = "smite", Name = "Smite", ClassId = "cleric", Effect = SkillEffectKind.Projectile, Damage = 20, Range = 12f, Radius = 0.5f, ProjectileSpeed = 16f,
                CooldownSeconds = 2.5f, ScalingStat = Stat.Intelligence, Tags = new[] { "magic", "projectile" },
            },
            new SkillDefinition
            {
                Id = "mending-light", Name = "Mending Light", ClassId = "cleric", Effect = SkillEffectKind.Projectile, Damage = 0, Healing = 30, Range = 14f, Radius = 0.8f, ProjectileSpeed = 18f,
                CooldownSeconds = 6f, ScalingStat = Stat.Sense, Tags = new[] { "magic", "projectile" },
            },
            new SkillDefinition
            {
                Id = "holy-nova", Name = "Holy Nova", ClassId = "cleric", Effect = SkillEffectKind.Area, Damage = 30, Radius = 3.5f,
                CooldownSeconds = 7f, UnlockLevel = 3, ScalingStat = Stat.Intelligence, Tags = new[] { "magic", "area" },
            },
            new SkillDefinition
            {
                Id = "sanctuary", Name = "Sanctuary", ClassId = "cleric", Effect = SkillEffectKind.Area, Damage = 0, Healing = 28, Radius = 5f,
                CooldownSeconds = 14f, UnlockLevel = 5, ScalingStat = Stat.Sense, Tags = new[] { "magic", "area" },
            },
            new SkillDefinition
            {
                Id = "judgement", Name = "Judgement", ClassId = "cleric", Effect = SkillEffectKind.Cone, Damage = 50, Range = 6f, ArcDegrees = 50f,
                CooldownSeconds = 9f, UnlockLevel = 10, ScalingStat = Stat.Intelligence, Tags = new[] { "magic" },
                ChargeSeconds = 1f, ChargedDamagePercent = 80, ChargeUnlockLevel = 12,
            },
            new SkillDefinition
            {
                Id = "radiant-lance", Name = "Radiant Lance", ClassId = "cleric", Effect = SkillEffectKind.Projectile, Damage = 55, Range = 16f, Radius = 0.6f, ProjectileSpeed = 20f, Pierces = true,
                CooldownSeconds = 10f, UnlockLevel = 15, ScalingStat = Stat.Intelligence, Tags = new[] { "magic", "projectile" },
            },

            // Warlock (ticket 58): every spell drains life, healing the Warlock for part of what it deals.
            new SkillDefinition
            {
                Id = "shadow-bolt", Name = "Shadow Bolt", ClassId = "warlock", Effect = SkillEffectKind.Projectile, Damage = 20, Range = 12f, Radius = 0.5f, ProjectileSpeed = 16f,
                CooldownSeconds = 2.5f, ScalingStat = Stat.Intelligence, LifeStealPercent = 20, Tags = new[] { "magic", "projectile" },
            },
            new SkillDefinition
            {
                Id = "drain-touch", Name = "Drain Touch", ClassId = "warlock", Effect = SkillEffectKind.Cone, Damage = 22, Range = 2.5f, ArcDegrees = 90f,
                CooldownSeconds = 4f, ScalingStat = Stat.Intelligence, LifeStealPercent = 50, Tags = new[] { "magic" },
            },
            new SkillDefinition
            {
                Id = "hex-nova", Name = "Hex Nova", ClassId = "warlock", Effect = SkillEffectKind.Area, Damage = 28, Radius = 3f,
                CooldownSeconds = 7f, UnlockLevel = 3, ScalingStat = Stat.Intelligence, LifeStealPercent = 25, Tags = new[] { "magic", "area" },
            },
            new SkillDefinition
            {
                Id = "soul-siphon", Name = "Soul Siphon", ClassId = "warlock", Effect = SkillEffectKind.Projectile, Damage = 32, Range = 12f, Radius = 0.6f, ProjectileSpeed = 15f,
                CooldownSeconds = 6f, UnlockLevel = 5, ScalingStat = Stat.Intelligence, LifeStealPercent = 40, Tags = new[] { "magic", "projectile" },
            },
            new SkillDefinition
            {
                Id = "abyssal-wave", Name = "Abyssal Wave", ClassId = "warlock", Effect = SkillEffectKind.Cone, Damage = 48, Range = 6f, ArcDegrees = 70f,
                CooldownSeconds = 10f, UnlockLevel = 10, ScalingStat = Stat.Intelligence, LifeStealPercent = 15, Tags = new[] { "magic" },
                ChargeSeconds = 1f, ChargedSizePercent = 60, ChargeUnlockLevel = 12,
            },
            new SkillDefinition
            {
                Id = "void-rupture", Name = "Void Rupture", ClassId = "warlock", Effect = SkillEffectKind.Area, Damage = 65, Radius = 5f,
                CooldownSeconds = 14f, UnlockLevel = 15, ScalingStat = Stat.Intelligence, LifeStealPercent = 20, Tags = new[] { "magic", "area" },
            },

            // Summoner (ticket 57): summons hit with Intelligence and stand up with Sense; a summoner keeps at most three.
            new SkillDefinition
            {
                Id = "call-imp", Name = "Call Imp", ClassId = "summoner", Effect = SkillEffectKind.Summon, Damage = 9, SummonHealth = 60, SummonLifetimeSeconds = 20f,
                SummonAttackRange = 1.5f, SummonAttackSeconds = 0.9f, SummonMoveSpeed = 4.5f, SummonCount = 1,
                CooldownSeconds = 8f, UnlockLevel = 1, ScalingStat = Stat.Intelligence, Tags = new[] { "summon" },
            },
            new SkillDefinition
            {
                Id = "spirit-lash", Name = "Spirit Lash", ClassId = "summoner", Effect = SkillEffectKind.Projectile, Damage = 16, Range = 11f, Radius = 0.5f, ProjectileSpeed = 16f,
                CooldownSeconds = 2.5f, ScalingStat = Stat.Intelligence, Tags = new[] { "summon", "projectile" },
            },
            new SkillDefinition
            {
                Id = "call-wolves", Name = "Call Wolves", ClassId = "summoner", Effect = SkillEffectKind.Summon, Damage = 7, SummonHealth = 45, SummonLifetimeSeconds = 15f,
                SummonAttackRange = 1.5f, SummonAttackSeconds = 0.8f, SummonMoveSpeed = 6f, SummonCount = 2,
                CooldownSeconds = 12f, UnlockLevel = 3, ScalingStat = Stat.Intelligence, Tags = new[] { "summon" },
            },
            new SkillDefinition
            {
                Id = "soul-burst", Name = "Soul Burst", ClassId = "summoner", Effect = SkillEffectKind.Area, Damage = 28, Radius = 3.5f,
                CooldownSeconds = 7f, UnlockLevel = 5, ScalingStat = Stat.Intelligence, Tags = new[] { "summon", "area" },
            },
            new SkillDefinition
            {
                Id = "call-golem", Name = "Call Golem", ClassId = "summoner", Effect = SkillEffectKind.Summon, Damage = 22, SummonHealth = 250, SummonLifetimeSeconds = 25f,
                SummonAttackRange = 2f, SummonAttackSeconds = 1.6f, SummonMoveSpeed = 3f, SummonCount = 1,
                CooldownSeconds = 22f, UnlockLevel = 10, ScalingStat = Stat.Intelligence, Tags = new[] { "summon" },
            },
            new SkillDefinition
            {
                Id = "call-wraith", Name = "Call Wraith", ClassId = "summoner", Effect = SkillEffectKind.Summon, Damage = 26, SummonHealth = 110, SummonLifetimeSeconds = 20f,
                SummonAttackRange = 6f, SummonAttackSeconds = 1.2f, SummonMoveSpeed = 5f, SummonCount = 1,
                CooldownSeconds = 16f, UnlockLevel = 15, ScalingStat = Stat.Intelligence, Tags = new[] { "summon" },
            },

            // The Warrior's Vanguard specialty (ticket 26, D-rank tier): learned only by Vanguards, from the start of D-rank.
            new SkillDefinition
            {
                // A heavy two-handed blow straight ahead: the Vanguard leads the charge into the first enemy.
                Id = "overhead-smash", Name = "Overhead Smash", ClassId = "warrior", SpecialtyId = "vanguard", Effect = SkillEffectKind.Cone, Damage = 55, Range = 3f, ArcDegrees = 90f,
                CooldownSeconds = 6f, UnlockLevel = 16, ScalingStat = Stat.Strength, Tags = new[] { "melee", "physical" },
            },

            // The Warrior's Berserker specialty (ticket 26, B-rank tier, under the Vanguard): learned only by Berserkers, from the start of B-rank.
            new SkillDefinition
            {
                // A huge, short-cooldown swing: the Berserker stands in the crowd and keeps swinging.
                Id = "cleave", Name = "Cleave", ClassId = "warrior", SpecialtyId = "berserker", Effect = SkillEffectKind.Cone, Damage = 65, Range = 3.5f, ArcDegrees = 220f,
                CooldownSeconds = 4f, UnlockLevel = 56, ScalingStat = Stat.Strength, Tags = new[] { "melee", "physical", "area" },
            },
            new SkillDefinition
            {
                // Hold to wind up a quake that empties the room around the Berserker.
                Id = "earthsplitter", Name = "Earthsplitter", ClassId = "warrior", SpecialtyId = "berserker", Effect = SkillEffectKind.Area, Damage = 80, Radius = 4.5f,
                CooldownSeconds = 11f, UnlockLevel = 62, ScalingStat = Stat.Strength, Tags = new[] { "melee", "physical", "area" },
                ChargeSeconds = 1.2f, ChargedSizePercent = 60, ChargedDamagePercent = 80,
            },
        };

        /// <summary>
        /// The specialties' passives (ticket 26), as created on a fresh project. Always in effect for a hunter
        /// with the specialty; listed with the content's modifiers like any other.
        /// </summary>
        private static readonly ModifierDefinition[] SpecialtyModifierData =
        {
            new ModifierDefinition
            {
                Id = "vanguard-edge", Name = "Vanguard's Edge", Description = "Vanguard: physical skills deal 10% more damage.",
                AppliesToTag = SkillTags.Physical, DamagePercent = 10,
            },
            new ModifierDefinition
            {
                Id = "vanguard-advance", Name = "Advance", Description = "Vanguard: Shield Bash's cooldown is 30% shorter and it reaches 1 m further.",
                AppliesToSkillId = "shield-bash", CooldownPercent = -30, RangeAdd = 1f,
            },
            new ModifierDefinition
            {
                Id = "berserker-fury", Name = "Berserker's Fury", Description = "Berserker: melee skills deal 20% more damage.",
                AppliesToTag = SkillTags.Melee, DamagePercent = 20,
            },
            new ModifierDefinition
            {
                Id = "berserker-relentless", Name = "Relentless", Description = "Berserker: Whirlwind's cooldown is 35% shorter and it reaches 1 m further.",
                AppliesToSkillId = "whirlwind", CooldownPercent = -35, RadiusAdd = 1f,
            },
        };

        /// <summary>
        /// Skills made before classes whose asset file keeps its old name, and those whose name,
        /// unlock level, scaling stat or damage changed when they moved onto a class.
        /// </summary>
        private static readonly Dictionary<string, string> SkillFileNames = new Dictionary<string, string> { ["fire-bolt"] = "Fire Bolt" };
        private static readonly HashSet<string> RetunedForClasses = new HashSet<string> { "fire-bolt", "hunters-javelin" };

        /// <summary>Three Goblins at 25 XP each: enough for the first level-up (50 XP) in one fight.</summary>
        private static readonly Vector3[] GoblinSpawns = { new Vector3(0f, 0f, 8f), new Vector3(-4f, 0f, 10f), new Vector3(4f, 0f, 10f) };

        /// <summary>Creates any missing content assets and refreshes the content list, without touching the scenes.</summary>
        [MenuItem("Hunter System/Refresh Content")]
        public static void RefreshContent() => GetOrCreateContent();

        [MenuItem("Hunter System/Rebuild All Scenes")]
        public static void BuildAll()
        {
            Build();
            BuildArena();
            BuildCoop();
        }

        [MenuItem("Hunter System/Rebuild Greybox Scene")]
        public static void Build() => BuildScene(ScenePath, simulation =>
        {
            simulation.AddComponent<DebugModifierToggles>();
            var goblin = GetOrCreateGoblinDefinition();
            var goblinMaterial = GetOrCreateMaterial("Greybox_Goblin", GoblinColour);
            for (var i = 0; i < GoblinSpawns.Length; i++)
                GreyboxVisuals.CreateEnemy($"Goblin {i + 1}", GoblinSpawns[i], goblin, goblinMaterial);
        });

        [MenuItem("Hunter System/Rebuild Test Arena Scene")]
        public static void BuildArena() => BuildScene(ArenaScenePath, simulation =>
            Wire(simulation.AddComponent<ArenaPanel>(), ("enemyMaterial", GetOrCreateMaterial("Greybox_Goblin", GoblinColour))));

        /// <summary>
        /// The co-op spike scene: no player placed (players appear as they join), the online views,
        /// the launcher menu, and two inactive network managers: Tugboat for local play, FishyFacepunch for Steam.
        /// </summary>
        [MenuItem("Hunter System/Rebuild Co-op Scene")]
        public static void BuildCoop() => BuildScene(CoopScenePath, simulation =>
        {
            Wire(simulation.AddComponent<OnlineViews>(),
                ("localPlayerMaterial", GetOrCreateMaterial("Greybox_Player", new Color(0.2f, 0.45f, 0.85f))),
                ("remotePlayerMaterial", GetOrCreateMaterial("Greybox_Ally", new Color(0.25f, 0.75f, 0.4f))),
                ("swingMaterial", GetOrCreateMaterial("Greybox_Swing", new Color(1f, 0.85f, 0.3f))),
                ("enemyMaterial", GetOrCreateMaterial("Greybox_Goblin", GoblinColour)),
                ("bossMaterial", GetOrCreateMaterial("Greybox_Boss", new Color(0.55f, 0.12f, 0.12f))));

            var runner = simulation.GetComponent<SimulationRunner>();
            var runnerSettings = new SerializedObject(runner);
            runnerSettings.FindProperty("useHub").boolValue = true; // the co-op scene gathers in the hub
            runnerSettings.ApplyModifiedPropertiesWithoutUndo();
            Wire(simulation.AddComponent<HubView>(),
                ("floorMaterial", GetOrCreateMaterial("Greybox_HubFloor", new Color(0.5f, 0.47f, 0.42f))),
                ("wallMaterial", GetOrCreateMaterial("Greybox_GateWall", new Color(0.3f, 0.3f, 0.34f))),
                ("boardMaterial", GetOrCreateMaterial("Greybox_GateBoard", new Color(0.25f, 0.4f, 0.75f))),
                ("shopMaterial", GetOrCreateMaterial("Greybox_Shop", new Color(0.75f, 0.6f, 0.25f))));
            simulation.AddComponent<GateBoard>();
            simulation.AddComponent<ShopPanel>();
            simulation.AddComponent<TextScenePlayer>();

            Wire(simulation.AddComponent<BossTelegraphView>(),
                ("areaMaterial", GetOrCreateMaterial("Greybox_TelegraphArea", new Color(0.55f, 0.18f, 0.12f))),
                ("fillMaterial", GetOrCreateMaterial("Greybox_TelegraphFill", new Color(1f, 0.3f, 0.12f))));
            Wire(simulation.AddComponent<RaidView>(), ("markMaterial", GetOrCreateMaterial("Greybox_RaidMark", Color.white)));
            Wire(simulation.AddComponent<GateView>(),
                ("floorMaterial", GetOrCreateMaterial("Greybox_GateFloor", new Color(0.42f, 0.44f, 0.5f))),
                ("wallMaterial", GetOrCreateMaterial("Greybox_GateWall", new Color(0.3f, 0.3f, 0.34f))),
                ("checkpointMaterial", GetOrCreateMaterial("Greybox_Checkpoint", new Color(0.35f, 0.35f, 0.4f))),
                ("checkpointActiveMaterial", GetOrCreateMaterial("Greybox_CheckpointActive", new Color(0.3f, 0.9f, 1f))),
                ("portalMaterial", GetOrCreateMaterial("Greybox_Portal", new Color(0.55f, 0.95f, 1f))));
            simulation.AddComponent<GateHud>();

            var local = CreateNetwork<Tugboat>("Network (Local UDP)");
            var steam = CreateNetwork<FishyFacepunch.FishyFacepunch>("Network (Steam)");
            Wire(simulation.AddComponent<CoopLauncher>(), ("localNetwork", local), ("steamNetwork", steam));
        }, includePlayer: false);

        /// <summary>
        /// An inactive Fish-Net network manager with the given transport; the launcher activates the
        /// one it uses. Fish-Net sends on its own network tick (30 Hz by default), which would add up
        /// to a tick of delay each way on top of ours, so it runs at the simulation's 60 Hz.
        /// </summary>
        private static GameObject CreateNetwork<TTransport>(string name) where TTransport : Component
        {
            var network = new GameObject(name);
            network.SetActive(false);
            network.AddComponent<TTransport>();
            var time = network.AddComponent<FishNet.Managing.Timing.TimeManager>();
            var so = new SerializedObject(time);
            so.FindProperty("_tickRate").intValue = 60;
            so.ApplyModifiedPropertiesWithoutUndo();
            network.AddComponent<NetworkManager>();
            return network;
        }

        /// <summary>A scene with the shared parts (light, camera, floor, simulation, player), plus <paramref name="addExtras"/>.</summary>
        private static void BuildScene(string path, System.Action<GameObject> addExtras, bool includePlayer = true)
        {
            // New scene first: opening it unloads unused assets, which would orphan anything loaded before.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var content = GetOrCreateContent();
            var cameraRig = CreateLightCameraAndFloor();
            var simulation = CreateSimulation(content);
            if (includePlayer) CreatePlayer(cameraRig);
            addExtras(simulation);

            EditorSceneManager.SaveScene(scene, path);
            RegisterScenes();
        }

        // --- Shared scene parts ------------------------------------------------------------

        private static ThirdPersonCamera CreateLightCameraAndFloor()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            // Matches the rig's starting orbit so the scene view looks right before Play.
            var focus = Vector3.up * ThirdPersonCamera.DefaultTargetHeight;
            camera.transform.position = CameraMath.OrbitPosition(focus, 0f, ThirdPersonCamera.DefaultPitch, ThirdPersonCamera.DefaultDistance);
            camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position, Vector3.up);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(5f, 1f, 5f); // 50 m x 50 m
            floor.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Greybox_Floor", new Color(0.55f, 0.55f, 0.55f));

            return camera.gameObject.AddComponent<ThirdPersonCamera>();
        }

        private static GameObject CreateSimulation(GameContentAsset content)
        {
            var simulation = new GameObject("Simulation");
            Wire(simulation.AddComponent<SimulationRunner>(), ("content", content));
            simulation.AddComponent<SystemHud>();
            simulation.AddComponent<ClassPicker>();
            Wire(simulation.AddComponent<PerfectDodgeView>(), ("ringMaterial", GetOrCreateMaterial("Greybox_PerfectDodge", new Color(0.55f, 0.95f, 1f))));
            Wire(simulation.AddComponent<SkillEffectsView>(),
                ("projectileMaterial", GetOrCreateMaterial("Greybox_Fire", new Color(1f, 0.45f, 0.1f))),
                ("areaMaterial", GetOrCreateMaterial("Greybox_SkillArea", new Color(1f, 0.75f, 0.3f))),
                ("coneMaterial", GetOrCreateMaterial("Greybox_SkillCone", new Color(0.6f, 0.85f, 1f))),
                ("explosionMaterial", GetOrCreateMaterial("Greybox_Explosion", new Color(1f, 0.3f, 0.05f))));
            return simulation;
        }

        /// <summary>The player sits at the feet; the capsule and the swing marker are children above.</summary>
        private static void CreatePlayer(ThirdPersonCamera cameraRig)
        {
            var player = new GameObject("Player 1");
            var playerView = player.AddComponent<PlayerView>();
            var playerBody = GreyboxVisuals.CreateVisual(player, PrimitiveType.Capsule, "Capsule", new Vector3(0f, 1f, 0f), Vector3.one,
                GetOrCreateMaterial("Greybox_Player", new Color(0.2f, 0.45f, 0.85f)));
            // A flat slab in front of the player, shown for a moment on each light attack.
            var swing = GreyboxVisuals.CreateVisual(player, PrimitiveType.Cube, "Swing Marker", new Vector3(0f, 1f, 1.1f), new Vector3(1.6f, 0.1f, 0.9f),
                GetOrCreateMaterial("Greybox_Swing", new Color(1f, 0.85f, 0.3f)));
            Wire(playerView, ("swingMarker", swing.gameObject), ("body", playerBody));
            Wire(cameraRig, ("target", player.transform));
        }

        /// <summary>Greybox first (the default build scene), then the arena, then anything else.</summary>
        private static void RegisterScenes()
        {
            var builtScenes = new[] { ScenePath, ArenaScenePath, CoopScenePath, OverworldPatchScenePath, ContinentScenePath };
            var existing = builtScenes.Where(p => AssetDatabase.LoadAssetAtPath<SceneAsset>(p) != null).Select(p => new EditorBuildSettingsScene(p, true));
            var others = EditorBuildSettings.scenes.Where(s => !builtScenes.Contains(s.path));
            EditorBuildSettings.scenes = existing.Concat(others).ToArray();
            AssetDatabase.SaveAssets();
        }

        // --- Content -----------------------------------------------------------------------

        /// <summary>
        /// The GameContent asset, with every skill, modifier and enemy asset in the project listed.
        /// Existing order is kept (it sets the starting bar) and newly found assets are appended, so
        /// adding content is a matter of creating its asset.
        /// </summary>
        private static GameContentAsset GetOrCreateContent()
        {
            var tuning = GetOrCreateSingle<PlayerTuningAsset>(PlayerTuningPath);
            var progression = GetOrCreateSingle<ProgressionAsset>(ProgressionPath); // its defaults hold the default curve
            var classes = ClassData.Select(GetOrCreateClass).ToArray();
            var skills = SkillData.Concat(SpecialtyTreeSkills).Select(data => GetOrCreateSkill(data, classes)).ToArray();
            var modifiers = DemoModifierData.Select(GetOrCreateModifier).ToArray();
            var passives = SpecialtyModifierData.Concat(SpecialtyTreePassives).Select(GetOrCreateModifier).ToArray();
            var enemies = new[] { GetOrCreateGoblinDefinition(), GetOrCreateHobgoblinDefinition(), GetOrCreateRaidCrystal() };

            // Each class's two trials (ADR 0022), with a clear reward that suits the class.
            var trialRewards = new Dictionary<string, string> { ["warrior"] = "deep-rend", ["rogue"] = "split-shot", ["archer"] = "split-shot", ["wizard"] = "searing", ["monk"] = "split-shot", ["cleric"] = "split-shot", ["warlock"] = "split-shot", ["summoner"] = "split-shot" };
            var trials = classes.Where(c => trialRewards.ContainsKey(c.Id)).ToDictionary(c => c.Id, c =>
            {
                var reward = modifiers.Where(m => m.Id == trialRewards[c.Id]).ToArray();
                return new[] { GetOrCreateTrial(c.DisplayName, "D", reward), GetOrCreateTrial(c.DisplayName, "B", reward) };
            });
            var gates = new[] { GetOrCreateGoblinWarren(modifiers), GetOrCreateThornwoodHollow(), GetOrCreateGoblinKingRaid(modifiers) }.Concat(trials.Values.SelectMany(t => t)).ToArray();
            GiveWarriorItsSpecialties(classes.First(c => c.Id == "warrior"), trials["warrior"], passives);
            foreach (var c in classes)
                if (trials.TryGetValue(c.Id, out var own)) FillSpecialtyTree(c, own, passives);
            var bosses = new[] { GetOrCreateGoblinWarlord(), GetOrCreateGoblinKing() };
            var hub = GetOrCreateSingle<HubAsset>(HubPath);
            var scenes = new[] { GetOrCreateArrivalScene() }.Concat(GetOrCreatePromotionScenes())
                .Concat(classes.Where(c => trials.ContainsKey(c.Id)).SelectMany(c => trials[c.Id].Select(t => GetOrCreateTrialScene(t, c.DisplayName)))).ToArray();
            var scaling = GetOrCreateSingle<PartyScalingAsset>(PartyScalingPath); // its defaults are the starting tiers

            var content = AssetDatabase.LoadAssetAtPath<GameContentAsset>(ContentPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<GameContentAsset>();
                AssetDatabase.CreateAsset(content, ContentPath);
            }

            // More gear (ticket 61): four sets and two weapons per class, each new piece in its gates' drop lists.
            content.SetGear(GetOrCreateMoreGear(content.Gear, gates.First(g => g.Id == "thornwood-hollow"), gates.First(g => g.IsRaid), trials));

            content.ReplaceContent(tuning, progression,
                WithEveryProjectAsset(content.Classes, classes),
                WithEveryProjectAsset(content.Skills, skills),
                WithEveryProjectAsset(content.Modifiers, modifiers.Concat(passives)),
                WithEveryProjectAsset(content.Enemies, enemies),
                WithEveryProjectAsset(content.Gates, gates),
                WithEveryProjectAsset(content.Bosses, bosses),
                hub,
                WithEveryProjectAsset(content.Scenes, scenes),
                scaling,
                WithEveryProjectAsset(content.Shops, new[] { GetOrCreateGuildStore(hub) }));
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();
            // Saving re-imports the asset; hand back the reloaded object, or scene references to it come out empty.
            return AssetDatabase.LoadAssetAtPath<GameContentAsset>(ContentPath);
        }

        /// <summary>
        /// The current list, then the given defaults, then every other asset of the type found anywhere
        /// in the project, without repeats. (The content test still fails on assets added after the last rebuild.)
        /// </summary>
        private static T[] WithEveryProjectAsset<T>(IReadOnlyList<T> current, IEnumerable<T> defaults) where T : Object
        {
            var everyAsset = AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));
            return current.Where(a => a != null).Concat(defaults).Concat(everyAsset).Distinct().ToArray();
        }

        /// <summary>Assigns private [SerializeField] references, as dragging them in the Inspector would.</summary>
        private static void Wire(Object target, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in fields) so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The guild's store, in the hub beside the gate board (ticket 22). It is the easiest store
        /// to reach and pays the least: half an item's value. Stores in the towns pay more.
        /// </summary>
        private static ShopAsset GetOrCreateGuildStore(HubAsset hub)
        {
            var centre = hub.ToDefinition().Centre;
            return GetOrCreate<ShopAsset>("Shops", "Guild Exchange", a =>
                a.Set("guild-exchange", "Guild Exchange", new Vector3(centre.X - 7f, 0f, centre.Z + 5f), 2.5f, 50));
        }

        private static ClassDefinitionAsset GetOrCreateClass(ClassDefinition data)
        {
            var asset = GetOrCreate<ClassDefinitionAsset>("Classes", data.Name, a => a.CopyFrom(data));
            // A new class's weapon (ticket 55): the gear asset with that id, set once if the class has none.
            if (asset.StartingWeapon == null && data.StartingWeaponId != null)
            {
                var weapon = AssetDatabase.FindAssets("t:GearDefinitionAsset").Select(g => AssetDatabase.LoadAssetAtPath<GearDefinitionAsset>(AssetDatabase.GUIDToAssetPath(g)))
                    .FirstOrDefault(g => g != null && g.Id == data.StartingWeaponId);
                if (weapon != null)
                {
                    asset.SetStartingWeapon(weapon);
                    EditorUtility.SetDirty(asset);
                }
            }
            return asset;
        }

        /// <summary>
        /// The skill's asset, created if missing. A skill asset made before classes (no class yet)
        /// is moved onto its class once, and retuned if it changed when it did; after that, as with
        /// every asset, the Inspector's values stand.
        /// </summary>
        private static SkillDefinitionAsset GetOrCreateSkill(SkillDefinition data, ClassDefinitionAsset[] classes)
        {
            var characterClass = classes.FirstOrDefault(c => c.Id == data.ClassId);
            var fileName = SkillFileNames.TryGetValue(data.Id, out var old) ? old : data.Name;
            var asset = GetOrCreate<SkillDefinitionAsset>("Skills", fileName, a =>
            {
                a.CopyFrom(data);
                a.SetClass(characterClass);
            });
            if (asset.Class == null && characterClass != null && asset.Id == data.Id)
            {
                asset.SetClass(characterClass);
                if (RetunedForClasses.Contains(data.Id)) asset.SetForClasses(data.Name, data.UnlockLevel, data.ScalingStat, data.Damage);
                EditorUtility.SetDirty(asset);
            }
            return asset;
        }

        private static ModifierDefinitionAsset GetOrCreateModifier(ModifierDefinition data) =>
            GetOrCreate<ModifierDefinitionAsset>("Modifiers", data.Name, a => a.CopyFrom(data));

        private static EnemyDefinitionAsset GetOrCreateGoblinDefinition() =>
            GetOrCreate<EnemyDefinitionAsset>("Enemies", "Goblin", a => a.SetStats("Goblin", maxHealth: 30, moveSpeed: 3f, stopDistance: 1.2f));

        /// <summary>A slower, tougher enemy with a longer, harder-hitting telegraph, so the arena has a real choice.</summary>
        private static EnemyDefinitionAsset GetOrCreateHobgoblinDefinition() =>
            GetOrCreate<EnemyDefinitionAsset>("Enemies", "Hobgoblin", a =>
            {
                a.SetStats("Hobgoblin", maxHealth: 90, moveSpeed: 2.2f, stopDistance: 1.4f);
                a.SetAttackAndReward(damage: 20, range: 1.9f, windupSeconds: 0.8f, cooldownSeconds: 2.2f, xpReward: 60);
            });

        // --- The room kit and the first gate (ticket 17) -----------------------------------------

        private static RoomPieceAsset.Spawn At(EnemyDefinitionAsset enemy, float x, float z) =>
            new RoomPieceAsset.Spawn { enemy = enemy, offset = new Vector3(x, 0f, z) };

        private static RoomPieceAsset GetOrCreateRoom(string name, float width, float length, bool checkpoint, params RoomPieceAsset.Spawn[] spawns) =>
            GetOrCreate<RoomPieceAsset>("Rooms", name, a => a.Set(width, length, spawns, checkpoint));

        private static BossDefinitionAsset.Attack BossAttack(BossAttackKind kind, int damage, float windup, float cooldown,
            float range = 0f, float arc = 120f, float radius = 0f, float speed = 0f) => new BossDefinitionAsset.Attack
        {
            kind = kind, damage = damage, windupSeconds = windup, cooldownSeconds = cooldown, range = range, arcDegrees = arc, radius = radius, speed = speed,
        };

        /// <summary>
        /// The first boss (ticket 18): a Goblin Warlord. A sweep in front (get behind it), a slam all
        /// around it (get out), and a charge at anyone out of reach (step aside), each with a long
        /// readable telegraph. At half health it enrages: quicker, and four goblins join.
        /// </summary>
        private static BossDefinitionAsset GetOrCreateGoblinWarlord()
        {
            var goblin = GetOrCreateGoblinDefinition();
            BossDefinitionAsset.Add At(float x, float z) => new BossDefinitionAsset.Add { enemy = goblin, offset = new Vector3(x, 0f, z) };
            return GetOrCreate<BossDefinitionAsset>("Bosses", "Goblin Warlord", a => a.Set("Goblin Warlord", health: 600, speed: 2.4f, stop: 1.8f, xp: 300,
                new[]
                {
                    BossAttack(BossAttackKind.Sweep, damage: 22, windup: 0.9f, cooldown: 1.2f, range: 4.5f, arc: 130f),
                    BossAttack(BossAttackKind.Slam, damage: 30, windup: 1.1f, cooldown: 1.6f, radius: 3.5f),
                    BossAttack(BossAttackKind.Charge, damage: 26, windup: 1.0f, cooldown: 1.8f, range: 12f, radius: 1.2f, speed: 11f),
                },
                new[] { At(-4f, 0f), At(4f, 0f), At(-2f, -3f), At(2f, -3f) }));
        }

        /// <summary>
        /// The first gate, built from eight kit pieces (the corridor is used three times): an empty
        /// entrance hall, three fights of rising size joined by corridors with a checkpoint in the
        /// first two, then an antechamber checkpoint and the Warlord's chamber. Greybox content for
        /// proving the gate runtime, not a designed dungeon.
        /// </summary>
        private static GateDefinitionAsset GetOrCreateGoblinWarren(ModifierDefinitionAsset[] rewardPool)
        {
            var goblin = GetOrCreateGoblinDefinition();
            var hobgoblin = GetOrCreateHobgoblinDefinition();
            var entrance = GetOrCreateRoom("Entrance Hall", 10f, 10f, checkpoint: false);
            var corridor = GetOrCreateRoom("Corridor", 4f, 8f, checkpoint: false);
            var den = GetOrCreateRoom("Goblin Den", 14f, 18f, checkpoint: true,
                At(goblin, -4f, 4f), At(goblin, 4f, 4f), At(goblin, 0f, 7f));
            var guardRoom = GetOrCreateRoom("Guard Room", 16f, 20f, checkpoint: true,
                At(goblin, -5f, 3f), At(goblin, 5f, 3f), At(goblin, -3f, 7f), At(goblin, 3f, 7f), At(hobgoblin, 0f, 8f));
            var hall = GetOrCreateRoom("Great Hall", 18f, 22f, checkpoint: false,
                At(goblin, -6f, 2f), At(goblin, 6f, 2f), At(goblin, -3f, 6f), At(goblin, 3f, 6f), At(hobgoblin, -4f, 9f), At(hobgoblin, 4f, 9f));
            var bossChamber = GetOrCreate<RoomPieceAsset>("Rooms", "Warlord Chamber",
                a => a.Set(22f, 26f, new RoomPieceAsset.Spawn[0], checkpoint: false, roomBoss: GetOrCreateGoblinWarlord(), bossAt: new Vector3(0f, 0f, 6f)));
            var antechamber = GetOrCreateRoom("Antechamber", 8f, 8f, checkpoint: true); // a checkpoint just before the boss
            return GetOrCreate<GateDefinitionAsset>("Gates", "Goblin Warren",
                a => a.Set("goblin-warren", "Goblin Warren", new[] { entrance, corridor, den, corridor, guardRoom, corridor, hall, antechamber, bossChamber },
                    rewardPool));
        }

        // --- The first raid (ticket 29) -------------------------------------------------------------

        /// <summary>A raid's crystal: it only stands there and takes hits. Broken together, they drop the boss's shield.</summary>
        private static EnemyDefinitionAsset GetOrCreateRaidCrystal() =>
            GetOrCreate<EnemyDefinitionAsset>("Enemies", "Shield Crystal", a =>
            {
                a.SetStats("Shield Crystal", maxHealth: 150, moveSpeed: 0f, stopDistance: 0f);
                a.SetAttackAndReward(damage: 0, range: 0f, windupSeconds: 0.5f, cooldownSeconds: 1f, xpReward: 0);
            });

        /// <summary>
        /// The first raid boss (ticket 29): the Goblin King. The Warlord's three answers (sweep, slam, charge), bigger and
        /// tougher, with six goblins at its enrage. Its raid phases (shield crystals, then the shelter blasts) are the
        /// gate's, not the boss's.
        /// </summary>
        private static BossDefinitionAsset GetOrCreateGoblinKing()
        {
            var goblin = GetOrCreateGoblinDefinition();
            BossDefinitionAsset.Add At(float x, float z) => new BossDefinitionAsset.Add { enemy = goblin, offset = new Vector3(x, 0f, z) };
            return GetOrCreate<BossDefinitionAsset>("Bosses", "Goblin King", a => a.Set("Goblin King", health: 2400, speed: 2.4f, stop: 2.2f, xp: 800,
                new[]
                {
                    BossAttack(BossAttackKind.Sweep, damage: 26, windup: 1.0f, cooldown: 1.2f, range: 5.5f, arc: 140f),
                    BossAttack(BossAttackKind.Slam, damage: 34, windup: 1.2f, cooldown: 1.6f, radius: 4.5f),
                    BossAttack(BossAttackKind.Charge, damage: 30, windup: 1.1f, cooldown: 1.8f, range: 16f, radius: 1.4f, speed: 12f),
                },
                new[] { At(-5f, 0f), At(5f, 0f), At(-3f, -4f), At(3f, -4f), At(-3f, 4f), At(3f, 4f) },
                enrageAt: 50, enragedTime: 75, turnRate: 55f));
        }

        /// <summary>
        /// The first raid gate (ticket 29), for up to ten: a short way in (a barracks fight with a checkpoint, then an
        /// antechamber checkpoint) and the Goblin King's throne hall, wide enough for groups to split to its four
        /// corners. Crystals stand in the corners (one per three players present), the shelter moves round the hall.
        /// </summary>
        private static GateDefinitionAsset GetOrCreateGoblinKingRaid(ModifierDefinitionAsset[] rewardPool)
        {
            var goblin = GetOrCreateGoblinDefinition();
            var hobgoblin = GetOrCreateHobgoblinDefinition();
            var entrance = GetOrCreateRoom("Entrance Hall", 10f, 10f, checkpoint: false);
            var corridor = GetOrCreateRoom("Corridor", 4f, 8f, checkpoint: false);
            var barracks = GetOrCreateRoom("Raid Barracks", 20f, 22f, checkpoint: true,
                At(goblin, -6f, 3f), At(goblin, 6f, 3f), At(goblin, -3f, 7f), At(goblin, 3f, 7f), At(hobgoblin, -5f, 10f), At(hobgoblin, 5f, 10f));
            var antechamber = GetOrCreateRoom("Antechamber", 8f, 8f, checkpoint: true);
            var throne = GetOrCreate<RoomPieceAsset>("Rooms", "Throne Hall",
                a => a.Set(36f, 36f, new RoomPieceAsset.Spawn[0], checkpoint: false, roomBoss: GetOrCreateGoblinKing(), bossAt: new Vector3(0f, 0f, 2f)));
            return GetOrCreate<GateDefinitionAsset>("Gates", "Goblin King Raid", a =>
            {
                a.Set("goblin-king-raid", "The Goblin King's Throne", new[] { entrance, corridor, barracks, corridor, antechamber, throne }, rewardPool);
                a.SetRankAndIntel("", $"[Guild notice: a raid gate for up to ten hunters. Split up to break the crystals together, then shelter from the King's blast: {TextSceneAsset.Unwritten}]");
                a.SetRaid(new GateDefinitionAsset.RaidSettings
                {
                    isRaid = true,
                    crystal = GetOrCreateRaidCrystal(),
                    crystalPlaces = new[] { new Vector3(-13f, 0f, -12f), new Vector3(13f, 0f, 12f), new Vector3(13f, 0f, -12f), new Vector3(-13f, 0f, 12f) },
                    playersPerCrystal = 3,
                    crystalRegrowSeconds = 8f,
                    shelterAtHealthPercent = 35,
                    firstShelterAfterSeconds = 2f,
                    shelterEverySeconds = 12f,
                    shelterWindupSeconds = 4f,
                    shelterRadius = 3.5f,
                    shelterDamagePercent = 80,
                    shelterPlaces = new[] { new Vector3(10f, 0f, 9f), new Vector3(-10f, 0f, -9f), new Vector3(-10f, 0f, 10f), new Vector3(10f, 0f, -10f) },
                });
            });
        }

        /// <summary>
        /// The first story scene (ticket 20): it plays the first time a host's world reaches the hub.
        /// Every line is a blank marker: the words are the developer's to write (ticket 16).
        /// </summary>
        private static TextSceneAsset GetOrCreateArrivalScene()
        {
            TextSceneAsset.Line Blank(string who, string what) => new TextSceneAsset.Line
            {
                speaker = who, text = $"[{what}: {TextSceneAsset.Unwritten}]",
            };
            return GetOrCreate<TextSceneAsset>("Scenes", "Arrival", a => a.Set("arrival", StoryTrigger.FirstHubArrival, new[]
            {
                Blank("", "Narration: arriving at the guild hall"),
                Blank("[Guild NPC]", "A guild member greets the new E-rank hunter"),
                Blank("[Guild NPC]", "Points the hunter to the gate board"),
            }));
        }

        /// <summary>
        /// The rank promotion scenes (ticket 25), D to A: each plays once the host's world is promoted and the party
        /// is home. Blank markers: the words are the developer's (ADR 0009). The S promotion belongs to the S story.
        /// </summary>
        private static IEnumerable<TextSceneAsset> GetOrCreatePromotionScenes()
        {
            TextSceneAsset.Line Blank(string who, string what) => new TextSceneAsset.Line
            {
                speaker = who, text = $"[{what}: {TextSceneAsset.Unwritten}]",
            };
            var opens = new Dictionary<string, string>
            {
                ["D"] = "work in Fenward mire, and the first mount",
                ["C"] = "harder work in the regions the hunter knows",
                ["B"] = "work on the Ashen steppe",
                ["A"] = "A-rank gates in the regions the hunter knows",
            };
            foreach (var rank in opens)
                yield return GetOrCreate<TextSceneAsset>("Scenes", $"Promotion {rank.Key}", a => a.Set($"promotion-{rank.Key.ToLowerInvariant()}", StoryTrigger.RankPromotion, new[]
                {
                    Blank("", $"Narration: the guild calls the hunter in for the {rank.Key}-rank licence"),
                    Blank("[Guild NPC]", $"The guild grants the {rank.Key}-rank licence"),
                    Blank("[Guild NPC]", $"What the {rank.Key}-rank licence opens: {rank.Value}"),
                }, forRank: rank.Key));
        }

        // --- Specialties (ticket 26) ---------------------------------------------------------------

        /// <summary>
        /// One of the Warrior's trials (ticket 26): a gate at <paramref name="rank"/> (D or B) the guild posts on the board
        /// (it has no site on the map). Clearing it as a Warrior of that rank unlocks that tier's specialty pick. Like
        /// every boss gate it gives a clear reward. Greybox content from the kit, ending with the Warlord: the D trial
        /// a goblin den, the B trial a bigger proving ground of hobgoblins.
        /// </summary>
        private static GateDefinitionAsset GetOrCreateTrial(string className, string rank, ModifierDefinitionAsset[] rewardPool)
        {
            var goblin = GetOrCreateGoblinDefinition();
            var hobgoblin = GetOrCreateHobgoblinDefinition();
            var entrance = GetOrCreateRoom("Entrance Hall", 10f, 10f, checkpoint: false);
            var corridor = GetOrCreateRoom("Corridor", 4f, 8f, checkpoint: false);
            var fight = rank == "D"
                ? GetOrCreateRoom("Goblin Den", 14f, 18f, checkpoint: true, At(goblin, -4f, 4f), At(goblin, 4f, 4f), At(goblin, 0f, 7f))
                : GetOrCreateRoom("Proving Ground", 18f, 22f, checkpoint: true,
                    At(hobgoblin, -5f, 4f), At(hobgoblin, 5f, 4f), At(hobgoblin, -3f, 9f), At(hobgoblin, 3f, 9f),
                    At(goblin, -7f, 7f), At(goblin, 7f, 7f), At(goblin, 0f, 11f));
            var antechamber = GetOrCreateRoom("Antechamber", 8f, 8f, checkpoint: true);
            var bossChamber = GetOrCreate<RoomPieceAsset>("Rooms", "Warlord Chamber",
                a => a.Set(22f, 26f, new RoomPieceAsset.Spawn[0], checkpoint: false, roomBoss: GetOrCreateGoblinWarlord(), bossAt: new Vector3(0f, 0f, 6f)));
            return GetOrCreate<GateDefinitionAsset>("Gates", $"{className} Trial {rank}", a =>
            {
                a.Set($"{className.ToLowerInvariant()}-trial-{rank.ToLowerInvariant()}", $"{className}'s {rank}-rank Trial", new[] { entrance, corridor, fight, corridor, antechamber, bossChamber }, rewardPool);
                a.SetRankAndIntel(rank, $"[Guild notice: the {className}'s {rank}-rank trial, for {rank}-rank {className}s: {TextSceneAsset.Unwritten}]");
            });
        }

        /// <summary>
        /// The Warrior's specialty tree (ticket 26), set once on the class asset if it has none: two at D, and two at B under
        /// each D one. The path Vanguard (D) then Berserker (B) is built; the rest are stubs, shown but not pickable until
        /// they have skills or passives. The names are working names.
        /// </summary>
        private static void GiveWarriorItsSpecialties(ClassDefinitionAsset warrior, GateDefinitionAsset[] trials, ModifierDefinitionAsset[] passives)
        {
            if (warrior.TrialGates.Count > 0 || warrior.Specialties.Count > 0) return; // the Inspector's values stand
            ClassDefinitionAsset.Specialty Stub(string id, string name, string rank, string parent) => new ClassDefinitionAsset.Specialty
            {
                id = id, displayName = name, rank = rank, parent = parent, passives = new ModifierDefinitionAsset[0],
                description = $"Not built yet: a {rank}-rank Warrior branch (ticket 26 builds Vanguard, then Berserker, first).",
            };
            warrior.SetSpecialties(trials, new[]
            {
                new ClassDefinitionAsset.Specialty
                {
                    id = "vanguard", displayName = "Vanguard", rank = "D",
                    description = "Leads the charge. Physical skills hit 10% harder, Shield Bash comes back sooner and reaches further, and a new skill: Overhead Smash, a heavy blow straight ahead.",
                    passives = passives.Where(p => p.Id.StartsWith("vanguard-")).ToArray(),
                },
                Stub("sentinel", "Sentinel", "D", null),
                new ClassDefinitionAsset.Specialty
                {
                    id = "berserker", displayName = "Berserker", rank = "B", parent = "vanguard",
                    description = "All-out offence. Melee skills hit 20% harder, Whirlwind comes back sooner and reaches further, and two new skills: Cleave, a huge swing on a short cooldown, and Earthsplitter (level 62), a quake you can charge.",
                    passives = passives.Where(p => p.Id.StartsWith("berserker-")).ToArray(),
                },
                Stub("champion", "Champion", "B", "vanguard"),
                Stub("guardian", "Guardian", "B", "sentinel"),
                Stub("bulwark", "Bulwark", "B", "sentinel"),
            });
            EditorUtility.SetDirty(warrior);
        }

        /// <summary>A trial's scene (ticket 26): plays after the first clear of that trial in a world. Blank markers (ADR 0009).</summary>
        private static TextSceneAsset GetOrCreateTrialScene(GateDefinitionAsset trial, string className)
        {
            TextSceneAsset.Line Blank(string who, string what) => new TextSceneAsset.Line
            {
                speaker = who, text = $"[{what}: {TextSceneAsset.Unwritten}]",
            };
            return GetOrCreate<TextSceneAsset>("Scenes", $"{className} Trial {trial.Rank} Cleared", a => a.Set($"{trial.Id}-cleared", StoryTrigger.GateCleared, new[]
            {
                Blank("", $"Narration: back at the guild after the {className}'s {trial.Rank}-rank trial"),
                Blank("[Guild NPC]", "The guild says the trial is passed"),
                Blank("[Guild NPC]", $"The paths a {trial.Rank}-rank {className} can take from here, and that the choice is for good"),
            }, forGate: trial));
        }

        /// <summary>A settings asset at a fixed path, created with its field defaults if missing and never overwritten.</summary>
        private static T GetOrCreateSingle<T>(string path) where T : ScriptableObject
        {
            EnsureFolder("Assets", "Data");
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset; // keep any tuning done in the Inspector

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Loads <c>Assets/Data/subfolder/Name.asset</c>, or creates it with <paramref name="initialise"/>. Existing assets are never overwritten.</summary>
        private static T GetOrCreate<T>(string subfolder, string displayName, System.Action<T> initialise) where T : ScriptableObject
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder(DataFolder, subfolder);
            // File names keep letters and digits only ("Hunter's Javelin" -> HuntersJavelin.asset).
            var fileName = new string(displayName.Where(char.IsLetterOrDigit).ToArray());
            var path = $"{DataFolder}/{subfolder}/{fileName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset; // keep any tuning done in the Inspector

            asset = ScriptableObject.CreateInstance<T>();
            initialise(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.CreateFolder("Assets/Art", "Materials");
            var path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline.defaultShader
                    : Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
