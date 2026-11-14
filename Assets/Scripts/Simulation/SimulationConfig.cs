using System;
using System.Collections.Generic;

namespace HunterSystem.Simulation
{
    /// <summary>Tunable simulation values. Configuration, not code constants.</summary>
    public sealed class SimulationConfig
    {
        /// <summary>Simulation ticks per second.</summary>
        public int TickRate { get; set; } = 60;

        /// <summary>Player movement speed in metres per second at full input.</summary>
        public float PlayerMoveSpeed { get; set; } = 5f;

        /// <summary>Holding sprint: move speed as a percentage of walking.</summary>
        public int SprintSpeedPercent { get; set; } = 160;

        /// <summary>A base jump's peak, in metres. Too low to clear an enemy; classes raise it per player.</summary>
        public float JumpHeight { get; set; } = 0.9f;

        /// <summary>How long a jump lasts, take-off to landing.</summary>
        public float JumpSeconds { get; set; } = 0.6f;

        public int PlayerMaxHealth { get; set; } = 100;

        /// <summary>Move speed while charging a skill, as a percentage of normal.</summary>
        public int ChargeMoveSpeedPercent { get; set; } = 40;

        /// <summary>
        /// Holding a key shorter than this still counts as a tap: the charge only starts building
        /// once the grace has passed, so a quick press fires the normal skill.
        /// </summary>
        public float ChargeTapGraceSeconds { get; set; } = 0.15f;

        public AttackDefinition LightAttack { get; set; } = new AttackDefinition();

        public AttackDefinition HeavyAttack { get; set; } = new AttackDefinition
        {
            Damage = 25, Range = 2.5f, ArcDegrees = 90f, WindupSeconds = 0.35f, CooldownSeconds = 1f,
        };

        public SoftLockOnDefinition SoftLockOn { get; set; } = new SoftLockOnDefinition();

        public DodgeDefinition Dodge { get; set; } = new DodgeDefinition();

        public ProgressionDefinition Progression { get; set; } = new ProgressionDefinition();

        public StatEffectsDefinition StatEffects { get; set; } = new StatEffectsDefinition();

        /// <summary>Tags skills and modifiers may use; anything else is rejected as a typo.</summary>
        public IReadOnlyCollection<string> KnownTags { get; set; } = SkillTags.Standard;

        /// <summary>Every skill in the game. Starters (unlock level 1) fill the bar in this order.</summary>
        public IReadOnlyList<SkillDefinition> Skills { get; set; } = Array.Empty<SkillDefinition>();

        /// <summary>Every gate in the game. Like skills, gates are named by id in snapshots, so host and clients must hold the same list.</summary>
        public IReadOnlyList<GateDefinition> Gates { get; set; } = Array.Empty<GateDefinition>();

        /// <summary>The hub the party gathers in between gates, or null for a game without one (tests, the arena).</summary>
        public HubDefinition Hub { get; set; }

        /// <summary>Party-size scaling of the enemies that gates spawn (ticket 27). The default changes nothing.</summary>
        public PartyScalingDefinition PartyScaling { get; set; } = new PartyScalingDefinition();

        /// <summary>Text-only story scenes and what makes each one play (ticket 20).</summary>
        public IReadOnlyList<StorySceneDefinition> StoryScenes { get; set; } = Array.Empty<StorySceneDefinition>();
    }
}
