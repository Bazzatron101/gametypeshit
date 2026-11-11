using System.Collections.Generic;
using System.Linq;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    public class SimulationCombatTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);
        private static readonly PlayerId P2 = new PlayerId(2);

        // 10 damage per hit against 30 health: three hits to defeat.
        private static SimulationConfig Config => new SimulationConfig
        {
            TickRate = 60,
            PlayerMoveSpeed = 5f,
            LightAttack = new AttackDefinition { Damage = 10, Range = 2f, ArcDegrees = 120f, CooldownSeconds = 0.4f },
        };

        private static readonly EnemyDefinition Goblin = new EnemyDefinition
        {
            Name = "Goblin", MaxHealth = 30, MoveSpeed = 3f, StopDistance = 1.2f,
        };

        private static readonly int CooldownTicks = 24; // 0.4 s at 60 Hz

        private static readonly Dictionary<PlayerId, PlayerCommand> NoInput = new Dictionary<PlayerId, PlayerCommand>();

        private static Dictionary<PlayerId, PlayerCommand> Attack(PlayerId id) =>
            new Dictionary<PlayerId, PlayerCommand> { [id] = new PlayerCommand(0f, 0f, lightAttack: true) };

        /// <summary>Runs ticks and returns every event emitted across them.</summary>
        private static List<SimulationEvent> Run(SimulationWorld sim, int ticks, Dictionary<PlayerId, PlayerCommand> commands)
        {
            var events = new List<SimulationEvent>();
            for (var i = 0; i < ticks; i++)
            {
                sim.Step(commands);
                events.AddRange(sim.Events);
            }
            return events;
        }

        /// <summary>Presses attack once, then waits out the cooldown. Returns the events.</summary>
        private static List<SimulationEvent> AttackOnce(SimulationWorld sim, PlayerId id)
        {
            var events = Run(sim, 1, Attack(id));
            events.AddRange(Run(sim, CooldownTicks, NoInput));
            return events;
        }

        private static SimulationWorld WorldWithPlayerAndEnemyInFront(out EnemyId enemy)
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, Vec3.Zero); // faces +Z until it moves
            enemy = sim.SpawnEnemy(Goblin, new Vec3(0f, 0f, 1.2f)); // already at stop distance, so it holds still
            return sim;
        }

        [Test]
        public void LightAttackDamagesEnemyInFrontAndEmitsADamageEvent()
        {
            var sim = WorldWithPlayerAndEnemyInFront(out var enemy);

            var events = Run(sim, 1, Attack(P1));

            Assert.That(sim.GetEnemy(enemy).Health, Is.EqualTo(20));
            var damage = events.OfType<DamageDealt>().Single();
            Assert.That(damage.Attacker, Is.EqualTo(P1));
            Assert.That(damage.Target, Is.EqualTo(enemy));
            Assert.That(damage.Amount, Is.EqualTo(10));
            Assert.That(damage.RemainingHealth, Is.EqualTo(20));
            Assert.That(damage.Tick, Is.EqualTo(1));
            Assert.That(events.OfType<AttackPerformed>().Single().Player, Is.EqualTo(P1));
        }

        [Test]
        public void EnemyIsDefeatedAfterThreeLightAttacks()
        {
            var sim = WorldWithPlayerAndEnemyInFront(out var enemy);

            var events = new List<SimulationEvent>();
            events.AddRange(AttackOnce(sim, P1));
            events.AddRange(AttackOnce(sim, P1));
            Assert.That(sim.GetEnemy(enemy).IsDefeated, Is.False, "still standing after two hits");
            events.AddRange(AttackOnce(sim, P1));

            var state = sim.GetEnemy(enemy);
            Assert.That(state.IsDefeated, Is.True);
            Assert.That(state.Health, Is.EqualTo(0));
            Assert.That(events.OfType<DamageDealt>().Count(), Is.EqualTo(3));
            var defeated = events.OfType<EnemyDefeated>().Single();
            Assert.That(defeated.Enemy, Is.EqualTo(enemy));
            Assert.That(defeated.DefeatedBy, Is.EqualTo(P1));
        }

        [Test]
        public void DamageDoesNotTakeHealthBelowZero()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, Vec3.Zero);
            var frail = sim.SpawnEnemy(new EnemyDefinition { Name = "Frail", MaxHealth = 4, MoveSpeed = 0f, StopDistance = 1f }, new Vec3(0f, 0f, 1f));

            var events = Run(sim, 1, Attack(P1));

            Assert.That(sim.GetEnemy(frail).Health, Is.EqualTo(0));
            Assert.That(events.OfType<DamageDealt>().Single().Amount, Is.EqualTo(4), "only the health it had");
        }

        [Test]
        public void AttackDuringCooldownDoesNothing()
        {
            var sim = WorldWithPlayerAndEnemyInFront(out var enemy);

            var events = Run(sim, 1, Attack(P1));
            events.AddRange(Run(sim, CooldownTicks - 1, Attack(P1)));

            Assert.That(events.OfType<AttackPerformed>().Count(), Is.EqualTo(1));
            Assert.That(sim.GetEnemy(enemy).Health, Is.EqualTo(20));

            events = Run(sim, 1, Attack(P1));
            Assert.That(events.OfType<AttackPerformed>().Count(), Is.EqualTo(1), "ready again once the cooldown has passed");
        }

        [Test]
        public void EnemyOutOfRangeIsNotHit()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, Vec3.Zero);
            var far = sim.SpawnEnemy(new EnemyDefinition { Name = "Statue", MaxHealth = 30, MoveSpeed = 0f, StopDistance = 1f }, new Vec3(0f, 0f, 2.5f));

            var events = Run(sim, 1, Attack(P1));

            Assert.That(sim.GetEnemy(far).Health, Is.EqualTo(30));
            Assert.That(events.OfType<DamageDealt>(), Is.Empty);
            Assert.That(events.OfType<AttackPerformed>().Count(), Is.EqualTo(1), "a whiff still counts as an attack");
        }

        [Test]
        public void EnemyBehindThePlayerIsNotHit()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, Vec3.Zero);
            var behind = sim.SpawnEnemy(Goblin, new Vec3(0f, 0f, -1.2f));

            Run(sim, 1, Attack(P1));

            Assert.That(sim.GetEnemy(behind).Health, Is.EqualTo(30));
        }

        [Test]
        public void PlayerFacesTheWayTheyLastMoved()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, Vec3.Zero);
            var left = sim.SpawnEnemy(new EnemyDefinition { Name = "Statue", MaxHealth = 30, MoveSpeed = 0f, StopDistance = 1f }, new Vec3(-1.5f, 0f, 0f));

            Run(sim, 1, new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(-1f, 0f) });
            Run(sim, 1, Attack(P1)); // standing still keeps the last facing

            Assert.That(sim.GetPlayer(P1).Facing.X, Is.EqualTo(-1f).Within(1e-4f));
            Assert.That(sim.GetEnemy(left).Health, Is.EqualTo(20));
        }

        [Test]
        public void OneSwingHitsEveryEnemyInTheArc()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, Vec3.Zero);
            var a = sim.SpawnEnemy(Goblin, new Vec3(-0.5f, 0f, 1.2f));
            var b = sim.SpawnEnemy(Goblin, new Vec3(0.5f, 0f, 1.2f));

            var events = Run(sim, 1, Attack(P1));

            Assert.That(events.OfType<DamageDealt>().Select(d => d.Target), Is.EquivalentTo(new[] { a, b }));
        }

        [Test]
        public void EnemyChasesThePlayerAtItsMoveSpeed()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, Vec3.Zero);
            var enemy = sim.SpawnEnemy(Goblin, new Vec3(10f, 0f, 0f));

            Run(sim, 60, NoInput);

            Assert.That(sim.GetEnemy(enemy).Position.X, Is.EqualTo(7f).Within(1e-3f), "3 m/s for one second");
            Assert.That(sim.GetEnemy(enemy).Position.Z, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void EnemyStopsAtItsStopDistance()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, Vec3.Zero);
            var enemy = sim.SpawnEnemy(Goblin, new Vec3(0f, 0f, 5f));

            Run(sim, 300, NoInput);

            Assert.That(sim.GetEnemy(enemy).Position.Z, Is.EqualTo(1.2f).Within(1e-3f));
        }

        [Test]
        public void EnemyChasesTheNearestPlayer()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, new Vec3(-10f, 0f, 0f));
            sim.AddPlayer(P2, new Vec3(4f, 0f, 0f));
            var enemy = sim.SpawnEnemy(Goblin, Vec3.Zero);

            Run(sim, 30, NoInput);

            Assert.That(sim.GetEnemy(enemy).Position.X, Is.EqualTo(1.5f).Within(1e-3f), "toward P2 at 3 m/s for half a second");
        }

        [Test]
        public void DefeatedEnemyStopsMovingAndCannotBeHitAgain()
        {
            var sim = new SimulationWorld(Config);
            sim.AddPlayer(P1, Vec3.Zero);
            var enemy = sim.SpawnEnemy(new EnemyDefinition { Name = "Frail", MaxHealth = 10, MoveSpeed = 3f, StopDistance = 1.2f }, new Vec3(0f, 0f, 1.2f));
            AttackOnce(sim, P1);
            Assert.That(sim.GetEnemy(enemy).IsDefeated, Is.True);
            var corpse = sim.GetEnemy(enemy).Position;

            var events = AttackOnce(sim, P1); // still facing it, still in range
            Assert.That(events.OfType<DamageDealt>(), Is.Empty);
            Assert.That(events.OfType<EnemyDefeated>(), Is.Empty);

            Run(sim, 30, new Dictionary<PlayerId, PlayerCommand> { [P1] = new PlayerCommand(0f, -1f) }); // walk away; a live enemy would follow
            Assert.That(sim.GetEnemy(enemy).Position, Is.EqualTo(corpse));
        }

        [Test]
        public void EventsOnlyDescribeTheMostRecentTick()
        {
            var sim = WorldWithPlayerAndEnemyInFront(out _);

            sim.Step(Attack(P1));
            Assert.That(sim.Events, Is.Not.Empty);
            sim.Step(NoInput);

            Assert.That(sim.Events, Is.Empty);
        }
    }
}
