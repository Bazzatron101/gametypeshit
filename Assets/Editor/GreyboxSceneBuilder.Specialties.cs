using System.Collections.Generic;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using UnityEditor;

namespace HunterSystem.Editor
{
    /// <summary>
    /// Every class's specialty tree (ADR 0022, tickets 26 and 54) as data: two specialties at D, and two at B under each D one.
    /// A D specialty has two passives and one skill; a B specialty two passives and two skills. A specialty's description is
    /// its flavour plus what its passives and skills do, worked out from the numbers, so the two never disagree. The names
    /// are working names. The Vanguard and the Berserker (ticket 26) are kept in the main builder's tables.
    /// </summary>
    public static partial class GreyboxSceneBuilder
    {
        private sealed class SpecialtyData
        {
            public string ClassId, Id, Name, Rank, Parent, Flavour;
            public ModifierDefinition[] Passives;
            public SkillDefinition[] Skills;
        }

        /// <summary>The level a specialty's skills come at: D's at the start of D, B's at the start of B and a little after.</summary>
        private const int DSkillLevel = 16, BSkillLevel = 56, BSecondSkillLevel = 62;

        private static ModifierDefinition OnTag(string id, string name, string tag, int damage = 0, int cooldown = 0, float range = 0f, float radius = 0f,
            int extraProjectiles = 0, float burstRadius = 0f, int burstPercent = 0) => new ModifierDefinition
        {
            Id = id, Name = name, AppliesToTag = tag, DamagePercent = damage, CooldownPercent = cooldown, RangeAdd = range, RadiusAdd = radius,
            ExtraProjectiles = extraProjectiles, ProjectileSpreadDegrees = 15f, ExplosionRadius = burstRadius, ExplosionDamagePercent = burstPercent,
        };

        private static ModifierDefinition OnSkill(string id, string name, string skillId, int damage = 0, int cooldown = 0, float range = 0f, float radius = 0f,
            int extraProjectiles = 0, float burstRadius = 0f, int burstPercent = 0)
        {
            var m = OnTag(id, name, null, damage, cooldown, range, radius, extraProjectiles, burstRadius, burstPercent);
            m.AppliesToTag = null;
            m.AppliesToSkillId = skillId;
            return m;
        }

        private static SkillDefinition Cone(string id, string name, string classId, string specialty, int unlock, Stat stat, int damage, float range, float arc, float cooldown, params string[] tags) =>
            new SkillDefinition { Id = id, Name = name, ClassId = classId, SpecialtyId = specialty, UnlockLevel = unlock, ScalingStat = stat, Effect = SkillEffectKind.Cone,
                Damage = damage, Range = range, ArcDegrees = arc, CooldownSeconds = cooldown, Tags = tags };

        private static SkillDefinition Area(string id, string name, string classId, string specialty, int unlock, Stat stat, int damage, float radius, float cooldown, params string[] tags) =>
            new SkillDefinition { Id = id, Name = name, ClassId = classId, SpecialtyId = specialty, UnlockLevel = unlock, ScalingStat = stat, Effect = SkillEffectKind.Area,
                Damage = damage, Radius = radius, CooldownSeconds = cooldown, Tags = tags };

        private static SkillDefinition Bolt(string id, string name, string classId, string specialty, int unlock, Stat stat, int damage, float range, float radius, float cooldown, bool pierces, params string[] tags) =>
            new SkillDefinition { Id = id, Name = name, ClassId = classId, SpecialtyId = specialty, UnlockLevel = unlock, ScalingStat = stat, Effect = SkillEffectKind.Projectile,
                Damage = damage, Range = range, Radius = radius, ProjectileSpeed = 18f, Pierces = pierces, CooldownSeconds = cooldown, Tags = tags };

        private static SkillDefinition Call(string id, string name, string specialty, int unlock, int damage, int health, float lifetime, float range, float attackSeconds, float speed, int count, float cooldown) =>
            new SkillDefinition
            {
                Id = id, Name = name, ClassId = "summoner", SpecialtyId = specialty, UnlockLevel = unlock, ScalingStat = Stat.Intelligence, Effect = SkillEffectKind.Summon,
                Damage = damage, SummonHealth = health, SummonLifetimeSeconds = lifetime, SummonAttackRange = range, SummonAttackSeconds = attackSeconds,
                SummonMoveSpeed = speed, SummonCount = count, CooldownSeconds = cooldown, Tags = new[] { SkillTags.Summon },
            };

        /// <summary>A healing skill (ticket 56): it restores <paramref name="healing"/> instead of dealing damage.</summary>
        private static SkillDefinition Heal(SkillDefinition skill, int healing)
        {
            skill.Healing = healing;
            skill.Damage = 0;
            return skill;
        }

        /// <summary>A skill that drains life (ticket 58): the caster heals <paramref name="percent"/>% of what its direct hits remove.</summary>
        private static SkillDefinition Drain(SkillDefinition skill, int percent)
        {
            skill.LifeStealPercent = percent;
            return skill;
        }

        /// <summary>A skill that can also be held to charge: +60% size and +80% damage at full charge.</summary>
        private static SkillDefinition Charged(SkillDefinition skill, float seconds = 1.2f)
        {
            skill.ChargeSeconds = seconds;
            skill.ChargedSizePercent = 60;
            skill.ChargedDamagePercent = 80;
            return skill;
        }

        private const string Summoning = SkillTags.Summon;
        private const string Melee = SkillTags.Melee, Physical = SkillTags.Physical, AreaTag = SkillTags.Area, Fire = SkillTags.Fire, Projectile = SkillTags.Projectile, Magic = SkillTags.Magic;

        private static SpecialtyData D(string classId, string id, string name, string flavour, ModifierDefinition[] passives, SkillDefinition skill) =>
            new SpecialtyData { ClassId = classId, Id = id, Name = name, Rank = "D", Flavour = flavour, Passives = passives, Skills = new[] { skill } };

        private static SpecialtyData B(string classId, string parent, string id, string name, string flavour, ModifierDefinition[] passives, params SkillDefinition[] skills) =>
            new SpecialtyData { ClassId = classId, Id = id, Name = name, Rank = "B", Parent = parent, Flavour = flavour, Passives = passives, Skills = skills };

        /// <summary>The specialties tickets 54 and 55 build: the Warrior's four that ticket 26 left as stubs, and the Rogue's, Archer's, Wizard's and Monk's trees.</summary>
        private static readonly SpecialtyData[] SpecialtyTreeData =
        {
            // --- Warrior (Strength): Vanguard and Berserker are in the main tables -------------------------------
            D("warrior", "sentinel", "Sentinel", "Holds the line and shakes the ground around it.",
                new[] { OnSkill("sentinel-footing", "Firm Footing", "ground-slam", damage: 15, radius: 1.5f), OnTag("sentinel-steady", "Steady", AreaTag, cooldown: -15) },
                Area("shockwave", "Shockwave", "warrior", "sentinel", DSkillLevel, Stat.Strength, 40, 5f, 9f, Melee, Physical, AreaTag)),
            B("warrior", "vanguard", "champion", "Champion", "A duellist who ends fights with single, crushing blows.",
                new[] { OnSkill("champion-precision", "Precision", "rending-slash", damage: 35, range: 0.5f), OnTag("champion-focus", "Focus", Physical, cooldown: -15) },
                Charged(Cone("executioners-strike", "Executioner's Strike", "warrior", "champion", BSkillLevel, Stat.Strength, 140, 3f, 45f, 10f, Melee, Physical)),
                Cone("valiant-lunge", "Valiant Lunge", "warrior", "champion", BSecondSkillLevel, Stat.Strength, 70, 6f, 30f, 6f, Melee, Physical)),
            B("warrior", "sentinel", "guardian", "Guardian", "A wall of steel that sweeps everything away from the party.",
                new[] { OnSkill("guardian-ward", "Ward", "shield-bash", damage: 40, cooldown: -20), OnTag("guardian-reach", "Wide Guard", AreaTag, radius: 1f) },
                Charged(Area("bastion-slam", "Bastion Slam", "warrior", "guardian", BSkillLevel, Stat.Strength, 70, 6f, 10f, Melee, Physical, AreaTag)),
                Cone("rampart-sweep", "Rampart Sweep", "warrior", "guardian", BSecondSkillLevel, Stat.Strength, 55, 3.5f, 270f, 7f, Melee, Physical, AreaTag)),
            B("warrior", "sentinel", "bulwark", "Bulwark", "Every blow sends a tremor through the ground.",
                new[] { OnSkill("bulwark-tremor", "Tremor", "ground-slam", burstRadius: 2.5f, burstPercent: 40), OnTag("bulwark-weight", "Weight", Melee, damage: 15) },
                Area("aftershock", "Aftershock", "warrior", "bulwark", BSkillLevel, Stat.Strength, 90, 4.5f, 12f, Melee, Physical, AreaTag),
                Cone("fault-line", "Fault Line", "warrior", "bulwark", BSecondSkillLevel, Stat.Strength, 75, 9f, 25f, 8f, Melee, Physical)),

            // --- Rogue (Agility) ------------------------------------------------------------------------------
            D("rogue", "assassin", "Assassin", "Strikes once, hard, from up close.",
                new[] { OnSkill("assassin-killer", "Killer Instinct", "eviscerate", damage: 30), OnTag("assassin-quick", "Quick Hands", Melee, cooldown: -10) },
                Cone("backstab", "Backstab", "rogue", "assassin", DSkillLevel, Stat.Agility, 75, 2f, 60f, 7f, Melee, Physical)),
            D("rogue", "trickster", "Trickster", "Fights from a step away with a hail of blades.",
                new[] { OnSkill("trickster-fan", "Fanned Knives", "throwing-knives", extraProjectiles: 2), OnTag("trickster-edge", "Keen Edge", Projectile, damage: 10) },
                Bolt("fan-of-blades", "Fan of Blades", "rogue", "trickster", DSkillLevel, Stat.Agility, 30, 9f, 0.4f, 6f, true, Projectile, Physical)),
            B("rogue", "assassin", "shadowblade", "Shadowblade", "Vanishes into the crowd and leaves it bleeding.",
                new[] { OnSkill("shadowblade-veil", "Veil", "shadow-flurry", damage: 30, radius: 1f), OnTag("shadowblade-edge", "Shadow Edge", Melee, damage: 15) },
                Area("shadow-rend", "Shadow Rend", "rogue", "shadowblade", BSkillLevel, Stat.Agility, 95, 3f, 8f, Melee, Physical, AreaTag),
                Charged(Cone("death-mark", "Death Mark", "rogue", "shadowblade", BSecondSkillLevel, Stat.Agility, 180, 2.5f, 50f, 14f, Melee, Physical), 0.8f)),
            B("rogue", "assassin", "nightstalker", "Nightstalker", "Never stops cutting: quick fangs, again and again.",
                new[] { OnSkill("nightstalker-fangs", "Sharp Fangs", "twin-fangs", damage: 40, cooldown: -20), OnSkill("nightstalker-ready", "Always Ready", "eviscerate", cooldown: -25) },
                Cone("ambush", "Ambush", "rogue", "nightstalker", BSkillLevel, Stat.Agility, 120, 3f, 70f, 9f, Melee, Physical),
                Area("thousand-cuts", "Thousand Cuts", "rogue", "nightstalker", BSecondSkillLevel, Stat.Agility, 60, 2.5f, 5f, Melee, Physical, AreaTag)),
            B("rogue", "trickster", "bladedancer", "Bladedancer", "Spins through the fight, blades flying in every direction.",
                new[] { OnTag("bladedancer-flow", "Flow", Projectile, cooldown: -15), OnTag("bladedancer-reach", "Long Throw", Projectile, range: 3f) },
                Area("whirling-blades", "Whirling Blades", "rogue", "bladedancer", BSkillLevel, Stat.Agility, 70, 4f, 7f, Melee, Physical, AreaTag),
                Bolt("blade-storm", "Blade Storm", "rogue", "bladedancer", BSecondSkillLevel, Stat.Agility, 50, 12f, 0.6f, 9f, true, Projectile, Physical)),
            B("rogue", "trickster", "saboteur", "Saboteur", "Every knife is a fuse, and every fight ends in smoke.",
                new[] { OnTag("saboteur-blast", "Blasting Caps", Projectile, burstRadius: 2f, burstPercent: 50), OnSkill("saboteur-heavy", "Weighted Knives", "throwing-knives", damage: 25) },
                Area("smoke-bomb", "Smoke Bomb", "rogue", "saboteur", BSkillLevel, Stat.Agility, 50, 5f, 10f, Physical, AreaTag),
                Charged(Bolt("blasting-charge", "Blasting Charge", "rogue", "saboteur", BSecondSkillLevel, Stat.Agility, 110, 10f, 1.5f, 12f, false, Projectile, Physical, AreaTag))),

            // --- Archer (Agility) -----------------------------------------------------------------------------
            D("archer", "marksman", "Marksman", "One arrow, aimed true, from far away.",
                new[] { OnSkill("marksman-javelin", "True Aim", "hunters-javelin", damage: 30, range: 4f), OnTag("marksman-edge", "Steady Hand", Projectile, damage: 10) },
                Charged(Bolt("aimed-shot", "Aimed Shot", "archer", "marksman", DSkillLevel, Stat.Agility, 70, 22f, 0.5f, 8f, true, Projectile, Physical))),
            D("archer", "ranger", "Ranger", "Covers the field with arrows.",
                new[] { OnSkill("ranger-rain", "Wide Rain", "arrow-rain", radius: 1.5f), OnSkill("ranger-volley", "Quick Volley", "volley", cooldown: -20) },
                Cone("scatter-shot", "Scatter Shot", "archer", "ranger", DSkillLevel, Stat.Agility, 45, 8f, 60f, 6f, Projectile, Physical)),
            B("archer", "marksman", "sniper", "Sniper", "Picks off the biggest threat before it is close.",
                new[] { OnSkill("sniper-javelin", "Heartseeker", "hunters-javelin", damage: 40), OnTag("sniper-range", "Long Sight", Projectile, range: 5f) },
                Charged(Bolt("deadeye", "Deadeye", "archer", "sniper", BSkillLevel, Stat.Agility, 180, 30f, 0.5f, 14f, true, Projectile, Physical)),
                Bolt("piercing-bolt", "Piercing Bolt", "archer", "sniper", BSecondSkillLevel, Stat.Agility, 90, 20f, 0.5f, 7f, true, Projectile, Physical)),
            B("archer", "marksman", "hawkeye", "Hawkeye", "Looses arrows faster than the eye can follow.",
                new[] { OnSkill("hawkeye-split", "Split Arrows", "quick-shot", extraProjectiles: 2), OnSkill("hawkeye-quick", "Quick Draw", "quick-shot", cooldown: -25) },
                Bolt("rapid-fire", "Rapid Fire", "archer", "hawkeye", BSkillLevel, Stat.Agility, 35, 14f, 0.4f, 2.5f, false, Projectile, Physical),
                Bolt("split-arrow", "Split Arrow", "archer", "hawkeye", BSecondSkillLevel, Stat.Agility, 60, 16f, 0.8f, 6f, true, Projectile, Physical)),
            B("archer", "ranger", "warden", "Warden", "Turns the sky over a fight into falling arrows.",
                new[] { OnSkill("warden-rain", "Storm of Arrows", "arrow-rain", damage: 35), OnTag("warden-area", "Covering Fire", AreaTag, damage: 15) },
                Charged(Area("hail-of-arrows", "Hail of Arrows", "archer", "warden", BSkillLevel, Stat.Agility, 85, 6f, 11f, Projectile, Physical, AreaTag)),
                Cone("thorn-barrage", "Thorn Barrage", "archer", "warden", BSecondSkillLevel, Stat.Agility, 70, 9f, 90f, 8f, Projectile, Physical)),
            B("archer", "ranger", "trapper", "Trapper", "Arrows that burst, and ground that bites.",
                new[] { OnTag("trapper-burst", "Barbed Heads", Projectile, burstRadius: 2f, burstPercent: 40), OnSkill("trapper-volley", "Heavy Volley", "volley", damage: 25) },
                Bolt("snare-shot", "Snare Shot", "archer", "trapper", BSkillLevel, Stat.Agility, 60, 12f, 1f, 6f, false, Projectile, Physical),
                Area("spike-field", "Spike Field", "archer", "trapper", BSecondSkillLevel, Stat.Agility, 95, 4f, 10f, Physical, AreaTag)),

            // --- Wizard (Intelligence) ------------------------------------------------------------------------
            D("wizard", "pyromancer", "Pyromancer", "Fire, and more fire.",
                new[] { OnTag("pyromancer-heat", "Heat", Fire, damage: 15), OnSkill("pyromancer-burst", "Bursting Fireball", "fire-bolt", burstRadius: 2f, burstPercent: 50) },
                Bolt("fire-lance", "Fire Lance", "wizard", "pyromancer", DSkillLevel, Stat.Intelligence, 60, 14f, 0.5f, 6f, true, Fire, Magic, Projectile)),
            D("wizard", "arcanist", "Arcanist", "Pure magic, cast quickly and often.",
                new[] { OnTag("arcanist-flow", "Flowing Mana", Magic, cooldown: -15), OnSkill("arcanist-orb", "Greater Orb", "arcane-orb", damage: 25, radius: 0.5f) },
                Bolt("arcane-missiles", "Arcane Missiles", "wizard", "arcanist", DSkillLevel, Stat.Intelligence, 35, 12f, 0.4f, 3f, false, Magic, Projectile)),
            B("wizard", "pyromancer", "flamecaller", "Flamecaller", "Calls fire down from the sky.",
                new[] { OnSkill("flamecaller-burst", "Wildfire", "flame-burst", damage: 30, radius: 1.5f), OnSkill("flamecaller-wave", "Rolling Inferno", "inferno-wave", damage: 25, range: 2f) },
                Charged(Area("meteor", "Meteor", "wizard", "flamecaller", BSkillLevel, Stat.Intelligence, 160, 6f, 16f, Fire, Magic, AreaTag), 1.5f),
                Area("pillar-of-flame", "Pillar of Flame", "wizard", "flamecaller", BSecondSkillLevel, Stat.Intelligence, 90, 2.5f, 7f, Fire, Magic, AreaTag)),
            B("wizard", "pyromancer", "ashbringer", "Ashbringer", "A swarm of embers that never lets up.",
                new[] { OnTag("ashbringer-swarm", "Swarm", Fire, extraProjectiles: 1), OnTag("ashbringer-haste", "Kindling", Fire, cooldown: -15) },
                Bolt("ember-swarm", "Ember Swarm", "wizard", "ashbringer", BSkillLevel, Stat.Intelligence, 40, 12f, 0.5f, 4f, false, Fire, Magic, Projectile),
                Area("cinder-nova", "Cinder Nova", "wizard", "ashbringer", BSecondSkillLevel, Stat.Intelligence, 100, 5f, 11f, Fire, Magic, AreaTag)),
            B("wizard", "arcanist", "chronomancer", "Chronomancer", "Bends time so spells come back sooner.",
                new[] { OnTag("chronomancer-haste", "Haste", Magic, cooldown: -20), OnSkill("chronomancer-decree", "Endless Decree", "monarchs-decree", cooldown: -30) },
                Area("time-rift", "Time Rift", "wizard", "chronomancer", BSkillLevel, Stat.Intelligence, 80, 4f, 9f, Magic, AreaTag),
                Bolt("echo-bolt", "Echo Bolt", "wizard", "chronomancer", BSecondSkillLevel, Stat.Intelligence, 75, 14f, 0.6f, 5f, true, Magic, Projectile)),
            B("wizard", "arcanist", "spellblade", "Spellblade", "Casts up close, with a blade of light.",
                new[] { OnTag("spellblade-power", "Spellpower", Magic, damage: 15), OnTag("spellblade-area", "Runic Circle", AreaTag, damage: 20) },
                Cone("arcane-slash", "Arcane Slash", "wizard", "spellblade", BSkillLevel, Stat.Intelligence, 85, 3.5f, 140f, 4f, Magic),
                Area("runic-burst", "Runic Burst", "wizard", "spellblade", BSecondSkillLevel, Stat.Intelligence, 110, 3.5f, 8f, Magic, AreaTag)),

            // --- Monk (Agility), ticket 55 ----------------------------------------------------------------------
            D("monk", "iron-fist", "Iron Fist", "Every blow lands like a hammer.",
                new[] { OnSkill("iron-fist-palm", "Iron Palm", "palm-strike", damage: 30), OnTag("iron-fist-power", "Hardened Hands", Melee, damage: 10) },
                Cone("stone-palm", "Stone Palm", "monk", "iron-fist", DSkillLevel, Stat.Agility, 80, 2.5f, 60f, 7f, Melee, Physical)),
            D("monk", "wind-walker", "Wind Walker", "Never still, and never where the enemy strikes.",
                new[] { OnTag("wind-walker-pace", "Swift Pace", Melee, cooldown: -15), OnSkill("wind-walker-kick", "Long Kick", "rising-kick", cooldown: -20, range: 1f) },
                Cone("gale-kick", "Gale Kick", "monk", "wind-walker", DSkillLevel, Stat.Agility, 50, 4f, 120f, 5f, Melee, Physical)),
            B("monk", "iron-fist", "mountain-breaker", "Mountain Breaker", "Strikes the ground hard enough to throw a crowd off its feet.",
                new[] { OnSkill("mountain-breaker-descent", "Avalanche", "dragons-descent", damage: 30, radius: 1.5f), OnTag("mountain-breaker-area", "Rockfall", AreaTag, damage: 15) },
                Charged(Area("earthshaker-fist", "Earthshaker Fist", "monk", "mountain-breaker", BSkillLevel, Stat.Agility, 130, 5f, 13f, Melee, Physical, AreaTag)),
                Cone("quaking-palm", "Quaking Palm", "monk", "mountain-breaker", BSecondSkillLevel, Stat.Agility, 110, 3f, 90f, 8f, Melee, Physical)),
            B("monk", "iron-fist", "dragon-fist", "Dragon Fist", "One perfect strike, gathered and let go.",
                new[] { OnSkill("dragon-fist-palms", "Dragon's Palms", "hundred-palms", damage: 35), OnTag("dragon-fist-power", "Inner Fire", Physical, damage: 10) },
                Charged(Cone("dragon-strike", "Dragon Strike", "monk", "dragon-fist", BSkillLevel, Stat.Agility, 170, 3f, 45f, 12f, Melee, Physical), 1f),
                Area("coiling-dragon", "Coiling Dragon", "monk", "dragon-fist", BSecondSkillLevel, Stat.Agility, 90, 3f, 7f, Melee, Physical, AreaTag)),
            B("monk", "wind-walker", "storm-dancer", "Storm Dancer", "A whirl of kicks that never lets up.",
                new[] { OnSkill("storm-dancer-flurry", "Storm Flurry", "flurry-of-fists", cooldown: -25, radius: 1f), OnTag("storm-dancer-tempo", "Tempo", Melee, cooldown: -10) },
                Area("cyclone-kick", "Cyclone Kick", "monk", "storm-dancer", BSkillLevel, Stat.Agility, 75, 3.5f, 5f, Melee, Physical, AreaTag),
                Cone("thunder-step", "Thunder Step", "monk", "storm-dancer", BSecondSkillLevel, Stat.Agility, 90, 6f, 40f, 7f, Melee, Physical)),
            B("monk", "wind-walker", "shadow-palm", "Shadow Palm", "Strikes from a distance with waves of force.",
                new[] { OnSkill("shadow-palm-waves", "Spreading Waves", "force-wave", extraProjectiles: 2), OnTag("shadow-palm-force", "Focused Force", Projectile, damage: 20) },
                Bolt("twin-waves", "Twin Waves", "monk", "shadow-palm", BSkillLevel, Stat.Agility, 60, 12f, 0.7f, 6f, true, Projectile, Physical),
                Cone("palm-of-silence", "Palm of Silence", "monk", "shadow-palm", BSecondSkillLevel, Stat.Agility, 140, 2.5f, 80f, 11f, Melee, Physical)),

            // --- Cleric (Intelligence smites, Sense heals), ticket 56 ------------------------------------------
            D("cleric", "priest", "Priest", "Keeps the party standing.",
                new[] { OnSkill("priest-mending", "Deep Mending", "mending-light", damage: 30), OnSkill("priest-sanctuary", "Wide Sanctuary", "sanctuary", radius: 1.5f) },
                Heal(Bolt("greater-mending", "Greater Mending", "cleric", "priest", DSkillLevel, Stat.Sense, 0, 14f, 0.9f, 8f, false, Magic, Projectile), 60)),
            D("cleric", "templar", "Templar", "Carries the light into the fight.",
                new[] { OnSkill("templar-smite", "Righteous Smite", "smite", damage: 30), OnTag("templar-zeal", "Zeal", Magic, damage: 10) },
                Cone("hammer-of-light", "Hammer of Light", "cleric", "templar", DSkillLevel, Stat.Intelligence, 60, 3f, 100f, 6f, Magic)),
            B("cleric", "priest", "oracle", "Oracle", "Sees who will fall, and gets there first.",
                new[] { OnSkill("oracle-sanctuary", "Quick Sanctuary", "sanctuary", cooldown: -30), OnSkill("oracle-mending", "Scattered Light", "mending-light", extraProjectiles: 2) },
                Heal(Area("circle-of-light", "Circle of Light", "cleric", "oracle", BSkillLevel, Stat.Sense, 0, 6f, 16f, Magic, AreaTag), 70),
                Heal(Bolt("blessed-bolt", "Blessed Bolt", "cleric", "oracle", BSecondSkillLevel, Stat.Sense, 0, 16f, 0.8f, 4f, false, Magic, Projectile), 45)),
            B("cleric", "priest", "saint", "Saint", "Turns a lost fight around.",
                new[] { OnSkill("saint-mending", "Swift Mending", "mending-light", cooldown: -25), OnSkill("saint-sanctuary", "Holy Ground", "sanctuary", damage: 30) },
                Charged(Heal(Area("miracle", "Miracle", "cleric", "saint", BSkillLevel, Stat.Sense, 0, 8f, 30f, Magic, AreaTag), 120), 1.5f),
                Heal(Area("halo", "Halo", "cleric", "saint", BSecondSkillLevel, Stat.Sense, 0, 4f, 8f, Magic, AreaTag), 40)),
            B("cleric", "templar", "paladin", "Paladin", "A holy knight in the front line.",
                new[] { OnSkill("paladin-judgement", "Heavy Judgement", "judgement", damage: 35), OnSkill("paladin-nova", "Bright Nova", "holy-nova", radius: 1f) },
                Cone("divine-hammer", "Divine Hammer", "cleric", "paladin", BSkillLevel, Stat.Intelligence, 110, 3.5f, 120f, 8f, Magic),
                Area("consecrate", "Consecrate", "cleric", "paladin", BSecondSkillLevel, Stat.Intelligence, 90, 4.5f, 10f, Magic, AreaTag)),
            B("cleric", "templar", "inquisitor", "Inquisitor", "Burns the wicked from far away.",
                new[] { OnSkill("inquisitor-lance", "Searing Lance", "radiant-lance", damage: 30, cooldown: -20), OnTag("inquisitor-fervour", "Fervour", Magic, damage: 15) },
                Bolt("searing-light", "Searing Light", "cleric", "inquisitor", BSkillLevel, Stat.Intelligence, 90, 16f, 0.6f, 7f, true, Magic, Projectile),
                Charged(Cone("final-verdict", "Final Verdict", "cleric", "inquisitor", BSecondSkillLevel, Stat.Intelligence, 180, 7f, 30f, 15f, Magic))),

            // --- Warlock (Intelligence, drain life), ticket 58 -------------------------------------------------
            D("warlock", "hexer", "Hexer", "Drains its enemies dry, up close.",
                new[] { OnSkill("hexer-touch", "Grasping Touch", "drain-touch", damage: 30), OnTag("hexer-flow", "Dark Flow", Magic, cooldown: -10) },
                Drain(Bolt("wither", "Wither", "warlock", "hexer", DSkillLevel, Stat.Intelligence, 50, 12f, 0.6f, 6f, false, Magic, Projectile), 30)),
            D("warlock", "shadowcaller", "Shadowcaller", "Calls the dark down on a crowd.",
                new[] { OnSkill("shadowcaller-rupture", "Wide Rupture", "void-rupture", radius: 1f), OnSkill("shadowcaller-bolts", "Twin Shadows", "shadow-bolt", extraProjectiles: 1) },
                Drain(Area("shadow-burst", "Shadow Burst", "warlock", "shadowcaller", DSkillLevel, Stat.Intelligence, 55, 4f, 9f, Magic, AreaTag), 15)),
            B("warlock", "hexer", "soul-reaper", "Soul Reaper", "Cuts down the weak and grows stronger for it.",
                new[] { OnSkill("soul-reaper-siphon", "Deep Siphon", "soul-siphon", damage: 35), OnTag("soul-reaper-hunger", "Hunger", Magic, damage: 10) },
                Drain(Cone("reap", "Reap", "warlock", "soul-reaper", BSkillLevel, Stat.Intelligence, 100, 3.5f, 140f, 8f, Magic), 35),
                Charged(Drain(Area("harvest-souls", "Harvest Souls", "warlock", "soul-reaper", BSecondSkillLevel, Stat.Intelligence, 120, 5f, 14f, Magic, AreaTag), 25))),
            B("warlock", "hexer", "blood-mage", "Blood Mage", "Spends blood freely, and always takes more back.",
                new[] { OnSkill("blood-mage-touch", "Quick Touch", "drain-touch", cooldown: -30), OnSkill("blood-mage-bolt", "Blood Bolt", "shadow-bolt", damage: 30) },
                Drain(Bolt("blood-lance", "Blood Lance", "warlock", "blood-mage", BSkillLevel, Stat.Intelligence, 110, 14f, 0.6f, 8f, true, Magic, Projectile), 20),
                Drain(Cone("crimson-tide", "Crimson Tide", "warlock", "blood-mage", BSecondSkillLevel, Stat.Intelligence, 95, 6f, 80f, 10f, Magic), 30)),
            B("warlock", "shadowcaller", "voidlord", "Voidlord", "Opens the void and lets it swallow the fight.",
                new[] { OnSkill("voidlord-rupture", "Deep Void", "void-rupture", damage: 30), OnTag("voidlord-area", "Spreading Dark", AreaTag, damage: 15) },
                Charged(Drain(Area("black-hole", "Black Hole", "warlock", "voidlord", BSkillLevel, Stat.Intelligence, 170, 6f, 18f, Magic, AreaTag), 10), 1.5f),
                Drain(Bolt("void-spike", "Void Spike", "warlock", "voidlord", BSecondSkillLevel, Stat.Intelligence, 85, 14f, 0.6f, 6f, false, Magic, Projectile), 15)),
            B("warlock", "shadowcaller", "doombringer", "Doombringer", "Every spell is a slow, certain end.",
                new[] { OnSkill("doombringer-wave", "Rolling Doom", "abyssal-wave", damage: 25, range: 2f), OnTag("doombringer-haste", "Dread Haste", Magic, cooldown: -15) },
                Drain(Bolt("doom", "Doom", "warlock", "doombringer", BSkillLevel, Stat.Intelligence, 140, 14f, 1.2f, 12f, false, Magic, Projectile), 10),
                Drain(Area("cataclysm", "Cataclysm", "warlock", "doombringer", BSecondSkillLevel, Stat.Intelligence, 130, 5.5f, 15f, Magic, AreaTag), 10)),

            // --- Summoner (Intelligence hits, Sense health), ticket 57 -----------------------------------------
            D("summoner", "beastcaller", "Beastcaller", "Fights beside a pack of beasts.",
                new[] { OnSkill("beastcaller-wolves", "Fierce Wolves", "call-wolves", damage: 30), OnTag("beastcaller-call", "Quick Call", Summoning, cooldown: -15) },
                Call("call-bear", "Call Bear", "beastcaller", DSkillLevel, 30, 200, 25f, 2f, 1.4f, 4f, 1, 18f)),
            D("summoner", "spiritbinder", "Spiritbinder", "Binds restless spirits to its will.",
                new[] { OnSkill("spiritbinder-lash", "Twin Lash", "spirit-lash", extraProjectiles: 1), OnTag("spiritbinder-bond", "Bond", Summoning, damage: 15) },
                Call("call-spirits", "Call Spirits", "spiritbinder", DSkillLevel, 12, 40, 12f, 4f, 1f, 6f, 3, 14f)),
            B("summoner", "beastcaller", "packmaster", "Packmaster", "Leads a pack that hunts as one.",
                new[] { OnSkill("packmaster-wolves", "Pack Call", "call-wolves", cooldown: -30), OnTag("packmaster-fangs", "Pack Fangs", Summoning, damage: 15) },
                Call("call-dire-wolves", "Call Dire Wolves", "packmaster", BSkillLevel, 35, 150, 20f, 1.8f, 0.8f, 6.5f, 2, 16f),
                Area("howl", "Howl", "summoner", "packmaster", BSecondSkillLevel, Stat.Intelligence, 70, 6f, 10f, Summoning, AreaTag)),
            B("summoner", "beastcaller", "golemancer", "Golemancer", "Raises giants of stone to stand in front of the party.",
                new[] { OnSkill("golemancer-golem", "Heavy Stone", "call-golem", damage: 35, cooldown: -20), OnTag("golemancer-quake", "Quake", AreaTag, damage: 15) },
                Call("call-colossus", "Call Colossus", "golemancer", BSkillLevel, 70, 600, 30f, 2.5f, 1.8f, 2.5f, 1, 30f),
                Area("tremor", "Tremor", "summoner", "golemancer", BSecondSkillLevel, Stat.Intelligence, 90, 5f, 11f, Summoning, AreaTag)),
            B("summoner", "spiritbinder", "necromancer", "Necromancer", "The fallen rise again, and fight for it.",
                new[] { OnSkill("necromancer-wraith", "Hungry Wraith", "call-wraith", damage: 30), OnTag("necromancer-call", "Grave Call", Summoning, cooldown: -15) },
                Call("raise-dead", "Raise Dead", "necromancer", BSkillLevel, 20, 80, 18f, 1.5f, 1f, 4.5f, 3, 18f),
                Bolt("bone-spear", "Bone Spear", "summoner", "necromancer", BSecondSkillLevel, Stat.Intelligence, 90, 14f, 0.6f, 7f, true, Summoning, Projectile)),
            B("summoner", "spiritbinder", "soulweaver", "Soulweaver", "Weaves spirits into storms and phantoms.",
                new[] { OnSkill("soulweaver-lash", "Spirit Fan", "spirit-lash", extraProjectiles: 2), OnTag("soulweaver-focus", "Spirit Focus", Projectile, damage: 15) },
                Area("soul-storm", "Soul Storm", "summoner", "soulweaver", BSkillLevel, Stat.Intelligence, 110, 5f, 12f, Summoning, AreaTag),
                Call("call-phantom", "Call Phantom", "soulweaver", BSecondSkillLevel, 45, 120, 20f, 7f, 1f, 5f, 1, 14f)),
        };

        /// <summary>Every specialty skill the builder knows: ticket 26's (in the main skill table) and ticket 54's.</summary>
        private static IEnumerable<SkillDefinition> SpecialtyTreeSkills => SpecialtyTreeData.SelectMany(s => s.Skills);

        /// <summary>Every specialty passive the builder knows, with its description filled in from its numbers.</summary>
        private static IEnumerable<ModifierDefinition> SpecialtyTreePassives
        {
            get
            {
                foreach (var s in SpecialtyTreeData)
                foreach (var m in s.Passives)
                {
                    if (string.IsNullOrEmpty(m.Description)) m.Description = $"{s.Name}: {Describe(m)}.";
                    yield return m;
                }
            }
        }

        /// <summary>What a modifier does, from its numbers: "melee skills deal 15% more damage", "Ground Slam reaches 1.5 m further".</summary>
        private static string Describe(ModifierDefinition m)
        {
            var many = m.AppliesToTag != null; // "melee skills deal", but "Ground Slam deals"
            var target = many ? $"{m.AppliesToTag} skills" : SkillName(m.AppliesToSkillId);
            string Verb(string plural, string single) => many ? plural : single;
            var parts = new List<string>();
            var heals = !many && SkillData.Concat(SpecialtyTreeSkills).FirstOrDefault(s => s.Id == m.AppliesToSkillId)?.Heals == true;
            if (m.DamagePercent > 0) parts.Add(heals ? $"restores {m.DamagePercent}% more health" : $"{Verb("deal", "deals")} {m.DamagePercent}% more damage");
            if (m.CooldownPercent != 0) parts.Add($"{Verb("have", "has")} {(many ? "" : "a ")}{System.Math.Abs(m.CooldownPercent)}% {(m.CooldownPercent < 0 ? "shorter" : "longer")} {Verb("cooldowns", "cooldown")}");
            if (m.RangeAdd != 0f) parts.Add($"{Verb("reach", "reaches")} {m.RangeAdd:0.#} m further");
            if (m.RadiusAdd != 0f) parts.Add($"{Verb("cover", "covers")} {m.RadiusAdd:0.#} m more around {Verb("them", "it")}");
            if (m.ExtraProjectiles > 0) parts.Add($"{Verb("fire", "fires")} {m.ExtraProjectiles} more {(m.ExtraProjectiles == 1 ? "bolt" : "bolts")} in a fan");
            if (m.ExplosionRadius > 0f) parts.Add($"{Verb("burst", "bursts")} on every hit, for {m.ExplosionDamagePercent}% of it within {m.ExplosionRadius:0.#} m");
            return $"{target} {string.Join(" and ", parts)}";
        }

        private static string SkillName(string id) =>
            SkillData.Concat(SpecialtyTreeSkills).FirstOrDefault(s => s.Id == id)?.Name ?? id;

        /// <summary>A specialty's description: its flavour, its passives, and its skills with their levels.</summary>
        private static string Describe(SpecialtyData s) =>
            $"{s.Flavour} {string.Join(" ", s.Passives.Select(p => char.ToUpperInvariant(Describe(p)[0]) + Describe(p).Substring(1) + "."))} " +
            $"New {(s.Skills.Length == 1 ? "skill" : "skills")}: {string.Join(", ", s.Skills.Select(k => $"{k.Name} (level {k.UnlockLevel})"))}.";

        /// <summary>
        /// Fills a class's tree on its asset, once: a class with no tree gets all of it, its trials, and the reward pool; a class whose
        /// tree has stubs gets those stubs filled. Entries the Inspector has already filled (any passive) are left as they are.
        /// </summary>
        private static void FillSpecialtyTree(ClassDefinitionAsset asset, GateDefinitionAsset[] trials, ModifierDefinitionAsset[] passiveAssets)
        {
            var entries = SpecialtyTreeData.Where(s => s.ClassId == asset.Id).ToList();
            if (entries.Count == 0) return;
            ModifierDefinitionAsset[] PassivesOf(SpecialtyData s) => s.Passives.Select(p => passiveAssets.First(a => a.Id == p.Id)).ToArray();

            if (asset.Specialties.Count == 0)
            {
                asset.SetSpecialties(trials, entries.Select(s => new ClassDefinitionAsset.Specialty
                {
                    id = s.Id, displayName = s.Name, rank = s.Rank, parent = s.Parent ?? "", description = Describe(s), passives = PassivesOf(s),
                }).ToArray());
                EditorUtility.SetDirty(asset);
                return;
            }
            var changed = false;
            foreach (var s in entries)
                changed |= asset.FillStub(s.Id, Describe(s), PassivesOf(s));
            if (asset.TrialGates.Count == 0) { asset.SetSpecialties(trials, asset.Specialties.ToArray()); changed = true; }
            if (changed) EditorUtility.SetDirty(asset);
        }
    }
}
