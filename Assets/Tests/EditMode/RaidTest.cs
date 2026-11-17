using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The first raid's rules (ticket 29, ADR 0023). When the boss's room fills, the boss is shielded and crystals
    /// stand far apart, one per three players present. The shield falls only when every crystal is broken within
    /// the regrow window of the first, so the party must split up and strike together; otherwise the broken ones
    /// grow back. From a health threshold the boss also stops to wind up a room-wide blast that only a shelter
    /// circle is safe from, so everyone must get into it together. Wipes and solo falls start the raid over.
    /// </summary>
    public class RaidTest
    {
        private const int Tick = 60;
        private static readonly Vec3 HallCentre = new Vec3(0f, 0f, 25f); // the second room: 30 by 30, from z 10 to 40

        private static readonly EnemyDefinition Crystal = new EnemyDefinition
        {
            Name = "Crystal", MaxHealth = 100, MoveSpeed = 0f, AttackDamage = 50, AttackRange = 50f, XpReward = 0,
        };

        /// <summary>A boss that stands still and never attacks unless given attacks.</summary>
        private static EnemyDefinition King(params BossAttackDefinition[] attacks)
        {
            var boss = new BossDefinition { EnrageAtHealthPercent = 0 };
            boss.Attacks.AddRange(attacks);
            return new EnemyDefinition { Name = "King", MaxHealth = 1000, MoveSpeed = 0f, StopDistance = 1.5f, XpReward = 100, Boss = boss };
        }

        private static GateDefinition RaidGate(EnemyDefinition king = null)
        {
            var raid = new RaidDefinition
            {
                Crystal = Crystal, PlayersPerCrystal = 3, CrystalRegrowSeconds = 2f,
                ShelterAtHealthPercent = 40, FirstShelterAfterSeconds = 1f, ShelterEverySeconds = 3f, ShelterWindupSeconds = 2f,
                ShelterRadius = 3f, ShelterDamagePercent = 80,
            };
            raid.CrystalPlaces.AddRange(new[] { new Vec3(-10f, 0f, -10f), new Vec3(10f, 0f, 10f), new Vec3(10f, 0f, -10f), new Vec3(-10f, 0f, 10f) });
            raid.ShelterPlaces.AddRange(new[] { new Vec3(8f, 0f, 8f), new Vec3(-8f, 0f, -8f) });

            var gate = new GateDefinition { Id = "raid", Name = "Raid", Raid = raid };
            gate.Rooms.Add(new RoomDefinition { Width = 10f, Length = 10f, HasCheckpoint = true });
            var hall = new RoomDefinition { Width = 30f, Length = 30f };
            hall.Spawns.Add(new RoomSpawn(king ?? King(), Vec3.Zero));
            gate.Rooms.Add(hall);
            return gate;
        }

        private static SimulationConfig Config(GateDefinition gate) => new SimulationConfig
        {
            TickRate = Tick, PlayerMoveSpeed = 5f, PlayerMaxHealth = 100, Gates = new[] { gate },
        };

        private static List<SimulationEvent> Step(SimulationWorld world, int ticks = 1, Dictionary<PlayerId, PlayerCommand> commands = null)
        {
            var all = new List<SimulationEvent>();
            for (var i = 0; i < ticks; i++)
            {
                world.Step(commands ?? new Dictionary<PlayerId, PlayerCommand>());
                all.AddRange(world.Events);
            }
            return all;
        }

        /// <summary>A raid with <paramref name="players"/> players present, all standing in the hall so it has filled.</summary>
        private static SimulationWorld InTheHall(int players, out EnemyId boss, EnemyDefinition king = null)
        {
            var world = new SimulationWorld(Config(RaidGate(king)));
            for (var i = 1; i <= players; i++) world.AddPlayer(new PlayerId(i), new Vec3(0f, 0f, 2f));
            world.StartGate("raid");
            for (var i = 1; i <= players; i++) world.PlacePlayer(new PlayerId(i), HallCentre + new Vec3(-12f + i, 0f, -12f));
            Step(world);
            boss = world.EnemyIds.Single(id => world.GetEnemy(id).IsBoss);
            return world;
        }

        private static void Break(SimulationWorld world, EnemyId crystal) => world.DamageEnemy(crystal, new PlayerId(1), 1000);

        // --- The shield and the crystals ----------------------------------------------------------

        [TestCase(1, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(6, 2)]
        [TestCase(7, 3)]
        [TestCase(10, 4)]
        public void OneCrystalForEveryThreePlayersPresent(int players, int crystals)
        {
            var world = InTheHall(players, out _);

            Assert.That(world.Gate.Raid.Crystals.Count, Is.EqualTo(crystals));
            Assert.That(world.Gate.Raid.Phase, Is.EqualTo(RaidPhase.Shielded));
        }

        [Test]
        public void NeverMoreCrystalsThanPlaces()
        {
            var gate = RaidGate();
            gate.Raid.PlayersPerCrystal = 1;

            Assert.That(gate.Raid.CrystalsFor(10), Is.EqualTo(4));
            Assert.That(gate.Raid.CrystalsFor(0), Is.EqualTo(1), "never none");
        }

        [Test]
        public void CrystalsStandFarApartAtTheirPlacesInTheBossRoom()
        {
            var world = InTheHall(10, out _);

            var places = world.Gate.Raid.Crystals.Select(id => world.GetEnemy(id).Position).ToList();
            Assert.That(places[0].X, Is.EqualTo(-10f).Within(0.01f));
            Assert.That(places[0].Z, Is.EqualTo(15f).Within(0.01f));
            Assert.That(places[1].X, Is.EqualTo(10f).Within(0.01f));
            Assert.That(places[1].Z, Is.EqualTo(35f).Within(0.01f));
            Assert.That(Vec3Distance(places[0], places[2]), Is.EqualTo(20f).Within(0.01f), "a group per corner");
        }

        [Test]
        public void TheShieldedBossTakesNoDamage()
        {
            var world = InTheHall(4, out var boss);

            world.DamageEnemy(boss, new PlayerId(1), 300);
            var events = Step(world);

            Assert.That(world.GetEnemy(boss).Health, Is.EqualTo(1000));
            Assert.That(events.OfType<DamageDealt>(), Is.Empty);
            Assert.That(world.Gate.Raid.IsBossShielded, Is.True);
        }

        [Test]
        public void CrystalsNeitherMoveNorAttack()
        {
            var world = InTheHall(1, out _);
            var crystal = world.Gate.Raid.Crystals[0];
            var at = world.GetEnemy(crystal).Position;
            world.PlacePlayer(new PlayerId(1), at + new Vec3(1f, 0f, 0f)); // right beside it, inside its authored reach

            var events = Step(world, Tick * 3);

            Assert.That(world.GetEnemy(crystal).Position, Is.EqualTo(at));
            Assert.That(events.OfType<EnemyAttackStarted>(), Is.Empty);
            Assert.That(world.GetPlayer(new PlayerId(1)).Health, Is.EqualTo(100));
        }

        [Test]
        public void BreakingEveryCrystalTogetherDropsTheShield()
        {
            var world = InTheHall(6, out var boss);
            var crystals = world.Gate.Raid.Crystals.ToList();

            Break(world, crystals[0]);
            Step(world, Tick); // a second later, well inside the two-second window
            Break(world, crystals[1]);
            var events = Step(world);

            Assert.That(events.OfType<RaidShieldBroken>().Single().Boss, Is.EqualTo(boss));
            Assert.That(world.Gate.Raid.Phase, Is.EqualTo(RaidPhase.Open));
            Assert.That(world.Gate.Raid.Crystals, Is.Empty);
            Assert.That(crystals.Any(world.HasEnemy), Is.False, "the shards are gone");

            world.DamageEnemy(boss, new PlayerId(1), 300);
            Step(world);
            Assert.That(world.GetEnemy(boss).Health, Is.EqualTo(700), "the boss can be hurt now");
        }

        [Test]
        public void TooSlowAndTheBrokenCrystalsGrowBack()
        {
            var world = InTheHall(6, out _);
            var crystals = world.Gate.Raid.Crystals.ToList();

            Break(world, crystals[0]);
            Step(world);
            Assert.That(world.Gate.Raid.SecondsUntilRegrow, Is.EqualTo(2f).Within(0.02f));
            var events = Step(world, Tick * 2);

            Assert.That(events.OfType<RaidCrystalsRegrew>().Single().Count, Is.EqualTo(1));
            Assert.That(world.GetEnemy(crystals[0]).Health, Is.EqualTo(100), "whole again");
            Assert.That(world.Gate.Raid.Phase, Is.EqualTo(RaidPhase.Shielded));
            Assert.That(world.Gate.Raid.SecondsUntilRegrow, Is.Null);

            Break(world, crystals[1]); // the other one alone does not do it either
            Step(world);
            Assert.That(world.Gate.Raid.Phase, Is.EqualTo(RaidPhase.Shielded));
        }

        [TestCase(Tick * 2, RaidPhase.Open)] // the second lands on the window's last tick: in time
        [TestCase(Tick * 2 + 1, RaidPhase.Shielded)] // a tick later: the first has already grown back
        public void TheWindowStartsWithTheFirstCrystalBroken(int ticksBetween, RaidPhase after)
        {
            var world = InTheHall(6, out _);
            var crystals = world.Gate.Raid.Crystals.ToList();

            Break(world, crystals[0]);
            Step(world, ticksBetween);
            Break(world, crystals[1]);
            Step(world);

            Assert.That(world.Gate.Raid.Phase, Is.EqualTo(after));
        }

        [Test]
        public void ASoloRaidHasOneCrystalAndCanBeBrokenAlone()
        {
            var world = InTheHall(1, out _);

            Break(world, world.Gate.Raid.Crystals[0]);
            Step(world);

            Assert.That(world.Gate.Raid.Phase, Is.EqualTo(RaidPhase.Open));
        }

        [Test]
        public void CrystalsGiveNoXp()
        {
            var world = InTheHall(1, out _);

            Break(world, world.Gate.Raid.Crystals[0]);
            var events = Step(world);

            Assert.That(events.OfType<XpGained>(), Is.Empty);
        }

        // --- The final phase: the shelter -----------------------------------------------------------

        /// <summary>Into the final phase: the shield broken and the boss brought to the threshold.</summary>
        private static SimulationWorld InTheFinalPhase(int players, out EnemyId boss, EnemyDefinition king = null)
        {
            var world = InTheHall(players, out boss, king);
            foreach (var crystal in world.Gate.Raid.Crystals.ToList()) Break(world, crystal);
            Step(world);
            world.DamageEnemy(boss, new PlayerId(1), 600);
            var events = Step(world);
            Assert.That(events.OfType<RaidFinalPhase>().Count(), Is.EqualTo(1));
            Assert.That(world.Gate.Raid.Phase, Is.EqualTo(RaidPhase.Final));
            return world;
        }

        [Test]
        public void AboveTheThresholdThereIsNoFinalPhase()
        {
            var world = InTheHall(1, out var boss);
            Break(world, world.Gate.Raid.Crystals[0]);
            Step(world);

            world.DamageEnemy(boss, new PlayerId(1), 599); // 401 of 1000 left: just above 40%
            var events = Step(world, Tick * 5);

            Assert.That(world.Gate.Raid.Phase, Is.EqualTo(RaidPhase.Open));
            Assert.That(events.OfType<RaidShelterMarked>(), Is.Empty);
        }

        [Test]
        public void TheShelterIsMarkedAndTheBlastHitsEveryoneOutsideIt()
        {
            var world = InTheFinalPhase(3, out var boss);

            var marked = Step(world, Tick).OfType<RaidShelterMarked>().Single();
            var shelter = HallCentre + new Vec3(8f, 0f, 8f);
            Assert.That(marked.Position.X, Is.EqualTo(shelter.X).Within(0.01f));
            Assert.That(marked.Position.Z, Is.EqualTo(shelter.Z).Within(0.01f));
            Assert.That(marked.Radius, Is.EqualTo(3f));
            Assert.That(world.Gate.Raid.Shelter.HasValue, Is.True, "in the state too, for drawing and late joiners");

            world.PlacePlayer(new PlayerId(1), shelter + new Vec3(2.5f, 0f, 0f)); // inside, near the edge
            world.PlacePlayer(new PlayerId(2), shelter + new Vec3(3.5f, 0f, 0f)); // just outside
            var events = Step(world, marked.LandsAtTick - world.Tick);

            Assert.That(events.OfType<RaidBlastLanded>().Single().Caught, Is.EqualTo(2));
            Assert.That(world.GetPlayer(new PlayerId(1)).Health, Is.EqualTo(100), "sheltered");
            Assert.That(world.GetPlayer(new PlayerId(2)).Health, Is.EqualTo(20), "80% of their health");
            Assert.That(world.GetPlayer(new PlayerId(3)).Health, Is.EqualTo(20));
            Assert.That(events.OfType<PlayerDamaged>().All(d => d.Attacker == boss));
            Assert.That(world.Gate.Raid.Shelter, Is.Null, "over until the next one");
        }

        [Test]
        public void ADodgeDoesNotSaveYouFromTheBlast()
        {
            var world = InTheFinalPhase(1, out _);
            var marked = Step(world, Tick).OfType<RaidShelterMarked>().Single();

            Step(world, marked.LandsAtTick - world.Tick - 1);
            var dodge = new Dictionary<PlayerId, PlayerCommand> { [new PlayerId(1)] = new PlayerCommand(1f, 0f, dodge: true) };
            Step(world, 1, dodge);

            Assert.That(world.GetPlayer(new PlayerId(1)).Health, Is.EqualTo(20));
        }

        [Test]
        public void TwoBlastsOutsideTheShelterFellASoloPlayerAndTheRaidStartsOver()
        {
            var world = InTheFinalPhase(1, out var boss);

            var events = Step(world, Tick * 10);

            Assert.That(events.OfType<RaidShelterMarked>().Count(), Is.EqualTo(2));
            Assert.That(events.OfType<PlayerDefeated>().Single().DefeatedBy, Is.EqualTo(boss));
            Assert.That(world.Gate.Raid.Phase, Is.EqualTo(RaidPhase.Waiting), "a solo fall brings the boss back fresh (ADR 0013), raid and all");
            Assert.That(world.Gate.Raid.Shelter, Is.Null);
        }

        [Test]
        public void TheShelterMovesEachTime()
        {
            var world = InTheFinalPhase(2, out _);

            var marks = Step(world, Tick * 12).OfType<RaidShelterMarked>().ToList();

            Assert.That(marks.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(marks[0].Position.X, Is.EqualTo(8f).Within(0.01f));
            Assert.That(marks[1].Position.X, Is.EqualTo(-8f).Within(0.01f));
            Assert.That(marks[2].Position.X, Is.EqualTo(8f).Within(0.01f), "round and round");
            Assert.That(marks[1].Tick - marks[0].LandsAtTick, Is.EqualTo(Tick * 3), "three seconds from one blast to the next");
        }

        [Test]
        public void TheBossStandsAndChannelsWhileTheBlastWindsUp()
        {
            var sweep = new BossAttackDefinition { Kind = BossAttackKind.Sweep, Damage = 5, WindupSeconds = 0.2f, CooldownSeconds = 0.1f, Range = 50f, ArcDegrees = 360f };
            var world = InTheFinalPhase(1, out var boss, King(sweep));

            var events = Step(world, Tick * 2);
            var marked = events.OfType<RaidShelterMarked>().Single();
            var during = events.Where(e => e.Tick > marked.Tick && e.Tick < marked.LandsAtTick);

            Assert.That(during.OfType<BossAttackStarted>(), Is.Empty, "no attacks while it winds up the blast");
            Assert.That(events.Where(e => e.Tick < marked.Tick).OfType<BossAttackStarted>(), Is.Not.Empty, "it fought before");
        }

        // --- Starting over and finishing --------------------------------------------------------------

        [Test]
        public void AWipeStartsTheRaidOver()
        {
            var world = InTheHall(4, out _);
            Break(world, world.Gate.Raid.Crystals[0]);
            Break(world, world.Gate.Raid.Crystals[1]);
            Step(world);
            Assert.That(world.Gate.Raid.Phase, Is.EqualTo(RaidPhase.Open));

            for (var i = 1; i <= 4; i++) world.DamagePlayerForTesting(new PlayerId(i), 1000);
            Step(world, Tick * 4);
            Assert.That(world.Gate.Raid.Phase, Is.EqualTo(RaidPhase.Waiting), "the boss room is not entered any more");

            for (var i = 1; i <= 4; i++) world.PlacePlayer(new PlayerId(i), HallCentre);
            Step(world);
            Assert.That(world.Gate.Raid.Phase, Is.EqualTo(RaidPhase.Shielded));
            Assert.That(world.Gate.Raid.Crystals.Count, Is.EqualTo(2));
            Assert.That(world.Gate.Raid.Crystals.All(id => world.GetEnemy(id).Health == 100));
        }

        [Test]
        public void DefeatingTheBossClearsTheRaidAndEndsItsBlasts()
        {
            var world = InTheFinalPhase(1, out var boss);
            Step(world, Tick); // a shelter is marked
            Assert.That(world.Gate.Raid.Shelter.HasValue, Is.True);

            world.DamageEnemy(boss, new PlayerId(1), 1000);
            var events = Step(world, Tick * 4);

            Assert.That(events.OfType<GateCleared>().Count(), Is.EqualTo(1));
            Assert.That(world.Gate.Raid.Shelter, Is.Null);
            Assert.That(events.OfType<RaidBlastLanded>(), Is.Empty);
            Assert.That(world.GetPlayer(new PlayerId(1)).Health, Is.EqualTo(100));
        }

        [Test]
        public void ANormalGateHasNoRaid()
        {
            var gate = RaidGate();
            gate.Raid = null;
            var world = new SimulationWorld(Config(gate));
            world.AddPlayer(new PlayerId(1), Vec3.Zero);
            world.StartGate("raid");

            Assert.That(world.Gate.Raid, Is.Null);
        }

        // --- Snapshots ---------------------------------------------------------------------------

        [Test]
        public void ARaidMidShieldRestoresAndCarriesOnIdentically()
        {
            var world = InTheHall(6, out _);
            Break(world, world.Gate.Raid.Crystals[0]);
            Step(world, 10);

            AssertRestoresAndCarriesOn(world, ticks: Tick * 3);
        }

        [Test]
        public void ARaidMidBlastRestoresAndCarriesOnIdentically()
        {
            var world = InTheFinalPhase(3, out _);
            Step(world, Tick + 10);
            Assert.That(world.Gate.Raid.Shelter.HasValue, Is.True);

            AssertRestoresAndCarriesOn(world, ticks: Tick * 8);
        }

        private static void AssertRestoresAndCarriesOn(SimulationWorld world, int ticks)
        {
            var config = Config(world.Gate.Definition);
            var copy = SimulationWorld.FromSnapshot(config, world.CaptureSnapshot());
            Assert.That(copy.CaptureSnapshot(), Is.EqualTo(world.CaptureSnapshot()), "the same bytes");
            Assert.That(copy.Gate.Raid.Phase, Is.EqualTo(world.Gate.Raid.Phase));
            Assert.That(copy.Gate.Raid.Crystals, Is.EqualTo(world.Gate.Raid.Crystals));
            Assert.That(copy.Gate.Raid.Shelter, Is.EqualTo(world.Gate.Raid.Shelter));

            for (var i = 0; i < ticks; i++)
            {
                world.Step(new Dictionary<PlayerId, PlayerCommand>());
                copy.Step(new Dictionary<PlayerId, PlayerCommand>());
            }
            Assert.That(copy.CaptureSnapshot(), Is.EqualTo(world.CaptureSnapshot()), "and the same after carrying on");
        }

        [Test]
        public void ASnapshotOfARaidIsRefusedByAWorldWhoseGateIsNotOne()
        {
            var world = InTheHall(1, out _);
            var plain = RaidGate();
            plain.Raid = null;

            Assert.Throws<System.ArgumentException>(() => SimulationWorld.FromSnapshot(Config(plain), world.CaptureSnapshot()));
        }

        private static float Vec3Distance(Vec3 a, Vec3 b) => (a - b).Length;
    }
}
