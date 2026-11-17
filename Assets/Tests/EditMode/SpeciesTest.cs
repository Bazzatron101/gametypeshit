using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEditor;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Species (ticket 81, ADR 0024). Every hunter is an Australian animal: the species adds to one stat (+2 at
    /// E-rank, +1 more each rank, never saved into the stats) and brings one small perk. It is chosen once and
    /// saved; a hunter from before species gets one without losing anything; snapshots carry it.
    /// </summary>
    public class SpeciesTest
    {
        private static readonly PlayerId P = new PlayerId(1);

        private static readonly SkillDefinition Smash = new SkillDefinition
        {
            Id = "smash", Name = "Smash", Effect = SkillEffectKind.Cone, Damage = 100, Range = 4f, ArcDegrees = 160f,
            CooldownSeconds = 5f, Tags = new[] { "melee", "physical" },
        };

        private static EnemyDefinition Dummy(int health = 100000) => new EnemyDefinition
        {
            Name = "Dummy", MaxHealth = health, MoveSpeed = 0f, StopDistance = 1f, AttackDamage = 0, AttackRange = 0f, XpReward = 0,
        };

        private static SimulationConfig Config() => new SimulationConfig
        {
            TickRate = 60,
            PlayerMaxHealth = 1000,
            SoftLockOn = new SoftLockOnDefinition { Enabled = false },
            Skills = new[] { Smash },
        };

        private static SimulationWorld World(string speciesId, SimulationConfig config = null, Vec3? at = null)
        {
            var world = new SimulationWorld(config ?? Config());
            world.AddPlayer(P, at ?? Vec3.Zero, new CharacterRecord { IsNew = true, SpeciesId = speciesId, Name = "Tester" });
            Step(world);
            return world;
        }

        private static List<SimulationEvent> Step(SimulationWorld world, int ticks = 1, PlayerCommand? first = null)
        {
            var all = new List<SimulationEvent>();
            for (var i = 0; i < ticks; i++)
            {
                var commands = new Dictionary<PlayerId, PlayerCommand>();
                if (i == 0 && first.HasValue) commands[P] = first.Value;
                world.Step(commands);
                all.AddRange(world.Events);
            }
            return all;
        }

        private static PlayerState Me(SimulationWorld world) => world.GetPlayer(P);

        private static readonly PlayerCommand UseSmash = new PlayerCommand(0f, 0f, useSkillSlot: 0);

        // --- The stat bonus -------------------------------------------------------------------------------

        [Test]
        public void TheBonusIsTwoAtERankAndGrowsByOneAtEachPromotion()
        {
            var world = World("kangaroo");
            var own = Me(world).Stats[Stat.Strength];
            Assert.That(Me(world).SpeciesStats[Stat.Strength], Is.EqualTo(2), "E-rank");
            Assert.That(Me(world).TotalStat(Stat.Strength), Is.EqualTo(own + 2));
            Assert.That(Me(world).SpeciesStats[Stat.Agility], Is.Zero, "only the species' own stat");

            world.RaiseToLevel(P, 16);
            Step(world);
            Assert.That(Me(world).SpeciesStats[Stat.Strength], Is.EqualTo(3), "D-rank");
            world.RaiseToLevel(P, 36);
            Step(world);
            Assert.That(Me(world).SpeciesStats[Stat.Strength], Is.EqualTo(4), "C-rank");
            world.RaiseToLevel(P, 95); // the level cap: A-rank (S is above it until the S-rank story raises the cap)
            Step(world);
            Assert.That(Me(world).SpeciesStats[Stat.Strength], Is.EqualTo(6), "A-rank: +2 and four promotions");

            var saved = world.CaptureCharacter(P);
            Assert.That(saved.Stats[(int)Stat.Strength] - own, Is.EqualTo(Me(world).Stats[Stat.Strength] - own), "never saved into the stats");
            Assert.That(saved.SpeciesId, Is.EqualTo("kangaroo"));
        }

        [Test]
        public void AVitalitySpeciesHasMoreHealthAndMoreAtTheNextRank()
        {
            var config = Config();
            var none = World(null, config);
            var wombat = World("wombat", config);
            Assert.That(Me(wombat).MaxHealth - Me(none).MaxHealth, Is.EqualTo(2 * config.StatEffects.HealthPerVitality));
            Assert.That(Me(wombat).Health, Is.EqualTo(Me(wombat).MaxHealth), "arrives at full health");

            wombat.RaiseToLevel(P, 16);
            none.RaiseToLevel(P, 16);
            Step(wombat);
            Step(none);
            Assert.That(Me(wombat).MaxHealth - Me(none).MaxHealth, Is.EqualTo(3 * config.StatEffects.HealthPerVitality), "the promotion raised it");
        }

        // --- Choosing, saving and snapshots ----------------------------------------------------------------

        [Test]
        public void TheSpeciesIsSavedAndAHunterFromBeforeSpeciesLoadsWithNone()
        {
            var record = new CharacterRecord { IsNew = false, Name = "Ayla", ClassId = "warrior", SpeciesId = "koala", Level = 9 };
            Assert.That(CharacterCodec.Decode(CharacterCodec.Encode(record)).SpeciesId, Is.EqualTo("koala"));
            var old = CharacterCodec.Decode(CharacterCodec.EncodeVersion7ForTests(record));
            Assert.That(old.SpeciesId, Is.Null, "a format 7 save has no species: the game asks for one");
            Assert.That(old.Level, Is.EqualTo(9));
        }

        [Test]
        public void AHunterFromBeforeSpeciesKeepsClassAndProgressAndTheChoiceIsPermanent()
        {
            var old = new CharacterRecord { Name = "Brann", ClassId = "archer", Level = 12, Xp = 40, Gold = 300, Stats = new[] { 12, 13, 14, 15, 16, 17 } };
            var upgraded = old.WithSpecies(SpeciesDefinition.Standard().First(s => s.Id == "platypus"));
            Assert.That(upgraded.SpeciesId, Is.EqualTo("platypus"));
            Assert.That(upgraded.ClassId, Is.EqualTo("archer"));
            Assert.That(upgraded.Level, Is.EqualTo(12));
            Assert.That(upgraded.Xp, Is.EqualTo(40));
            Assert.That(upgraded.Gold, Is.EqualTo(300));
            Assert.That(upgraded.Stats, Is.EqualTo(old.Stats), "the bonus is worked out, not stored");
            Assert.That(old.SpeciesId, Is.Null, "the original is left alone");
            Assert.Throws<InvalidOperationException>(() => upgraded.WithSpecies(SpeciesDefinition.Standard()[0]));
            var classed = new CharacterRecord { IsNew = true, SpeciesId = "quokka" }.WithClass(new ClassDefinition { Id = "rogue" }, new ProgressionDefinition());
            Assert.That(classed.SpeciesId, Is.EqualTo("quokka"), "picking the class afterwards keeps the species");
        }

        [Test]
        public void SnapshotsCarryEachPlayersSpecies()
        {
            var host = World("cockatoo");
            var client = new SimulationWorld(Config());
            client.RestoreSnapshot(host.CaptureSnapshot());
            Assert.That(client.GetPlayer(P).Species.Id, Is.EqualTo("cockatoo"));
            Assert.That(client.GetPlayer(P).SpeciesStats[Stat.Intelligence], Is.EqualTo(2));
        }

        [Test]
        public void AnUnknownSpeciesInASaveIsDroppedAndTheContentIsChecked()
        {
            var world = World("bunyip");
            Assert.That(Me(world).Species, Is.Null, "one the game does not have");
            var twice = Config();
            twice.Species = new[] { new SpeciesDefinition { Id = "emu" }, new SpeciesDefinition { Id = "emu" } };
            Assert.Throws<ArgumentException>(() => new SimulationWorld(twice));
            var negative = Config();
            negative.Species = new[] { new SpeciesDefinition { Id = "emu", PerkAmount = -1f } };
            Assert.Throws<ArgumentException>(() => new SimulationWorld(negative));
        }

        // --- The perks -------------------------------------------------------------------------------------

        [Test]
        public void KangarooHitsPushOrdinaryEnemiesBack()
        {
            Vec3 PushedTo(string species)
            {
                var world = World(species);
                world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2f));
                Step(world, 30, UseSmash);
                return world.GetEnemy(world.EnemyIds[0]).Position;
            }
            Assert.That(PushedTo("wombat").Z, Is.EqualTo(2f).Within(0.01f), "others do not push");
            Assert.That(PushedTo("kangaroo").Z, Is.EqualTo(3.2f).Within(0.05f), "1.2 m straight away");
        }

        [Test]
        public void TasmanianDevilHitsHarderOnBadlyHurtEnemies()
        {
            int Hit(string species, int enemyHealth)
            {
                var world = World(species);
                world.SpawnEnemy(new EnemyDefinition { Name = "Dummy", MaxHealth = 1000, MoveSpeed = 0f, AttackDamage = 0, AttackRange = 0f, XpReward = 0 }, new Vec3(0f, 0f, 2f));
                Step(world);
                var id = world.EnemyIds[0];
                world.DamageEnemy(id, P, 1000 - enemyHealth);
                Step(world);
                world.DamageEnemy(id, P, 100);
                return Step(world).OfType<DamageDealt>().Single().Amount;
            }
            Assert.That(Hit("tasmanian-devil", 1000), Is.EqualTo(100), "a healthy enemy");
            Assert.That(Hit("tasmanian-devil", 300), Is.EqualTo(115), "at 30% health: 15% more");
            Assert.That(Hit("kangaroo", 300), Is.EqualTo(100));
        }

        [Test]
        public void SugarGlidersComeDownFromAJumpMoreSlowly()
        {
            int AirTicks(string species)
            {
                var world = World(species);
                Step(world, 1, new PlayerCommand(0f, 0f, jump: true));
                var ticks = 1;
                while (Me(world).IsAirborne && ticks < 600) { Step(world); ticks++; }
                return ticks;
            }
            var normal = AirTicks("kangaroo");
            var glider = AirTicks("sugar-glider");
            Assert.That(glider, Is.GreaterThan(normal), "a glide");
            Assert.That(glider, Is.EqualTo(normal / 2 + (int)Math.Round(normal / 2.0 * 1.6)).Within(2), "the way down 60% longer");
        }

        [Test]
        public void WombatsTakeLessDamageFromBehind()
        {
            int Taken(string species, float enemyZ)
            {
                var world = World(species);
                world.SpawnEnemy(new EnemyDefinition
                {
                    Name = "Biter", MaxHealth = 100, MoveSpeed = 0f, StopDistance = 1f, AttackDamage = 100, AttackRange = 2f,
                    AttackWindupSeconds = 0.1f, AttackCooldownSeconds = 10f, XpReward = 0,
                }, new Vec3(0f, 0f, enemyZ));
                return Step(world, 30).OfType<PlayerDamaged>().First().Amount;
            }
            Assert.That(Taken("wombat", 1f), Is.EqualTo(100), "from the front: all of it");
            Assert.That(Taken("wombat", -1f), Is.EqualTo(80), "from behind: 20% less");
            Assert.That(Taken("koala", -1f), Is.EqualTo(100));
        }

        [Test]
        public void KoalasHealSlowlyOnlyAfterAQuietSpell()
        {
            var koala = World("koala");
            var other = World("kangaroo");
            koala.DamagePlayerForTesting(P, 500);
            other.DamagePlayerForTesting(P, 500);
            Step(koala);
            Step(other);
            var hurt = Me(koala).Health;
            Step(koala, 60 * 3);
            Step(other, 60 * 3);
            Assert.That(Me(koala).Health - hurt, Is.InRange(2 * Me(koala).MaxHealth / 100, 3 * Me(koala).MaxHealth / 100 + 1), "1% a second");
            Assert.That(Me(other).Health, Is.EqualTo(Me(other).MaxHealth - 500), "others do not");

            koala.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2f));
            Step(koala, 30, UseSmash); // a fight
            var afterFight = Me(koala).Health;
            Step(koala, 60 * 5);
            Assert.That(Me(koala).Health, Is.EqualTo(afterFight), "nothing for the first six quiet seconds");
            Step(koala, 60 * 2);
            Assert.That(Me(koala).Health, Is.GreaterThan(afterFight), "then it starts again");
        }

        [Test]
        public void CockatooSkillsCoolDownFaster()
        {
            int Cooldown(string species)
            {
                var world = World(species);
                world.SpawnEnemy(Dummy(), new Vec3(0f, 0f, 2f));
                Step(world, 2, UseSmash);
                return Me(world).SkillSlots[0].CooldownTicks;
            }
            Assert.That(Cooldown("kangaroo"), Is.EqualTo(300));
            Assert.That(Cooldown("cockatoo"), Is.EqualTo(276), "8% faster");
        }

        [Test]
        public void PlatypusesFindHiddenGatesFromFurtherAway()
        {
            bool Finds(string species)
            {
                var overworld = new OverworldDefinition { EntranceRadius = 3f, FindRadius = 20f };
                overworld.StartingRegions.Add("green");
                overworld.Gates.Add(new GateSite { GateId = "den", Entrance = new Vec3(100f, 0f, 0f), Region = "green" });
                var config = Config();
                config.Gates = new[] { new GateDefinition { Id = "den", Name = "Den", Rooms = { new RoomDefinition() } } };
                config.Hub = new HubDefinition { Centre = new Vec3(-200f, 0f, 0f) };
                config.Overworld = overworld;
                var world = World(species, config, new Vec3(75f, 0f, 0f)); // 25 m away
                Step(world, 2);
                return world.IsGateFound("den");
            }
            Assert.That(Finds("kangaroo"), Is.False, "20 m for others");
            Assert.That(Finds("platypus"), Is.True, "50% further: 30 m");
        }

        [Test]
        public void QuokkasDrawWithMoreLuck()
        {
            Assert.That(World("kangaroo").DropLuck(P), Is.Zero);
            Assert.That(World("quokka").DropLuck(P), Is.EqualTo(2 + 5), "the species' Luck bonus plus the perk's five");
        }

        // --- Bots and the game's content -------------------------------------------------------------------

        [Test]
        public void BotTeammatesGetASpeciesThatSuitsTheirRole()
        {
            var config = new SimulationConfig();
            Assert.That(CompanionParty.SpeciesFor("warrior", config), Is.EqualTo("wombat"));
            Assert.That(CompanionParty.SpeciesFor("cleric", config), Is.EqualTo("koala"));
            Assert.That(CompanionParty.SpeciesFor("wizard", config), Is.EqualTo("cockatoo"));
            Assert.That(CompanionParty.SpeciesFor("archer", config), Is.EqualTo("platypus"));
            config.Species = Array.Empty<SpeciesDefinition>();
            Assert.That(CompanionParty.SpeciesFor("warrior", config), Is.Null, "a game without species");
        }

        [Test]
        public void TheGameHasEightAustralianSpeciesAndEveryClassHasTwoThatSuitIt()
        {
            var content = AssetDatabase.FindAssets("t:GameContentAsset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameContentAsset>(AssetDatabase.GUIDToAssetPath(guid))).Single();
            var config = new SimulationConfig();
            content.ApplyTo(config);
            Assert.That(config.Species.Select(s => s.Id), Is.EquivalentTo(new[]
                { "kangaroo", "tasmanian-devil", "sugar-glider", "wombat", "koala", "cockatoo", "platypus", "quokka" }));
            Assert.That(config.Species, Has.All.Matches<SpeciesDefinition>(s => s.Perk != SpeciesPerk.None && !string.IsNullOrEmpty(s.Name) && !string.IsNullOrEmpty(s.Description)));
            Assert.That(config.Species.Select(s => s.Perk).Distinct().Count(), Is.EqualTo(8), "a perk each");
            Assert.DoesNotThrow(() => new SimulationWorld(config));

            foreach (var c in config.Classes)
            {
                var suits = config.Species.Count(s => c.StatPerLevel((int)s.BonusStat) > 0);
                Assert.That(suits, Is.GreaterThanOrEqualTo(2), $"{c.Name}: no species is the only right pick");
            }
        }
    }
}
