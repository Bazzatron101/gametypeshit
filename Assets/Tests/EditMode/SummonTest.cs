using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Summons (ticket 57). A summon skill calls creatures beside the summoner. Each lasts a set time or until it falls,
    /// goes for the nearest enemy and hits it for the skill's damage (the kill and XP are the summoner's), and can be
    /// hit: an ordinary enemy takes on whichever is nearer, a hunter or a summon. A summoner keeps at most three.
    /// Summons are in snapshots and go when a gate starts or ends.
    /// </summary>
    public class SummonTest
    {
        private static readonly PlayerId P = new PlayerId(1);

        private static SkillDefinition Imp(int count = 1, float lifetime = 5f) => new SkillDefinition
        {
            Id = "call-imp", Name = "Call Imp", Effect = SkillEffectKind.Summon, Damage = 10, SummonHealth = 40, SummonLifetimeSeconds = lifetime,
            SummonAttackRange = 1.5f, SummonAttackSeconds = 0.5f, SummonMoveSpeed = 6f, SummonCount = count, CooldownSeconds = 0.1f,
            ScalingStat = Stat.Intelligence, Tags = new[] { SkillTags.Summon },
        };

        private static SimulationConfig Config(SkillDefinition skill) => new SimulationConfig
        {
            TickRate = 60,
            Skills = new[] { skill },
            Gates = new[] { new GateDefinition { Id = "room", Name = "room", Rooms = { new RoomDefinition { Width = 10f, Length = 10f } } } },
            Hub = new HubDefinition { Centre = new Vec3(-80f, 0f, 0f), Width = 20f, Length = 16f, BoardOffset = new Vec3(0f, 0f, 6f), ReturnCountdownSeconds = 1f },
        };

        /// <summary>Open ground with no hub or gates.</summary>
        private static SimulationConfig OpenConfig(SkillDefinition skill)
        {
            var config = Config(skill);
            config.Hub = null;
            config.Gates = Array.Empty<GateDefinition>();
            return config;
        }

        private static SimulationWorld World(SkillDefinition skill = null, bool hub = false)
        {
            var config = hub ? Config(skill ?? Imp()) : OpenConfig(skill ?? Imp());
            var world = new SimulationWorld(config);
            world.AddPlayer(P, hub ? world.HubJoinPosition(P) : Vec3.Zero);
            return world;
        }

        private static List<SimulationEvent> Step(SimulationWorld world, int ticks, PlayerCommand first = default)
        {
            var all = new List<SimulationEvent>();
            for (var i = 0; i < ticks; i++)
            {
                world.Step(new Dictionary<PlayerId, PlayerCommand> { [P] = i == 0 ? first : PlayerCommand.None });
                all.AddRange(world.Events);
            }
            return all;
        }

        private static readonly PlayerCommand Call = new PlayerCommand(0f, 0f, useSkillSlot: 0);

        private static EnemyDefinition Dummy(int health = 1000) => new EnemyDefinition { Name = "Dummy", MaxHealth = health, MoveSpeed = 0f, AttackRange = 0f, XpReward = 25 };

        [Test]
        public void ASummonAppearsBesideItsSummoner()
        {
            var world = World();
            var events = Step(world, 1, Call);

            var appeared = events.OfType<SummonAppeared>().Single();
            Assert.That(appeared.Owner, Is.EqualTo(P));
            var summon = world.Summons.Single();
            Assert.That((summon.Health, summon.MaxHealth), Is.EqualTo((40, 40)));
            Assert.That((summon.Position - Vec3.Zero).Length, Is.LessThan(2f), "beside the summoner");
        }

        [Test]
        public void ASummonGoesForTheNearestEnemyAndTheKillIsTheSummoners()
        {
            var world = World();
            var near = world.SpawnEnemy(Dummy(health: 30), new Vec3(4f, 0f, 0f));
            world.SpawnEnemy(Dummy(), new Vec3(-12f, 0f, 0f));
            Step(world, 1, Call);

            var events = Step(world, 180);

            Assert.That(events.OfType<DamageDealt>().Select(d => d.Target).Distinct(), Is.EqualTo(new[] { near }), "only the nearer one");
            Assert.That(events.OfType<DamageDealt>().All(d => d.Attacker == P && d.Skill.Id == "call-imp"), Is.True, "its hits are its summoner's");
            Assert.That(events.OfType<EnemyDefeated>().Single().DefeatedBy, Is.EqualTo(P));
            Assert.That(events.OfType<XpGained>().Any(x => x.Player == P), Is.True, "the summoner gets the XP");
        }

        [Test]
        public void ASummonLastsItsLifetime()
        {
            var world = World(Imp(lifetime: 1f));
            Step(world, 1, Call);
            Assert.That(world.Summons.Count, Is.EqualTo(1));

            var events = Step(world, 61);

            Assert.That(events.OfType<SummonEnded>().Single().Fell, Is.False);
            Assert.That(world.Summons, Is.Empty);
        }

        [Test]
        public void ASummonerKeepsAtMostThree()
        {
            var world = World(Imp(count: 2, lifetime: 60f));
            Step(world, 1, Call);
            var first = world.Summons.Select(s => s.Id).ToList();
            Step(world, 20); // past the cooldown
            var events = Step(world, 1, Call);

            Assert.That(world.Summons.Count, Is.EqualTo(SimulationWorld.MaxSummonsPerPlayer));
            Assert.That(events.OfType<SummonEnded>().Select(e => e.Summon), Is.EqualTo(new[] { first[0] }), "the oldest goes");
        }

        [Test]
        public void AnEnemyTakesOnANearerSummonAndCanFellIt()
        {
            var world = World(Imp(lifetime: 60f));
            Step(world, 1, Call);
            var summon = world.Summons.Single();
            // A biter that cannot move, near the summon and out of reach of the hunter.
            var away = summon.Position - Vec3.Zero;
            var biter = world.SpawnEnemy(new EnemyDefinition { Name = "Biter", MaxHealth = 100000, MoveSpeed = 0f, AttackRange = 2f, AttackDamage = 15, AttackWindupSeconds = 0.1f, AttackCooldownSeconds = 0.2f },
                summon.Position + away * (1.8f / away.Length));

            var events = Step(world, 200);

            Assert.That(events.OfType<SummonDamaged>().Any(d => d.Attacker == biter), Is.True);
            Assert.That(events.OfType<PlayerDamaged>(), Is.Empty, "the hunter is further away");
            Assert.That(events.OfType<SummonEnded>().Single().Fell, Is.True, "40 health against 15 a hit");
        }

        [Test]
        public void SenseRaisesASummonsHealth()
        {
            var world = World();
            world.SetStatsForTesting(P, Stat.Sense, 20); // +50%
            Step(world, 1);
            Step(world, 1, Call);
            Assert.That(world.Summons.Single().MaxHealth, Is.EqualTo(60));
        }

        [Test]
        public void SnapshotsCarrySummons()
        {
            var world = World(Imp(lifetime: 60f));
            Step(world, 1, Call);

            var guest = SimulationWorld.FromSnapshot(OpenConfig(Imp(lifetime: 60f)), world.CaptureSnapshot());

            Assert.That(guest.Summons.Select(s => (s.Id, s.Owner, s.Health)), Is.EqualTo(world.Summons.Select(s => (s.Id, s.Owner, s.Health))));
        }

        [Test]
        public void SummonsGoWhenAGateStartsOrEnds()
        {
            var world = World(Imp(lifetime: 60f), hub: true);
            Step(world, 1, Call);
            Assert.That(world.Summons, Is.Not.Empty);

            world.StartGate("room");
            Step(world, 1);
            Assert.That(world.Summons, Is.Empty, "not into the gate");

            Step(world, 10); // past the cooldown
            Step(world, 1, Call);
            Assert.That(world.Summons, Is.Not.Empty);
            world.LeaveGate();
            Step(world, 2);
            Assert.That(world.Summons, Is.Empty, "not out of it");
        }

        [Test]
        public void ASummonSkillNeedsHealthAndALifetime()
        {
            var bad = Imp();
            bad.SummonHealth = 0;
            Assert.Throws<ArgumentException>(() => new SimulationWorld(new SimulationConfig { Skills = new[] { bad } }));
            var many = Imp(count: 4);
            Assert.Throws<ArgumentException>(() => new SimulationWorld(new SimulationConfig { Skills = new[] { many } }));
        }

        [Test]
        public void SummonEventsCrossTheWire()
        {
            var config = new SimulationConfig { Skills = new[] { Imp() } };
            var events = new SimulationEvent[]
            {
                new SummonAppeared(3, 7, P, Imp(), new Vec3(1f, 0f, 2f)),
                new SummonDamaged(4, 7, new EnemyId(9), 15, 25),
                new SummonEnded(5, 7, true),
            };
            var back = SimulationEventCodec.Decode(SimulationEventCodec.Encode(events), config);
            Assert.That(back.Select(e => e.GetType()), Is.EqualTo(events.Select(e => e.GetType())));
            Assert.That(((SummonAppeared)back[0]).Skill.Id, Is.EqualTo("call-imp"));
            Assert.That(((SummonDamaged)back[1]).RemainingHealth, Is.EqualTo(25));
            Assert.That(((SummonEnded)back[2]).Fell, Is.True);
        }
    }
}
