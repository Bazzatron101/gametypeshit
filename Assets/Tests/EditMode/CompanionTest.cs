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
    /// The host's five bot teammates (ADR 0014): who they are, how they learn, how they are saved,
    /// and, in the first gate from the real content, that they play their roles and play better
    /// with experience. Headless, no network.
    /// </summary>
    public class CompanionTest
    {
        private const int TickRate = 60;
        private static readonly string[] Classes = { "warrior", "rogue", "archer", "wizard" };

        private static GameContentAsset Content() =>
            AssetDatabase.FindAssets("t:GameContentAsset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameContentAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Single();

        private static SimulationConfig RealConfig()
        {
            var config = new SimulationConfig();
            Content().ApplyTo(config);
            return config;
        }

        // --- Who they are ------------------------------------------------------------------

        [TestCase("warrior")]
        [TestCase("rogue")]
        [TestCase("archer")]
        [TestCase("wizard")]
        [TestCase(null)]
        public void FiveTeammatesCoverEveryClassAndEvenOutTheFrontAndBackLines(string playerClass)
        {
            var config = RealConfig();
            var roster = CompanionParty.Roster(playerClass, config.Classes);

            Assert.That(roster.Count, Is.EqualTo(5));
            var classes = config.Classes.Select(c => c.Id).ToArray();
            if (classes.Length <= roster.Count)
                Assert.That(roster.Distinct(), Is.EquivalentTo(classes), "one of every class, at least");
            else // more classes than seats (tickets 55 to 58): every role, at least
                Assert.That(roster.Select(CompanionRoles.ForClass).Distinct(), Is.EquivalentTo(classes.Select(CompanionRoles.ForClass).Distinct()), "one of every role, at least");
            var party = playerClass != null ? roster.Append(playerClass).ToList() : roster;
            var front = party.Count(id => CompanionRoles.IsFrontLine(CompanionRoles.ForClass(id)));
            Assert.That(Math.Abs(front - (party.Count - front)), Is.LessThanOrEqualTo(1), $"front {front} of {party.Count}: {string.Join(", ", party)}");
        }

        [Test]
        public void EachClassHasItsRole()
        {
            Assert.That(CompanionRoles.ForClass("warrior"), Is.EqualTo(CompanionRole.Tank));
            Assert.That(CompanionRoles.ForClass("rogue"), Is.EqualTo(CompanionRole.Skirmisher));
            Assert.That(CompanionRoles.ForClass("archer"), Is.EqualTo(CompanionRole.Marksman));
            Assert.That(CompanionRoles.ForClass("wizard"), Is.EqualTo(CompanionRole.Caster));
        }

        [Test]
        public void ANewPartyIsFiveNamedRookiesAtLevelOne()
        {
            var party = CompanionParty.NewParty("archer", RealConfig());

            Assert.That(party.Count, Is.EqualTo(5));
            Assert.That(party.Select(c => c.Name).Distinct().Count(), Is.EqualTo(5), "every name differs");
            Assert.That(party.All(c => c.Character.IsNew && c.Experience.Proficiency == CompanionExperience.Rookie), Is.True);
        }

        // --- How they learn ----------------------------------------------------------------

        [Test]
        public void ProficiencyGrowsWithExperienceAndLevelsOffBelowPerfect()
        {
            var x = new CompanionExperience();
            Assert.That(x.Proficiency, Is.EqualTo(CompanionExperience.Rookie).Within(1e-6));

            var previous = x.Proficiency;
            for (var gate = 1; gate <= 12; gate++)
            {
                // About what one run of the first gate gives: 25 kills, the boss, the clear, two falls, ten dodges, two minutes.
                x.EnemiesDefeated += 25; x.BossesDefeated++; x.GatesCleared++; x.TimesDowned += 2; x.AttacksDodged += 10; x.SecondsInCombat += 120;
                Assert.That(x.Proficiency, Is.GreaterThan(previous), $"better after gate {gate}");
                Assert.That(x.Proficiency - previous, Is.LessThanOrEqualTo(gate == 1 ? 1f : 0.2f), "and the gains shrink");
                if (gate == 1) Assert.That(x.Proficiency, Is.InRange(0.4f, 0.55f), "noticeably better after the first gate");
                previous = x.Proficiency;
            }
            Assert.That(x.Proficiency, Is.GreaterThan(0.95f).And.LessThan(1f));
            Assert.That(CompanionExperience.Rank(x.Proficiency), Is.EqualTo("Elite"));
        }

        [Test]
        public void TeammatesSaveAndLoadWithTheirExperience()
        {
            var party = CompanionParty.NewParty("warrior", RealConfig());
            party[2].Character = new CharacterRecord { ClassId = "archer", Level = 7, Xp = 12, UnspentStatPoints = 1, Stats = new[] { 10, 16, 10, 10, 15, 10 } };
            party[2].Experience = new CompanionExperience { EnemiesDefeated = 80, BossesDefeated = 3, GatesCleared = 3, TimesDowned = 5, AttacksDodged = 41, SecondsInCombat = 600 };

            var loaded = CompanionCodec.Decode(CompanionCodec.Encode(party));

            Assert.That(loaded.Select(c => c.Name), Is.EqualTo(party.Select(c => c.Name)));
            Assert.That(loaded.Select(c => c.ClassId), Is.EqualTo(party.Select(c => c.ClassId)));
            Assert.That(loaded[2].Character.Level, Is.EqualTo(7));
            Assert.That(loaded[2].Character.Stats, Is.EqualTo(party[2].Character.Stats));
            Assert.That(loaded[2].Experience.Points, Is.EqualTo(party[2].Experience.Points));
            Assert.That(loaded[2].Experience.Proficiency, Is.EqualTo(party[2].Experience.Proficiency));
        }

        [Test]
        public void UnreadableTeammateBytesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => CompanionCodec.Decode(new byte[] { 1, 2, 3 }));
            var bytes = CompanionCodec.Encode(CompanionParty.NewParty(null, new SimulationConfig()));
            Assert.Throws<ArgumentException>(() => CompanionCodec.Decode(bytes.Take(bytes.Length - 3).ToArray()));
        }

        [Test]
        public void ATeammateBehindThePlayerCatchesUpWithItsClassStatsAndSkills()
        {
            var config = RealConfig();
            var rookie = new CharacterRecord { IsNew = true, ClassId = "wizard" };

            var caughtUp = CompanionParty.CaughtUp(rookie, 8, config);
            Assert.That(CompanionParty.CaughtUp(caughtUp, 5, config), Is.SameAs(caughtUp), "never taken down a level");

            var world = new SimulationWorld(config);
            world.AddPlayer(new PlayerId(1), Vec3.Zero, caughtUp);
            var wizard = world.GetPlayer(new PlayerId(1));
            Assert.That(wizard.Level, Is.EqualTo(8));
            Assert.That(wizard.Stats[Stat.Intelligence], Is.EqualTo(config.Progression.StartingStatValue + 7), "the class's Intelligence for seven levels");
            Assert.That(wizard.UnspentStatPoints, Is.EqualTo(config.Progression.FreePointsPerLevel * 7), "and the free points, for its role to place");
            Assert.That(wizard.KnownSkills.Select(s => s.Id), Has.Member("arcane-orb"), "the level-8 spell");
        }

        // --- In the first gate -------------------------------------------------------------

        /// <summary>A gate run: player 1 is the leader (a simple bot standing in for the person), players 2 to 6 the teammates.</summary>
        private sealed class Run
        {
            public SimulationWorld World;
            public readonly Dictionary<PlayerId, CompanionBrain> Teammates = new Dictionary<PlayerId, CompanionBrain>();
            public readonly List<SimulationEvent> Events = new List<SimulationEvent>();
            public int BossTicks, TankNearestBoss;
            public readonly Dictionary<CompanionRole, (double sum, int n)> DistanceToNearestEnemy = new Dictionary<CompanionRole, (double, int)>();

            public int TeammateDamageTaken => Events.OfType<PlayerDamaged>().Where(d => Teammates.ContainsKey(d.Target)).Sum(d => d.Amount);
            public int TeammateDefeats => Events.OfType<PlayerDefeated>().Count(d => Teammates.ContainsKey(d.Player));
            public int TeammateDamageDealt => Events.OfType<DamageDealt>().Where(d => Teammates.ContainsKey(d.Attacker)).Sum(d => d.Amount);

            public double MeanDistance(CompanionRole role) =>
                DistanceToNearestEnemy.TryGetValue(role, out var v) && v.n > 0 ? v.sum / v.n : double.NaN;

            public string Summary() =>
                $"after {World.Tick / TickRate} s: cleared {World.Gate.IsCleared}, teammates took {TeammateDamageTaken} damage, fell {TeammateDefeats} times, dealt {TeammateDamageDealt}; " +
                $"party defeats {Events.OfType<PlayerDefeated>().Count()}, wipes {Events.OfType<GateWiped>().Count()}; tank nearest the boss {TankNearestBoss}/{BossTicks} ticks; " +
                string.Join(", ", DistanceToNearestEnemy.Keys.OrderBy(r => r).Select(r => $"{r} {MeanDistance(r):0.0} m"));
        }

        /// <param name="seed">0 for the usual fight; others play the same party with different dice.</param>
        private static Run PlayFirstGate(string leaderClass, Func<int, CompanionExperience> experience, int minutes = 10, int seed = 0)
        {
            var config = RealConfig();
            var content = Content();
            var run = new Run { World = new SimulationWorld(config) };
            var world = run.World;
            var leaderId = new PlayerId(1);
            world.AddPlayer(leaderId, Vec3.Zero, new CharacterRecord { IsNew = true, ClassId = leaderClass });
            var party = CompanionParty.NewParty(leaderClass, config);
            for (var i = 0; i < party.Count; i++)
            {
                var id = new PlayerId(i + 2);
                party[i].Experience = experience(i);
                world.AddPlayer(id, Vec3.Zero, party[i].Character);
                run.Teammates[id] = new CompanionBrain(party[i], seed: 7919 * (i + 1) + 104729 * seed) { Leader = leaderId };
            }
            world.StartGate(content.Gates[0].Id);

            var leader = new BotBrain(seed: 1 + seed);
            var commands = new Dictionary<PlayerId, PlayerCommand>();
            for (var tick = 0; tick < minutes * 60 * TickRate && !world.Gate.IsCleared; tick++)
            {
                commands[leaderId] = leader.Next(world, leaderId, world.Events);
                foreach (var pair in run.Teammates) commands[pair.Key] = pair.Value.Next(world, pair.Key, world.Events);
                world.Step(commands);
                run.Events.AddRange(world.Events);
                Observe(run);
            }
            foreach (var pair in run.Teammates) pair.Value.Next(world, pair.Key, world.Events); // as on the host, they see the last step (the clear) on the next tick
            return run;
        }

        /// <summary>Where everyone stands while fighting: who the boss is nearest to, and how far each role keeps from enemies.</summary>
        private static void Observe(Run run)
        {
            var world = run.World;
            var enemies = world.EnemyIds.Select(world.GetEnemy).Where(e => !e.IsDefeated).ToList();
            if (enemies.Count == 0) return;
            var boss = enemies.FirstOrDefault(e => e.IsBoss);
            float Flat(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, 0f, a.Z - b.Z).Length;

            if (boss.Definition != null)
            {
                var standing = world.PlayerIds.Select(world.GetPlayer).Where(p => !p.IsDefeated).ToList();
                if (standing.Count > 0)
                {
                    run.BossTicks++;
                    var nearest = standing.OrderBy(p => Flat(p.Position, boss.Position)).First();
                    if (run.Teammates.TryGetValue(nearest.Id, out var brain) && brain.Role == CompanionRole.Tank) run.TankNearestBoss++;
                }
            }
            foreach (var pair in run.Teammates)
            {
                var me = world.GetPlayer(pair.Key);
                if (me.IsDefeated) continue;
                var distance = enemies.Min(e => Flat(e.Position, me.Position));
                if (distance > 14f) continue; // not in this fight
                run.DistanceToNearestEnemy.TryGetValue(pair.Value.Role, out var v);
                run.DistanceToNearestEnemy[pair.Value.Role] = (v.sum + distance, v.n + 1);
            }
        }

        private static CompanionExperience Rookies(int _) => new CompanionExperience();
        private static CompanionExperience Elites(int _) => new CompanionExperience { GatesCleared = 30, BossesDefeated = 30 };

        [Test]
        public void FiveTeammatesFollowTheirLeaderThroughTheFirstGateAndLearnFromIt()
        {
            var run = PlayFirstGate("warrior", Rookies);

            TestContext.WriteLine(run.Summary());
            Assert.That(run.World.Gate.IsCleared, Is.True, run.Summary());
            foreach (var brain in run.Teammates.Values)
            {
                Assert.That(brain.Record.Experience.GatesCleared, Is.EqualTo(1));
                Assert.That(brain.Record.Experience.BossesDefeated, Is.EqualTo(1));
                Assert.That(brain.Proficiency, Is.GreaterThan(0.4f), $"{brain.Record.Name} is no longer a rookie");
            }
            Assert.That(run.Events.OfType<StatPointAllocated>().Any(), Is.True, "they spend their points");
        }

        [Test]
        public void TeammatesPlayTheirRoles()
        {
            var run = PlayFirstGate("archer", Elites);

            TestContext.WriteLine(run.Summary());
            Assert.That(run.World.Gate.IsCleared, Is.True, run.Summary());
            Assert.That(run.TankNearestBoss, Is.GreaterThan(run.BossTicks / 2), "the tank holds the boss. " + run.Summary());
            var melee = Math.Max(run.MeanDistance(CompanionRole.Tank), run.MeanDistance(CompanionRole.Skirmisher));
            Assert.That(run.MeanDistance(CompanionRole.Marksman), Is.GreaterThan(melee + 2), "the archer fights from range. " + run.Summary());
            Assert.That(run.MeanDistance(CompanionRole.Caster), Is.GreaterThan(melee + 2), "the wizard fights from range. " + run.Summary());

            var vitality = run.Teammates.Where(p => p.Value.Role == CompanionRole.Tank).Select(p => run.World.GetPlayer(p.Key).Stats[Stat.Vitality]);
            var intelligence = run.Teammates.Where(p => p.Value.Role == CompanionRole.Caster).Select(p => run.World.GetPlayer(p.Key).Stats[Stat.Intelligence]);
            Assert.That(vitality.All(v => v > RealConfig().Progression.StartingStatValue), Is.True, "the tank builds Vitality");
            Assert.That(intelligence.All(v => v > RealConfig().Progression.StartingStatValue), Is.True, "the wizard builds Intelligence");
        }

        [Test]
        public void ExperiencedTeammatesPlayBetterThanRookies()
        {
            // Four fights each, with different dice: one fight is too few to say who holds the boss more.
            var seeds = Enumerable.Range(0, 4).ToList();
            var rookies = seeds.Select(s => PlayFirstGate("wizard", Rookies, seed: s)).ToList();
            var elites = seeds.Select(s => PlayFirstGate("wizard", Elites, seed: s)).ToList();

            foreach (var run in rookies) TestContext.WriteLine("Rookies: " + run.Summary());
            foreach (var run in elites) TestContext.WriteLine("Elites:  " + run.Summary());
            foreach (var run in elites) Assert.That(run.World.Gate.IsCleared, Is.True, run.Summary());
            Assert.That(elites.Sum(r => r.TeammateDamageTaken), Is.LessThan(rookies.Sum(r => r.TeammateDamageTaken) / 2), "they take far fewer hits");
            Assert.That((long)elites.Sum(r => r.TankNearestBoss) * rookies.Sum(r => r.BossTicks), Is.GreaterThan((long)rookies.Sum(r => r.TankNearestBoss) * elites.Sum(r => r.BossTicks)),
                "their tank holds the boss more of the time");
            Assert.That(elites.Sum(r => r.World.Tick), Is.LessThanOrEqualTo(rookies.Sum(r => r.World.Tick) * 21 / 20), "and playing safe does not slow the clear");
        }

        // --- On the host -------------------------------------------------------------------

        [Test]
        public void TheHostsTeammatesJoinItsSessionReadyUpInTheHubAndAreSavedWithWhatTheyLearned()
        {
            var config = RealConfig();
            HostSession host = null;
            var network = new SimulatedNetwork(2);
            host = new HostSession(config, network.HostEndpoint, id => JoinPoint.For(host.World, id, Vec3.Zero));
            var me = host.AddLocalPlayer(new CharacterRecord { IsNew = true, ClassId = "rogue" });
            var party = CompanionParty.NewParty("rogue", config);
            foreach (var (record, i) in party.Select((r, i) => (r, i))) host.AddCompanion(record, seed: i + 1);

            Assert.That(host.World.PlayerIds.Count, Is.EqualTo(6));
            Assert.That(host.Companions.All(c => c.brain.Leader == me), Is.True, "they follow the host");
            Assert.That(host.IsCompanion(me), Is.False);
            Assert.That(host.World.InHub, Is.True, "the real content starts in the hub");

            for (var tick = 0; tick < 30; tick++) host.Tick(PlayerCommand.None);
            Assert.That(host.Companions.All(c => host.World.GetPlayer(c.id).IsReady), Is.True, "they never hold the party up");

            host.Companions[0].brain.Record.Experience.GatesCleared = 2;
            var saved = CompanionCodec.Decode(CompanionCodec.Encode(host.CaptureCompanions()));
            Assert.That(saved.Count, Is.EqualTo(5));
            Assert.That(saved[0].Experience.GatesCleared, Is.EqualTo(2));
            Assert.That(saved.Select(c => c.ClassId), Is.EqualTo(party.Select(c => c.ClassId)));
        }

        [Test]
        public void TeammatesNeverTakeASeatPastTheSessionLimit()
        {
            HostSession host = null;
            var network = new SimulatedNetwork(2);
            host = new HostSession(new SimulationConfig(), network.HostEndpoint, _ => Vec3.Zero);
            host.AddLocalPlayer();
            for (var i = 0; i < HostSession.MaxPlayers - 1; i++) host.AddCompanion(new CompanionRecord(), i);

            Assert.Throws<InvalidOperationException>(() => host.AddCompanion(new CompanionRecord(), 99));
        }
    }
}
