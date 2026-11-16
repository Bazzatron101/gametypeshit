using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Specialties (ticket 26): a tree in two tiers. Each tier has a trial, a gate the guild posts at the tier's rank
    /// (D, then B). Clearing it as a hunter of that class and rank passes it; the hunter then picks one of that tier's
    /// specialties, for good. At B only the branches under the hunter's D pick are offered. A specialty brings its own
    /// skills at their levels and passive modifiers on the class's skills. Trials and picks are the character's,
    /// saved with it and carried in snapshots.
    /// </summary>
    public class SpecialtyTest
    {
        private static readonly PlayerId P = new PlayerId(1);
        private static readonly PlayerId Guest = new PlayerId(2);

        private static readonly Vec3 Town = new Vec3(-80f, 0f, 0f);

        // The bands below: E 1, D 3, C 5, B 7.
        private const int DRankLevel = 3;
        private const int BRankLevel = 7;

        private static GateDefinition OneRoomGate(string id, string rank) => new GateDefinition
        {
            Id = id, Name = id, Rank = rank,
            Rooms = { new RoomDefinition { Width = 10f, Length = 10f } },
        };

        private static SkillDefinition Skill(string id, string classId, int unlock = 1, string specialty = null) => new SkillDefinition
        {
            Id = id, Name = id, Effect = SkillEffectKind.Area, Damage = 50, Radius = 4f, CooldownSeconds = 1f, UnlockLevel = unlock,
            ClassId = classId, SpecialtyId = specialty, Tags = new[] { SkillTags.Melee, SkillTags.Physical },
        };

        private static SpecialtyDefinition Specialty(string id, string rank, string parent = null, int meleePercent = 0)
        {
            var s = new SpecialtyDefinition { Id = id, Name = id, ClassId = "warrior", Rank = rank, ParentId = parent };
            if (meleePercent != 0) s.Passives.Add(new ModifierDefinition { Id = id + "-passive", Name = id, AppliesToTag = SkillTags.Melee, DamagePercent = meleePercent });
            return s;
        }

        /// <summary>
        /// The Warrior's tree: Vanguard (built, by its skill) and Sentinel (a stub) at D; under Vanguard, Berserker (built)
        /// and Champion (a stub); under Sentinel, Fortress (built, so the tree's rule is what hides it from a Vanguard).
        /// </summary>
        private static ClassDefinition Warrior()
        {
            var warrior = new ClassDefinition { Id = "warrior", Name = "Warrior" };
            warrior.TrialGateIds.Add("warrior-trial-d");
            warrior.TrialGateIds.Add("warrior-trial-b");
            warrior.Specialties.Add(Specialty("vanguard", "D"));
            warrior.Specialties.Add(Specialty("sentinel", "D"));
            warrior.Specialties.Add(Specialty("berserker", "B", "vanguard", meleePercent: 20));
            warrior.Specialties.Add(Specialty("champion", "B", "vanguard"));
            warrior.Specialties.Add(Specialty("fortress", "B", "sentinel", meleePercent: 5));
            return warrior;
        }

        private static ClassDefinition Wizard()
        {
            var wizard = new ClassDefinition { Id = "wizard", Name = "Wizard" }; // no trials yet
            wizard.Specialties.Add(new SpecialtyDefinition { Id = "pyromancer", Name = "Pyromancer", ClassId = "wizard", Rank = "D" });
            return wizard;
        }

        private static SimulationConfig Config() => new SimulationConfig
        {
            TickRate = 60,
            Progression = new ProgressionDefinition
            {
                Ranks = new[]
                {
                    new RankDefinition { Id = "E", FirstLevel = 1 },
                    new RankDefinition { Id = "D", FirstLevel = DRankLevel },
                    new RankDefinition { Id = "C", FirstLevel = 5 },
                    new RankDefinition { Id = "B", FirstLevel = BRankLevel },
                },
            },
            Classes = new[] { Warrior(), Wizard() },
            Skills = new[]
            {
                Skill("slam", "warrior"),
                Skill("smash", "warrior", unlock: DRankLevel, specialty: "vanguard"),
                Skill("cleave", "warrior", unlock: BRankLevel, specialty: "berserker"),
                Skill("earthsplitter", "warrior", unlock: 9, specialty: "berserker"),
                Skill("fireball", "wizard"),
            },
            Gates = new[] { OneRoomGate("mill", "E"), OneRoomGate("warrior-trial-d", "D"), OneRoomGate("warrior-trial-b", "B") },
            Hub = new HubDefinition { Centre = Town, Width = 20f, Length = 16f, BoardOffset = new Vec3(0f, 0f, 6f), ReturnCountdownSeconds = 2f },
        };

        private static CharacterRecord NewHunter(string classId) => new CharacterRecord { IsNew = true, ClassId = classId };

        /// <summary>A world in the hub with one hunter of <paramref name="classId"/> at <paramref name="level"/>.</summary>
        private static SimulationWorld World(string classId = "warrior", int level = DRankLevel, SimulationConfig config = null)
        {
            var world = new SimulationWorld(config ?? Config());
            world.AddPlayer(P, world.HubJoinPosition(P), NewHunter(classId));
            world.SetWorldOwner(P);
            if (level > 1) world.RaiseToLevel(P, level);
            Step(world);
            return world;
        }

        private static List<SimulationEvent> Step(SimulationWorld world, int ticks = 1, PlayerCommand command = default)
        {
            var all = new List<SimulationEvent>();
            for (var i = 0; i < ticks; i++)
            {
                var commands = new Dictionary<PlayerId, PlayerCommand>();
                foreach (var id in world.PlayerIds) commands[id] = i == 0 ? command : PlayerCommand.None;
                world.Step(commands);
                all.AddRange(world.Events);
            }
            return all;
        }

        private static List<SimulationEvent> Pick(SimulationWorld world, string specialtyId) =>
            Step(world, 1, PlayerCommand.None.WithChooseSpecialty(specialtyId));

        /// <summary>Runs a trial and brings the party home.</summary>
        private static List<SimulationEvent> Clear(SimulationWorld world, string trial)
        {
            Assert.That(world.IsOnBoard(trial), Is.True, "the board offers it");
            world.StartGate(trial); // as the board does once everyone is ready
            var events = Step(world, 10);
            Assert.That(events.OfType<GateCleared>().Select(g => g.GateId), Is.EqualTo(new[] { trial }), "an empty one-room gate clears at once");
            world.LeaveGate();
            events.AddRange(Step(world, 2));
            Assert.That(world.InHub, Is.True);
            return events;
        }

        /// <summary>A Warrior with Vanguard picked, at <paramref name="level"/>.</summary>
        private static SimulationWorld Vanguard(int level = BRankLevel)
        {
            var world = World(level: level);
            world.PassTrialForTesting(P);
            Step(world);
            Pick(world, "vanguard");
            Assert.That(world.GetPlayer(P).Specialties.Select(s => s.Id), Is.EqualTo(new[] { "vanguard" }));
            return world;
        }

        private static IEnumerable<string> Known(SimulationWorld world, PlayerId id) => world.GetPlayer(id).KnownSkills.Select(s => s.Id);

        private static IEnumerable<string> Picks(SimulationWorld world, PlayerId id) => world.GetPlayer(id).Specialties.Select(s => s.Id);

        // --- The trials ----------------------------------------------------------------------------------

        [Test]
        public void EachTrialIsPostedOnTheBoardAtItsRank()
        {
            var world = World(level: 1);
            Assert.That(world.IsOnBoard("warrior-trial-d"), Is.False, "an E-rank world");

            world.RaiseToLevel(P, DRankLevel);
            Step(world);
            Assert.That(world.IsOnBoard("warrior-trial-d"), Is.True, "the guild posts it: it has no site on the map");
            Assert.That(world.IsOnBoard("warrior-trial-b"), Is.False);

            world.RaiseToLevel(P, BRankLevel);
            Step(world);
            Assert.That(world.IsOnBoard("warrior-trial-b"), Is.True);
        }

        [Test]
        public void ClearingTheDTrialAsADRankWarriorUnlocksTheDPick()
        {
            var world = World();
            Assert.That(world.GetPlayer(P).CanChooseSpecialty, Is.False, "not before the trial");

            var events = Clear(world, "warrior-trial-d");

            Assert.That(events.OfType<SpecialtyUnlocked>().Select(e => (e.Player, e.ClassId, e.Rank)), Is.EqualTo(new[] { (P, "warrior", "D") }));
            Assert.That(world.GetPlayer(P).SpecialtyPickWaiting, Is.EqualTo("D"));
            Assert.That(world.SpecialtyOptions(P).Select(s => s.Id), Is.EqualTo(new[] { "vanguard", "sentinel" }));
        }

        [Test]
        public void TheBTrialIsForAHunterWithTheirDSpecialty()
        {
            // A B-rank Warrior who never took the D trial: the B branches grow from a D pick, so the B trial passes nothing.
            var unpicked = World(level: BRankLevel);
            Assert.That(Clear(unpicked, "warrior-trial-b").OfType<SpecialtyUnlocked>(), Is.Empty);

            var vanguard = Vanguard();
            var events = Clear(vanguard, "warrior-trial-b");
            Assert.That(events.OfType<SpecialtyUnlocked>().Select(e => e.Rank), Is.EqualTo(new[] { "B" }));
            Assert.That(vanguard.GetPlayer(P).SpecialtyPickWaiting, Is.EqualTo("B"));
        }

        [Test]
        public void AtBOnlyTheBranchesUnderTheDPickAreOffered()
        {
            var world = Vanguard();
            Clear(world, "warrior-trial-b");

            Assert.That(world.SpecialtyOptions(P).Select(s => s.Id), Is.EqualTo(new[] { "berserker", "champion" }));
            Assert.That(Pick(world, "fortress").OfType<SpecialtyChosen>(), Is.Empty, "Sentinel's branch, not Vanguard's");
            Assert.That(Pick(world, "berserker").OfType<SpecialtyChosen>().Select(e => e.SpecialtyId), Is.EqualTo(new[] { "berserker" }));
            Assert.That(Picks(world, P), Is.EqualTo(new[] { "vanguard", "berserker" }), "one pick per tier, lowest first");
            Assert.That(world.GetPlayer(P).CanChooseSpecialty, Is.False, "the tree is complete");
        }

        [Test]
        public void AHunterBelowTheTrialsRankDoesNotPassIt()
        {
            // The host's world is D-rank, so the board offers the D trial; the guest is an E-rank Warrior.
            var world = World();
            world.AddPlayer(Guest, world.HubJoinPosition(Guest), NewHunter("warrior"));
            Step(world);

            Clear(world, "warrior-trial-d");

            Assert.That(world.GetPlayer(P).CanChooseSpecialty, Is.True, "the D-rank host passes");
            Assert.That(world.GetPlayer(Guest).PassedTrials, Is.Empty, "the trial is for D-rank hunters");
        }

        [Test]
        public void OnlyTheTrialsOwnClassPassesIt()
        {
            var world = World();
            world.AddPlayer(Guest, world.HubJoinPosition(Guest), NewHunter("wizard"));
            world.RaiseToLevel(Guest, DRankLevel);
            Step(world);

            var events = Clear(world, "warrior-trial-d");

            Assert.That(events.OfType<SpecialtyUnlocked>().Select(e => e.Player), Is.EqualTo(new[] { P }), "a Wizard who helps a Warrior through the Warrior's trial gains nothing from it");
        }

        [Test]
        public void AGuestWarriorPassesTheTrialInTheHostsWorld()
        {
            // Trials belong to the character, not the world: a guest earns them like the host.
            var world = World("wizard");
            world.AddPlayer(Guest, world.HubJoinPosition(Guest), NewHunter("warrior"));
            world.RaiseToLevel(Guest, DRankLevel);
            Step(world);

            Clear(world, "warrior-trial-d");

            Assert.That(world.GetPlayer(Guest).CanChooseSpecialty, Is.True);
            Assert.That(world.CaptureCharacter(Guest).PassedTrials, Is.EqualTo(new[] { "D" }), "and takes it home in their own save");
        }

        [Test]
        public void ClearingAnotherGateIsNoTrial()
        {
            var world = World();
            world.StartGate("mill");
            var events = Step(world, 10);
            Assert.That(events.OfType<GateCleared>().Count(), Is.EqualTo(1));
            Assert.That(events.OfType<SpecialtyUnlocked>(), Is.Empty);
        }

        // --- The picks ---------------------------------------------------------------------------------

        [Test]
        public void APickBringsItsSkillsAtTheirLevels()
        {
            var world = Vanguard(level: BRankLevel);
            Assert.That(Known(world, P), Has.Member("smash"), "the D skill, at once");
            Clear(world, "warrior-trial-b");

            var events = Pick(world, "berserker");

            Assert.That(events.OfType<SpecialtyChosen>().Select(e => e.SpecialtyId), Is.EqualTo(new[] { "berserker" }));
            Assert.That(events.OfType<SkillUnlocked>().Select(e => e.Skill.Id), Is.EqualTo(new[] { "cleave" }), "the skill the hunter's level has reached, at once");
            Assert.That(Known(world, P), Has.No.Member("earthsplitter"), "a level-9 skill");

            world.RaiseToLevel(P, 9);
            var later = Step(world);
            Assert.That(later.OfType<SkillUnlocked>().Select(e => e.Skill.Id), Is.EqualTo(new[] { "earthsplitter" }), "learned at its level, like any skill");
        }

        [Test]
        public void WithoutTheSpecialtiesTheirSkillsAreNeverLearned()
        {
            var world = World(level: 12);
            Assert.That(Known(world, P), Is.EqualTo(new[] { "slam" }), "a Warrior past every unlock level, with no specialty");
        }

        [Test]
        public void ThePassivesChangeTheClasssSkills()
        {
            var plain = Vanguard();
            var berserker = Vanguard();
            Clear(berserker, "warrior-trial-b");
            Pick(berserker, "berserker");

            var plainHit = HitWithSlam(plain);
            var berserkerHit = HitWithSlam(berserker);

            Assert.That(berserkerHit, Is.EqualTo(plainHit * 1.2).Within(1), "Berserker: melee skills deal 20% more");
        }

        private static int HitWithSlam(SimulationWorld world)
        {
            var at = world.GetPlayer(P).Position + new Vec3(0f, 0f, 2f);
            world.SpawnEnemy(new EnemyDefinition { Name = "Dummy", MaxHealth = 100000, MoveSpeed = 0f, AttackRange = 0f }, at);
            var slot = world.GetPlayer(P).SkillSlots.ToList().FindIndex(s => s.Skill?.Id == "slam");
            var events = Step(world, 40, new PlayerCommand(0f, 0f, useSkillSlot: slot));
            return events.OfType<DamageDealt>().First(d => !d.IsSecondary).Amount;
        }

        [Test]
        public void APickIsPermanent()
        {
            var world = Vanguard(level: DRankLevel);

            var again = Pick(world, "sentinel");

            Assert.That(again.OfType<SpecialtyChosen>(), Is.Empty);
            Assert.That(Picks(world, P), Is.EqualTo(new[] { "vanguard" }));
            Assert.That(world.GetPlayer(P).CanChooseSpecialty, Is.False);
        }

        [Test]
        public void APickThatIsNotAllowedIsIgnored()
        {
            var world = World(level: BRankLevel);
            Assert.That(Pick(world, "vanguard").OfType<SpecialtyChosen>(), Is.Empty, "before the trial");

            world.PassTrialForTesting(P);
            Step(world);
            Assert.That(Pick(world, "sentinel").OfType<SpecialtyChosen>(), Is.Empty, "a stub with nothing in it yet");
            Assert.That(Pick(world, "berserker").OfType<SpecialtyChosen>(), Is.Empty, "a B specialty at the D pick");
            Assert.That(Pick(world, "pyromancer").OfType<SpecialtyChosen>(), Is.Empty, "another class's specialty");
            Assert.That(Pick(world, "no-such-thing").OfType<SpecialtyChosen>(), Is.Empty);

            world.StartGate("mill");
            Step(world);
            Assert.That(Pick(world, "vanguard").OfType<SpecialtyChosen>(), Is.Empty, "not in a gate");
            Assert.That(world.GetPlayer(P).SpecialtyPickWaiting, Is.EqualTo("D"), "the pick is still waiting");
        }

        [Test]
        public void AStubIsListedButNotBuilt()
        {
            var world = World();
            Assert.That(world.SpecialtiesFor("warrior").Select(s => s.Id), Is.EqualTo(new[] { "vanguard", "sentinel", "berserker", "champion", "fortress" }));
            Assert.That(world.SpecialtiesFor("warrior").Select(world.IsSpecialtyBuilt), Is.EqualTo(new[] { true, false, true, false, true }));
        }

        // --- Saves and snapshots ----------------------------------------------------------------------

        [Test]
        public void ThePicksAreSavedWithTheCharacter()
        {
            var world = Vanguard(level: 9);
            Clear(world, "warrior-trial-b");
            Pick(world, "berserker");

            var saved = CharacterCodec.Decode(CharacterCodec.Encode(world.CaptureCharacter(P)));
            Assert.That(saved.Specialties, Is.EqualTo(new[] { "vanguard", "berserker" }));
            Assert.That(saved.PassedTrials, Is.EqualTo(new[] { "D", "B" }));

            var later = new SimulationWorld(Config());
            later.AddPlayer(P, later.HubJoinPosition(P), saved);
            Assert.That(Picks(later, P), Is.EqualTo(new[] { "vanguard", "berserker" }));
            Assert.That(Known(later, P), Is.SupersetOf(new[] { "smash", "cleave", "earthsplitter" }));
        }

        [Test]
        public void APassedTrialWithNoPickIsSavedToo()
        {
            var world = World();
            world.PassTrialForTesting(P);
            Step(world);

            var saved = CharacterCodec.Decode(CharacterCodec.Encode(world.CaptureCharacter(P)));
            var later = new SimulationWorld(Config());
            later.AddPlayer(P, later.HubJoinPosition(P), saved);

            Assert.That(later.GetPlayer(P).SpecialtyPickWaiting, Is.EqualTo("D"), "the pick is still waiting after a reload");
        }

        [Test]
        public void ACharacterSavedBeforeSpecialtiesHasNone()
        {
            var world = Vanguard();
            var old = CharacterCodec.Decode(CharacterCodec.EncodeVersion6ForTests(world.CaptureCharacter(P)));
            Assert.That(old.Specialties, Is.Empty);
            Assert.That(old.PassedTrials, Is.Empty);
        }

        [Test]
        public void PicksThatDoNotFitAreDroppedOnLoad()
        {
            // Another class's specialty, and a B pick whose D parent is not there.
            var wizard = new SimulationWorld(Config());
            wizard.AddPlayer(P, wizard.HubJoinPosition(P), new CharacterRecord { ClassId = "wizard", Level = BRankLevel, Specialties = { "vanguard" } });
            Assert.That(Picks(wizard, P), Is.Empty);

            var orphan = new SimulationWorld(Config());
            orphan.AddPlayer(P, orphan.HubJoinPosition(P), new CharacterRecord { ClassId = "warrior", Level = BRankLevel, Specialties = { "berserker" }, PassedTrials = { "D", "B" } });
            Assert.That(Picks(orphan, P), Is.Empty);
            Assert.That(Known(orphan, P), Has.No.Member("cleave"));
            Assert.That(orphan.GetPlayer(P).SpecialtyPickWaiting, Is.EqualTo("D"), "its passed trials stay passed");
        }

        [Test]
        public void SnapshotsCarryThePicksToGuests()
        {
            var world = Vanguard();

            var guestView = SimulationWorld.FromSnapshot(Config(), world.CaptureSnapshot());
            Assert.That(Picks(guestView, P), Is.EqualTo(new[] { "vanguard" }));
            Assert.That(guestView.GetPlayer(P).PassedTrials, Is.EqualTo(new[] { "D" }));
        }

        [Test]
        public void TheCommandCrossesTheWire()
        {
            var command = PlayerCommand.None.WithChooseSpecialty("berserker");
            var (_, read) = HunterSystem.Netcode.Wire.ReadCommand(HunterSystem.Netcode.Wire.Command(1, command));
            Assert.That(read.ChooseSpecialtyId, Is.EqualTo("berserker"));
            var (_, none) = HunterSystem.Netcode.Wire.ReadCommand(HunterSystem.Netcode.Wire.Command(2, PlayerCommand.None));
            Assert.That(none.ChooseSpecialtyId, Is.Null);
        }

        // --- Content rules ----------------------------------------------------------------------------

        [Test]
        public void ASpecialtySkillMustBelongToItsSpecialtysClass()
        {
            var config = Config();
            config.Skills = config.Skills.Append(Skill("odd", "wizard", unlock: 3, specialty: "berserker")).ToArray();
            var error = Assert.Throws<ArgumentException>(() => new SimulationWorld(config));
            Assert.That(error.Message, Does.Contain("odd").And.Contain("warrior"));
        }

        [Test]
        public void ATrialMustBeARankedGateInTheGame()
        {
            var config = Config();
            config.Classes[0].TrialGateIds.Add("nowhere");
            Assert.That(Assert.Throws<ArgumentException>(() => new SimulationWorld(config)).Message, Does.Contain("nowhere"));

            var unranked = Config();
            unranked.Classes[0].TrialGateIds.Add("mill-unranked");
            unranked.Gates = unranked.Gates.Append(OneRoomGate("mill-unranked", null)).ToArray();
            Assert.Throws<ArgumentException>(() => new SimulationWorld(unranked));
        }

        [Test]
        public void ABranchMustGrowFromALowerTierOfItsClass()
        {
            var config = Config();
            config.Classes[0].Specialties.Add(Specialty("sideways", "D", parent: "vanguard"));
            Assert.That(Assert.Throws<ArgumentException>(() => new SimulationWorld(config)).Message, Does.Contain("sideways"));

            var lost = Config();
            lost.Classes[0].Specialties.Add(Specialty("lost", "B", parent: "pyromancer"));
            Assert.Throws<ArgumentException>(() => new SimulationWorld(lost));
        }

        [Test]
        public void ASkillMustNameASpecialtyThatExists()
        {
            var config = Config();
            config.Skills = config.Skills.Append(Skill("lost", "warrior", unlock: 3, specialty: "paladin")).ToArray();
            Assert.Throws<ArgumentException>(() => new SimulationWorld(config));
        }
    }
}
