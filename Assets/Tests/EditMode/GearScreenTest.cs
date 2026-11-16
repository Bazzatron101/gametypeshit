using System.Collections.Generic;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The gear screen's words and lists (ticket 48): what a piece does in plain words, what changes
    /// when one piece replaces another, and which bag pieces show for a slot.
    /// </summary>
    public class GearScreenTest
    {
        private static readonly PlayerId P = new PlayerId(1);

        private static WeaponAttack Attack(string name, SkillEffectKind effect, int damage, float range = 0f, float radius = 0f,
            float arc = 120f, float cooldown = 0.5f, float windup = 0f, bool pierces = false) => new WeaponAttack
        {
            WindupSeconds = windup,
            Hit = new SkillDefinition
            {
                Id = name.ToLowerInvariant(), Name = name, Effect = effect, Damage = damage, Range = range, Radius = radius,
                ArcDegrees = arc, CooldownSeconds = cooldown, Pierces = pierces, ProjectileSpeed = 20f,
            },
        };

        private static readonly GearDefinition Sword = new GearDefinition
        {
            Id = "sword", Name = "Sword", Slot = GearSlot.Weapon,
            LightAttack = Attack("Slash", SkillEffectKind.Cone, 12, range: 3.2f, arc: 150f, cooldown: 0.35f),
            HeavyAttack = Attack("Thrust", SkillEffectKind.Cone, 30, range: 4.4f, arc: 35f, cooldown: 0.9f, windup: 0.3f),
        };

        private static readonly GearDefinition Bow = new GearDefinition
        {
            Id = "bow", Name = "Bow", Slot = GearSlot.Weapon,
            LightAttack = Attack("Shot", SkillEffectKind.Projectile, 9, range: 16f, cooldown: 0.45f),
            HeavyAttack = Attack("Drawn Shot", SkillEffectKind.Projectile, 26, range: 22f, cooldown: 1.2f, windup: 0.5f, pierces: true),
        };

        private static GearDefinition Piece(string id, GearSlot slot, params ModifierDefinition[] modifiers)
        {
            var gear = new GearDefinition { Id = id, Name = id, Slot = slot };
            gear.Modifiers.AddRange(modifiers);
            return gear;
        }

        [Test]
        public void AWeaponSaysWhatItsAttacksDo()
        {
            var lines = GearText.Effects(Sword);

            Assert.That(lines[0], Is.EqualTo("Light attack: Slash: 12 damage, 3.2 m reach, 150° wide, every 0.35 s"));
            Assert.That(lines[1], Is.EqualTo("Heavy attack: Thrust: 30 damage, 4.4 m reach, 35° wide, 0.3 s windup, every 0.9 s"));
            Assert.That(GearText.Effects(Bow)[1], Does.Contain("22 m range, pierces"));
        }

        [Test]
        public void AModifierReadsAsOneShortLine()
        {
            Assert.That(GearText.Modifier(new ModifierDefinition { Id = "m", AppliesToTag = "fire", DamagePercent = 15 }),
                Is.EqualTo("+15% damage to fire attacks and skills"));
            Assert.That(GearText.Modifier(new ModifierDefinition { Id = "m", AppliesToSkillId = "fire-bolt", ExtraProjectiles = 2 }),
                Is.EqualTo("+2 bolts to Fire Bolt"), "a skill's id made readable when no name is given");
            Assert.That(GearText.Modifier(new ModifierDefinition { Id = "m", AppliesToSkillId = "fire-bolt", CooldownPercent = -20 }, id => "Ember Bolt"),
                Is.EqualTo("-20% cooldown to Ember Bolt"));
            Assert.That(GearText.Modifier(new ModifierDefinition { Id = "m", AppliesToTag = "fire", DamagePercent = 10, PerWornTag = "fire" }),
                Does.EndWith(", for each fire piece you wear"));
        }

        [Test]
        public void ComparingWeaponsShowsTheAttacksSideBySide()
        {
            var lines = GearText.Compare(Bow, Sword);

            Assert.That(lines[0], Is.EqualTo("Light: Slash → Shot (12 → 9 damage, 3.2 → 16 m reach, every 0.35 → 0.45 s)"));
            Assert.That(lines[1], Does.StartWith("Heavy: Thrust → Drawn Shot (30 → 26 damage"));
        }

        [Test]
        public void ComparingArmourShowsWhatIsGainedAndLost()
        {
            var fireHood = Piece("Fire Hood", GearSlot.Head, new ModifierDefinition { Id = "a", AppliesToTag = "fire", DamagePercent = 15 });
            var quickHood = Piece("Quick Hood", GearSlot.Head, new ModifierDefinition { Id = "b", AppliesToTag = "melee", CooldownPercent = -10 });

            var lines = GearText.Compare(quickHood, fireHood);

            Assert.That(lines, Is.EqualTo(new[] { "+ -10% cooldown to melee attacks and skills", "− +15% damage to fire attacks and skills" }));
            Assert.That(GearText.Compare(quickHood, null)[0], Does.Contain("empty"), "nothing worn: only gains");
        }

        [Test]
        public void TheBagListsSparePiecesFilteredBySlot()
        {
            var hood = Piece("Hood", GearSlot.Head);
            var boots = Piece("Boots", GearSlot.Feet);
            var world = new SimulationWorld(new SimulationConfig { TickRate = 60, Gear = new[] { Sword, Bow, hood, boots } });
            world.AddPlayer(P, Vec3.Zero);
            world.GiveGear(P, "sword", equip: true);
            foreach (var id in new[] { "bow", "Hood", "Boots" }) world.GiveGear(P, id);
            world.Step(new Dictionary<PlayerId, PlayerCommand>());
            var state = world.GetPlayer(P);

            Assert.That(GearPanel.Listed(state, null).Select(g => g.Id), Is.EqualTo(new[] { "bow", "Hood", "Boots" }), "the worn sword is not a spare; by slot order");
            Assert.That(GearPanel.Listed(state, GearSlot.Weapon).Select(g => g.Id), Is.EqualTo(new[] { "bow" }));
            Assert.That(GearPanel.Listed(state, GearSlot.Hands), Is.Empty);
        }
    }
}
