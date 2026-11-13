using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Host and client sessions over a simulated network: the host runs the real simulation,
    /// clients predict their own player and correct from host snapshots, and joining or rejoining
    /// a running session sends the whole world. No engine and no real network involved.
    /// </summary>
    public class NetSessionTest
    {
        private const int Latency = 6; // ticks each way: 100 ms at 60 Hz

        private static SimulationConfig Config(float moveSpeed = 5f) => new SimulationConfig
        {
            TickRate = 60,
            PlayerMoveSpeed = moveSpeed,
            Progression = new ProgressionDefinition
            {
                LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 }, FreePointsPerLevel = 3, StartingStatValue = 10,
            },
        };

        private static EnemyDefinition Prey => new EnemyDefinition
        {
            Name = "Prey", MaxHealth = 10, MoveSpeed = 0f, AttackDamage = 0, AttackRange = 0f, XpReward = 60,
        };

        private sealed class Rig
        {
            public readonly SimulatedNetwork Network = new SimulatedNetwork(Latency);
            public readonly HostSession Host;
            public readonly List<ClientSession> Clients = new List<ClientSession>();
            public readonly Dictionary<ClientSession, PlayerCommand> Input = new Dictionary<ClientSession, PlayerCommand>();
            public PlayerCommand HostInput;

            public Rig(SimulationConfig hostConfig = null, int snapshotEveryTicks = 3, Func<PlayerId, Vec3> spawnPoint = null)
            {
                Host = new HostSession(hostConfig ?? Config(), Network.HostEndpoint, spawnPoint ?? (_ => new Vec3(0f, 0f, 0f)), snapshotEveryTicks);
                Host.AddLocalPlayer();
            }

            public ClientSession Join(string key, SimulationConfig clientConfig = null, CharacterRecord character = null)
            {
                var client = new ClientSession(clientConfig ?? Config(), Network.Connect(), key, character);
                Clients.Add(client);
                Input[client] = PlayerCommand.None;
                return client;
            }

            /// <summary>One frame: deliver due messages, the host ticks, then each client ticks with its input.</summary>
            public void Tick(int times = 1)
            {
                for (var i = 0; i < times; i++)
                {
                    Network.Advance();
                    Host.Tick(HostInput);
                    foreach (var c in Clients) c.Tick(Input[c]);
                }
            }

            public PlayerState HostView(ClientSession c) => Host.World.GetPlayer(c.PlayerId.Value);
        }

        private static readonly PlayerCommand East = new PlayerCommand(1f, 0f);

        [Test]
        public void AJoiningClientIsWelcomedWithItsOwnPlayer()
        {
            var rig = new Rig();
            var client = rig.Join("alice");

            rig.Tick(Latency * 2 + 2);

            Assert.That(client.IsJoined, Is.True);
            Assert.That(rig.Host.World.PlayerIds, Has.Member(client.PlayerId.Value));
            Assert.That(client.World.PlayerIds, Is.EquivalentTo(rig.Host.World.PlayerIds));
        }

        [Test]
        public void TheClientsOwnMovementShowsOnItsFirstTick()
        {
            var rig = new Rig();
            var client = rig.Join("alice");
            rig.Tick(Latency * 2 + 2);
            var start = client.World.GetPlayer(client.PlayerId.Value).Position.X;

            rig.Input[client] = East;
            rig.Tick();

            Assert.That(client.World.GetPlayer(client.PlayerId.Value).Position.X - start, Is.EqualTo(5f / 60f).Within(1e-4f),
                "predicted at once, though the host is 100 ms away");
            Assert.That(rig.HostView(client).Position.X, Is.EqualTo(start).Within(1e-4f), "the host has not heard yet");
        }

        [Test]
        public void ClientAndHostAgreeOnceTheCommandsHaveArrived()
        {
            var rig = new Rig();
            var client = rig.Join("alice");
            rig.Tick(Latency * 2 + 2);

            rig.Input[client] = East;
            rig.Tick(60);
            rig.Input[client] = PlayerCommand.None;
            rig.Tick(Latency * 3);

            var predicted = client.World.GetPlayer(client.PlayerId.Value).Position;
            var authoritative = rig.HostView(client).Position;
            Assert.That(authoritative.X, Is.EqualTo(5f).Within(1e-3f), "60 ticks of movement, all processed by the host");
            Assert.That(predicted.X, Is.EqualTo(authoritative.X).Within(1e-4f));
        }

        [Test]
        public void AMispredictionIsCorrectedFromTheHost()
        {
            var rig = new Rig(hostConfig: Config(moveSpeed: 5f));
            var client = rig.Join("alice", clientConfig: Config(moveSpeed: 6f)); // the client believes it is faster
            rig.Tick(Latency * 2 + 2);

            rig.Input[client] = East;
            rig.Tick(60);
            rig.Input[client] = PlayerCommand.None;
            rig.Tick(Latency * 3);

            Assert.That(client.LargestCorrection, Is.GreaterThan(0.01f), "the client was pulled back");
            Assert.That(client.World.GetPlayer(client.PlayerId.Value).Position.X,
                Is.EqualTo(rig.HostView(client).Position.X).Within(1e-4f), "and ends where the host says");
        }

        [Test]
        public void ButtonPressesSurviveTheTrip()
        {
            var rig = new Rig();
            var client = rig.Join("alice");
            rig.Tick(Latency * 2 + 2);
            rig.Host.World.SpawnEnemy(Prey, new Vec3(0f, 0f, 1f));

            rig.Input[client] = new PlayerCommand(0f, 0f, lightAttack: true);
            rig.Tick();
            rig.Input[client] = PlayerCommand.None;
            rig.Tick(Latency * 3);

            // 60 XP against a 50 XP first level: level 2 with 10 over.
            Assert.That(rig.HostView(client).Level, Is.EqualTo(2), "the host ran the attack and the kill counted");
            Assert.That(client.World.GetPlayer(client.PlayerId.Value).Level, Is.EqualTo(2), "and the client's world shows it");
        }

        [Test]
        public void WhenCommandsGoMissingTheHostKeepsThePlayerMovingButPressesNothing()
        {
            var rig = new Rig();
            var client = rig.Join("alice");
            rig.Tick(Latency * 2 + 2);
            rig.Input[client] = East;
            rig.Tick(10);
            var before = rig.HostView(client).Position.X;

            rig.Network.DropClientMessages = true; // a burst of loss
            rig.Tick(Latency + 20);
            rig.Network.DropClientMessages = false;

            Assert.That(rig.HostView(client).Position.X, Is.GreaterThan(before + 1f), "still walking east through the gap");
        }

        [Test]
        public void ClientsSeeEachOther()
        {
            var rig = new Rig();
            var alice = rig.Join("alice");
            var bob = rig.Join("bob");
            rig.Tick(Latency * 2 + 2);

            rig.Input[bob] = East;
            rig.Tick(30);
            rig.Input[bob] = PlayerCommand.None;
            rig.Tick(Latency * 3);

            Assert.That(alice.World.GetPlayer(bob.PlayerId.Value).Position.X,
                Is.EqualTo(rig.HostView(bob).Position.X).Within(1e-4f), "alice sees bob where the host has him");
        }

        [Test]
        public void ALateJoinerReceivesTheWholeRunningWorld()
        {
            var rig = new Rig();
            var alice = rig.Join("alice");
            rig.Tick(Latency * 2 + 2);
            rig.Host.World.SpawnEnemy(Prey, new Vec3(3f, 0f, 3f));
            rig.Input[alice] = new PlayerCommand(0.5f, 0.5f, lightAttack: true);
            rig.Tick(200);

            var carol = rig.Join("carol");
            rig.Tick(Latency * 2 + 1);

            Assert.That(carol.IsJoined, Is.True);
            Assert.That(carol.World.PlayerIds, Is.EquivalentTo(rig.Host.World.PlayerIds));
            Assert.That(carol.World.GetPlayer(alice.PlayerId.Value).Level, Is.EqualTo(rig.HostView(alice).Level));
            Assert.That(carol.World.EnemyIds, Is.EqualTo(rig.Host.World.EnemyIds));
        }

        [Test]
        public void ARejoiningPlayerGetsTheirCharacterBack()
        {
            var rig = new Rig();
            var alice = rig.Join("alice");
            rig.Tick(Latency * 2 + 2);
            rig.Host.World.SpawnEnemy(Prey, new Vec3(0f, 0f, 1f));
            rig.Input[alice] = new PlayerCommand(0f, 0f, lightAttack: true);
            rig.Tick(Latency * 3);
            var id = alice.PlayerId.Value;
            Assert.That(rig.Host.World.GetPlayer(id).Level, Is.EqualTo(2));

            alice.Disconnect();
            rig.Clients.Remove(alice);
            rig.Tick(30);
            var again = rig.Join("alice");
            rig.Tick(Latency * 2 + 2);

            Assert.That(again.PlayerId, Is.EqualTo(id), "the same player, not a new one");
            Assert.That(again.World.GetPlayer(id).Level, Is.EqualTo(2), "with their progress");
            Assert.That(rig.Host.World.PlayerIds.Count(p => p == id), Is.EqualTo(1));
        }

        [Test]
        public void TheHostsOwnPlayerIsDrivenLocally()
        {
            var rig = new Rig();
            var start = rig.Host.World.GetPlayer(rig.Host.LocalPlayerId).Position.X;

            rig.HostInput = East;
            rig.Tick(60);

            Assert.That(rig.Host.World.GetPlayer(rig.Host.LocalPlayerId).Position.X - start, Is.EqualTo(5f).Within(1e-3f));
        }

        // --- Review fixes: hostile or broken input, identity, backlog, refusal ------------------

        [Test]
        public void GarbageFromAClientNeverStopsTheHost()
        {
            var rig = new Rig();
            var alice = rig.Join("alice");
            var raw = rig.Network.Connect();
            rig.Tick(Latency * 2 + 2);
            var before = rig.Host.World.Tick;

            raw.Send(new byte[0]);
            raw.Send(new byte[] { (byte)MessageType.Command });          // a command cut short
            raw.Send(new byte[] { 99, 1, 2, 3 });                         // no such message type
            raw.Send(Wire.Hello("mallory"));
            raw.Send(new byte[] { (byte)MessageType.Command, 1, 0 });     // another truncated command, now as a player
            rig.Input[alice] = East;
            rig.Tick(Latency + 10);

            Assert.That(rig.Host.World.Tick, Is.EqualTo(before + Latency + 10), "every tick still ran");
            Assert.That(rig.Host.MalformedMessages, Is.GreaterThanOrEqualTo(3));
            Assert.That(rig.HostView(alice).Position.X, Is.GreaterThan(0.1f), "and other players carried on");
        }

        [Test]
        public void ImpossibleCommandValuesAreNeutralised()
        {
            var rig = new Rig();
            var raw = rig.Network.Connect();
            raw.Send(Wire.Hello("mallory"));
            rig.Tick(Latency * 2 + 2);
            var mallory = rig.Host.World.PlayerIds.Last();
            var start = rig.Host.World.GetPlayer(mallory).Position;

            raw.Send(Wire.Command(1, new PlayerCommand(float.NaN, float.PositiveInfinity, allocateStat: (Stat)99)));
            rig.Tick(Latency + 2);

            var position = rig.Host.World.GetPlayer(mallory).Position;
            Assert.That(float.IsNaN(position.X) || float.IsInfinity(position.Z), Is.False);
            Assert.That(position, Is.EqualTo(start), "not moved by a non-number");
        }

        [Test]
        public void TheTransportsIdentityBeatsTheKeyAClientClaims()
        {
            var rig = new Rig();
            var aliceTransport = rig.Network.Connect(identity: "steam:alice");
            var alice = new ClientSession(Config(), aliceTransport, "steam:alice");
            rig.Clients.Add(alice);
            rig.Input[alice] = PlayerCommand.None;
            rig.Tick(Latency * 2 + 2);

            // Mallory connects as herself but claims Alice's key to take her character.
            var mallory = new ClientSession(Config(), rig.Network.Connect(identity: "steam:mallory"), "steam:alice");
            rig.Clients.Add(mallory);
            rig.Input[mallory] = PlayerCommand.None;
            rig.Tick(Latency * 2 + 2);

            Assert.That(mallory.PlayerId, Is.Not.EqualTo(alice.PlayerId), "a character of her own, not Alice's");
            Assert.That(rig.Host.ConnectedClients, Is.EqualTo(2), "and Alice is still connected");
        }

        [Test]
        public void ABacklogOfCommandsCannotGrowWithoutLimit()
        {
            var rig = new Rig();
            var client = rig.Join("alice");
            rig.Tick(Latency * 2 + 2);

            int halfway = 0;
            for (var frame = 0; frame < 300; frame++)
            {
                client.Tick(East); // a client whose clock runs fast: two commands per host tick
                rig.Tick();
                if (frame == 150) halfway = client.UnconfirmedCommands;
            }

            // Without shedding this grows by one a frame (to about 300). With it, it levels off at
            // twice (both ways in flight + the cap + a snapshot interval), since commands come twice as fast.
            // It ripples by a snapshot interval's worth (3 ticks, at double rate) as acknowledgements arrive.
            Assert.That(client.UnconfirmedCommands, Is.LessThanOrEqualTo(halfway + 2 * 3 + 2), "no longer growing");
            Assert.That(client.UnconfirmedCommands, Is.LessThan(2 * (Latency * 2 + HostSession.MaxBacklog + 3)));
        }

        [Test]
        public void PressesAreNeverDroppedFromABacklog()
        {
            var rig = new Rig();
            var client = rig.Join("alice");
            rig.Tick(Latency * 2 + 2);
            rig.Host.World.SpawnEnemy(Prey, new Vec3(0f, 0f, 1f));

            for (var i = 0; i < 20; i++) client.Tick(PlayerCommand.None);           // a burst of movement-only commands
            client.Tick(new PlayerCommand(0f, 0f, lightAttack: true));              // then an attack at the back of the queue
            rig.Tick(Latency * 4);

            Assert.That(rig.HostView(client).Level, Is.EqualTo(2), "the attack still landed and the kill counted");
        }

        [Test]
        public void AFullSessionTurnsANewcomerAwayWithAReason()
        {
            var rig = new Rig();
            for (var i = 0; i < HostSession.MaxPlayers - 1; i++) rig.Join($"player-{i}");
            var late = rig.Join("one-too-many");

            rig.Tick(Latency * 2 + 2);

            Assert.That(rig.Host.World.PlayerIds.Count, Is.EqualTo(HostSession.MaxPlayers));
            Assert.That(late.IsJoined, Is.False);
            Assert.That(late.RefusalReason, Does.Contain("full"));
        }

        [Test]
        public void AClientKnowsWhenItsConnectionIsGone()
        {
            var rig = new Rig();
            var client = rig.Join("alice");
            rig.Tick(Latency * 2 + 2);
            Assert.That(client.IsConnected, Is.True);

            client.Disconnect();

            Assert.That(client.IsConnected, Is.False);
        }

        [Test]
        public void TheHostCountsTheBytesItSendsAndReceives()
        {
            var rig = new Rig();
            rig.Join("alice");
            rig.Join("bob");
            rig.Input[rig.Clients[0]] = East;
            rig.Tick(120);

            Assert.That(rig.Host.BytesSent, Is.EqualTo(rig.Network.BytesToClients).And.GreaterThan(0));
            // Received lags sent by the latency: what is still in flight has not arrived.
            Assert.That(rig.Host.BytesReceived, Is.GreaterThan(0).And.LessThanOrEqualTo(rig.Network.BytesToHost));
        }

        // --- Ticket 13: what clients show -------------------------------------------------------

        /// <summary>Ticks the rig, collecting each client's presentation events.</summary>
        private static Dictionary<ClientSession, List<SimulationEvent>> TickAndCollect(Rig rig, int times)
        {
            var seen = rig.Clients.ToDictionary(c => c, _ => new List<SimulationEvent>());
            for (var i = 0; i < times; i++)
            {
                rig.Tick();
                foreach (var c in rig.Clients) seen[c].AddRange(c.Events);
            }
            return seen;
        }

        [Test]
        public void EveryClientSeesAKillOnceAndTheAttackerSeesTheirSwingAtOnce()
        {
            var rig = new Rig();
            var alice = rig.Join("alice");
            var bob = rig.Join("bob");
            rig.Tick(Latency * 2 + 2);
            rig.Host.World.SpawnEnemy(Prey, new Vec3(0f, 0f, 1f));
            rig.Tick(Latency * 2); // the enemy reaches both clients

            rig.Input[bob] = new PlayerCommand(0f, 0f, lightAttack: true);
            rig.Tick();
            var bobsFirstTick = bob.Events.ToList();
            rig.Input[bob] = PlayerCommand.None;
            var seen = TickAndCollect(rig, Latency * 3);
            seen[bob].InsertRange(0, bobsFirstTick);

            var bobId = bob.PlayerId.Value;
            Assert.That(bobsFirstTick.OfType<AttackPerformed>().Count(a => a.Player == bobId), Is.EqualTo(1),
                "bob's swing shows on the tick he pressed, predicted");
            foreach (var client in new[] { alice, bob })
            {
                var events = seen[client];
                var who = client == bob ? "bob" : "alice";
                Assert.That(events.OfType<AttackPerformed>().Count(a => a.Player == bobId), Is.EqualTo(1), $"{who} sees the swing once");
                Assert.That(events.OfType<EnemyDefeated>().Count(d => d.DefeatedBy == bobId), Is.EqualTo(1), $"{who} sees the kill once");
                Assert.That(events.OfType<DamageDealt>().Count(d => d.Attacker == bobId), Is.EqualTo(1), $"{who} sees the hit once");
                Assert.That(events.OfType<PlayerLeveledUp>().Count(l => l.Player == bobId), Is.EqualTo(1), $"{who} sees bob level up");
            }
        }

        [Test]
        public void ALateJoinerIsNotShownWhatHappenedBeforeItArrived()
        {
            var rig = new Rig(snapshotEveryTicks: 30); // a long gap, so events wait a while before they are sent
            rig.Host.World.SpawnEnemy(Prey, new Vec3(0f, 0f, 1f));
            rig.HostInput = new PlayerCommand(0f, 0f, lightAttack: true);
            rig.Tick();
            rig.HostInput = PlayerCommand.None;

            var carol = rig.Join("carol"); // welcomed before the snapshot carrying the kill goes out
            var seen = TickAndCollect(rig, 60);

            Assert.That(carol.IsJoined, Is.True);
            Assert.That(seen[carol].OfType<EnemyDefeated>(), Is.Empty);
        }

        [Test]
        public void RemotePlayersGlideBetweenSnapshotsAndArriveWhereTheHostSays()
        {
            var rig = new Rig();
            var alice = rig.Join("alice");
            var bob = rig.Join("bob");
            rig.Tick(Latency * 2 + 2);
            var bobId = bob.PlayerId.Value;
            var step = 5f / 60f;

            rig.Input[bob] = East;
            rig.Tick(Latency * 3); // bob is walking steadily by now
            var largestRawStep = 0f;
            var largestShownStep = 0f;
            for (var i = 0; i < 30; i++)
            {
                var raw = alice.World.GetPlayer(bobId).Position;
                var shown = alice.DisplayPosition(bobId);
                rig.Tick();
                largestRawStep = Math.Max(largestRawStep, (alice.World.GetPlayer(bobId).Position - raw).Length);
                largestShownStep = Math.Max(largestShownStep, (alice.DisplayPosition(bobId) - shown).Length);
            }

            Assert.That(largestRawStep, Is.GreaterThan(2.5f * step), "the world itself jumps a snapshot's worth at a time");
            Assert.That(largestShownStep, Is.LessThan(1.5f * step), "but alice is shown bob walking at his own pace");

            rig.Input[bob] = PlayerCommand.None;
            rig.Tick(Latency * 3 + 6);
            Assert.That(alice.DisplayPosition(bobId).X, Is.EqualTo(rig.HostView(bob).Position.X).Within(1e-4f), "and he ends where the host has him");
        }

        [Test]
        public void TheClientsOwnPlayerIsShownWhereItIsPredicted()
        {
            var rig = new Rig();
            var alice = rig.Join("alice");
            rig.Tick(Latency * 2 + 2);

            rig.Input[alice] = East;
            rig.Tick(10);

            var me = alice.PlayerId.Value;
            Assert.That(alice.DisplayPosition(me), Is.EqualTo(alice.World.GetPlayer(me).Position));
        }

        [Test]
        public void AJoinerAppearsBesideTheParty()
        {
            var world = new SimulationWorld(Config());
            var fallback = new Vec3(0f, 0f, -20f);
            var first = new PlayerId(1);
            Assert.That(JoinPoint.BesideParty(world, first, fallback), Is.EqualTo(fallback), "nobody to stand beside yet");

            world.AddPlayer(first, new Vec3(40f, 0f, 12f));
            var second = JoinPoint.BesideParty(world, new PlayerId(2), fallback);
            world.AddPlayer(new PlayerId(2), second);
            var third = JoinPoint.BesideParty(world, new PlayerId(3), fallback);

            Assert.That((second - new Vec3(40f, 0f, 12f)).Length, Is.InRange(1.5f, 3f), "close to the party");
            Assert.That((third - second).Length, Is.GreaterThan(1f), "and not on top of each other");
        }

        [Test]
        public void ARejoinerAppearsBesideTheParty()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(new PlayerId(1), new Vec3(40f, 0f, 12f));
            world.AddPlayer(new PlayerId(2), new Vec3(-30f, 0f, 0f)); // left the game a long way back

            var point = JoinPoint.BesideParty(world, new PlayerId(2), Vec3.Zero);

            Assert.That((point - new Vec3(40f, 0f, 12f)).Length, Is.LessThan(3f), "beside the others, not where they left");
        }

        [Test]
        public void TheHostPlacesARejoinerAtTheJoinPointAgain()
        {
            var joinAt = new Vec3(0f, 0f, 0f);
            var rig = new Rig(spawnPoint: _ => joinAt);
            var alice = rig.Join("alice");
            rig.Tick(Latency * 2 + 2);
            rig.Input[alice] = East;
            rig.Tick(30);
            alice.Disconnect();
            rig.Clients.Remove(alice);
            rig.Tick(Latency + 2);

            joinAt = new Vec3(0f, 0f, 50f); // the party has moved on
            var again = rig.Join("alice");
            rig.Tick(Latency * 2 + 2);

            Assert.That(rig.HostView(again).Position, Is.EqualTo(joinAt));
            Assert.That(again.World.GetPlayer(again.PlayerId.Value).Position, Is.EqualTo(joinAt), "and the welcome shows it");
        }

        [Test]
        public void ASecondConnectionWithTheSameKeyTakesOverWithoutMovingThePlayer()
        {
            var rig = new Rig(spawnPoint: _ => new Vec3(0f, 0f, 50f));
            var alice = rig.Join("alice");
            rig.Tick(Latency * 2 + 2);
            rig.Input[alice] = East;
            rig.Tick(30);
            rig.Input[alice] = PlayerCommand.None;
            rig.Tick(Latency * 2);
            var before = rig.HostView(alice).Position;

            var again = rig.Join("alice"); // the old connection is still live
            rig.Tick(Latency * 2 + 2);

            Assert.That(again.PlayerId, Is.EqualTo(alice.PlayerId));
            Assert.That(rig.HostView(again).Position, Is.EqualTo(before), "a takeover, not a return: stays put");
        }
        // --- Ticket 15: guests bring their own character -----------------------------------------

        private static CharacterRecord Veteran => new CharacterRecord { Level = 4, Xp = 20, UnspentStatPoints = 2, Stats = new[] { 12, 10, 10, 15, 10, 10 } };

        [Test]
        public void AGuestBringsTheirOwnCharacterIntoTheHostsSession()
        {
            var rig = new Rig();
            var guest = rig.Join("alice", character: Veteran);

            rig.Tick(Latency * 2 + 2);

            var onHost = rig.HostView(guest);
            Assert.That((onHost.Level, onHost.Xp, onHost.UnspentStatPoints), Is.EqualTo((4, 20, 2)));
            Assert.That(onHost.Stats[Stat.Strength], Is.EqualTo(12));
            Assert.That(guest.World.GetPlayer(guest.PlayerId.Value).Level, Is.EqualTo(4), "and the guest sees themselves as they are");
            Assert.That(rig.Host.World.GetPlayer(rig.Host.LocalPlayerId).Level, Is.EqualTo(1), "the host's own character is its own");
        }

        [Test]
        public void WhatAGuestEarnsInTheSessionIsInTheCharacterTheyTakeHome()
        {
            var rig = new Rig();
            var guest = rig.Join("alice", character: Veteran);
            rig.Tick(Latency * 2 + 2);
            rig.Host.World.SpawnEnemy(Prey, new Vec3(0f, 0f, 1f));
            rig.Tick(Latency * 2);

            rig.Input[guest] = new PlayerCommand(0f, 0f, lightAttack: true);
            rig.Tick();
            rig.Input[guest] = PlayerCommand.None;
            rig.Tick(Latency * 3);

            // The guest's own machine saves this: its character as the host last confirmed it.
            var takenHome = guest.ConfirmedCharacter();
            Assert.That(takenHome.Level, Is.EqualTo(4));
            Assert.That(takenHome.Xp, Is.EqualTo(20 + 60), "the kill's XP came home");
        }

        [Test]
        public void ARejoinerKeepsTheCharacterFromTheSessionNotTheOneTheySend()
        {
            var rig = new Rig();
            var alice = rig.Join("alice", character: Veteran);
            rig.Tick(Latency * 2 + 2);
            rig.Host.World.GrantXp(alice.PlayerId.Value, 500); // progress made in the session
            rig.Tick(2);
            var earned = rig.HostView(alice).Level;
            alice.Disconnect();
            rig.Clients.Remove(alice);
            rig.Tick(Latency + 2);

            var again = rig.Join("alice", character: Veteran); // their save from before the session
            rig.Tick(Latency * 2 + 2);

            Assert.That(earned, Is.GreaterThan(4));
            Assert.That(rig.HostView(again).Level, Is.EqualTo(earned), "the session's character, with what it earned");
        }

        [Test]
        public void ARejoinerWhoseOwnSaveHasMovedOnBringsThatInstead()
        {
            // They left, played elsewhere (or restored a backup), and came back to the same session.
            var rig = new Rig();
            var alice = rig.Join("alice", character: Veteran);
            rig.Tick(Latency * 2 + 2);
            alice.Disconnect();
            rig.Clients.Remove(alice);
            rig.Tick(Latency + 2);

            var further = new CharacterRecord { Level = 6, Xp = 5, Stats = new[] { 20, 10, 10, 15, 10, 10 } };
            var again = rig.Join("alice", character: further);
            rig.Tick(Latency * 2 + 2);

            Assert.That(again.PlayerId, Is.EqualTo(alice.PlayerId), "the same player in the session");
            Assert.That(rig.HostView(again).Level, Is.EqualTo(6), "with the character that is further along");
            Assert.That(rig.HostView(again).Stats[Stat.Strength], Is.EqualTo(20));
        }

        [Test]
        public void TheCharacterAGuestSavesIsTheHostsViewNotItsOwnGuess()
        {
            var rig = new Rig();
            var guest = rig.Join("alice", character: Veteran);
            Assert.That(guest.ConfirmedCharacter(), Is.Null, "nothing to save before the host has welcomed it");
            rig.Tick(Latency * 2 + 2);

            // The guest asks to spend a point; its own world shows it at once, the host has not heard yet.
            rig.Input[guest] = new PlayerCommand(0f, 0f, allocateStat: Stat.Agility);
            rig.Tick();
            rig.Input[guest] = PlayerCommand.None;

            var me = guest.PlayerId.Value;
            Assert.That(guest.World.GetPlayer(me).UnspentStatPoints, Is.EqualTo(1), "predicted");
            Assert.That(guest.ConfirmedCharacter().UnspentStatPoints, Is.EqualTo(2), "not yet confirmed, so not yet saved");

            rig.Tick(Latency * 3);
            Assert.That(guest.ConfirmedCharacter().UnspentStatPoints, Is.EqualTo(1), "confirmed by the host");
        }

        [Test]
        public void ACharacterTheHostCannotReadIsTurnedAwayRatherThanReplaced()
        {
            // For example a guest on a newer version. Letting them in as a fresh level 1 would risk their save.
            var rig = new Rig();
            var raw = rig.Network.Connect();
            raw.Send(Wire.Hello("mallory", new byte[] { 1, 2, 3, 4, 5 }));
            var before = rig.Host.World.PlayerIds.Count;

            rig.Tick(Latency * 2 + 2);

            Assert.That(rig.Host.World.PlayerIds.Count, Is.EqualTo(before));
            Assert.That(raw.TryReceive(out var reply), Is.True);
            Assert.That(Wire.TypeOf(reply), Is.EqualTo(MessageType.Refused));
            Assert.That(Wire.ReadRefused(reply), Does.Contain("character"));
        }

        [Test]
        public void TheHostBringsItsOwnCharacterToo()
        {
            var network = new SimulatedNetwork(Latency);
            var host = new HostSession(Config(), network.HostEndpoint, _ => Vec3.Zero);

            var id = host.AddLocalPlayer(Veteran);

            Assert.That(host.World.GetPlayer(id).Level, Is.EqualTo(4));
        }
        // --- Ticket 17: gates over the network ---------------------------------------------------

        private static GateDefinition Corridor() => new GateDefinition
        {
            Id = "corridor", Name = "Corridor",
            Rooms =
            {
                new RoomDefinition { Width = 10f, Length = 10f },
                new RoomDefinition { Width = 10f, Length = 20f, HasCheckpoint = true, Spawns = { new RoomSpawn(Prey, new Vec3(0f, 0f, 8f)) } },
            },
        };

        private static SimulationConfig GateConfig()
        {
            var config = Config();
            config.Gates = new[] { Corridor() };
            return config;
        }

        private static readonly Vec3 CorridorCheckpoint = new Vec3(0f, 0f, 20f);

        [Test]
        public void AGuestSeesTheGateAndItsWallsHoldInItsOwnPrediction()
        {
            var rig = new Rig(hostConfig: GateConfig());
            var guest = rig.Join("alice", clientConfig: GateConfig());
            rig.Tick(Latency * 2 + 2);
            rig.Host.World.StartGate("corridor");
            rig.Tick(Latency * 2);
            Assert.That(guest.World.HasGate, Is.True);

            var me = guest.PlayerId.Value;
            rig.Input[guest] = new PlayerCommand(-1f, 0f); // west, into the wall, for three seconds
            var furthestWest = 0f;
            for (var i = 0; i < 180; i++)
            {
                rig.Tick();
                furthestWest = Math.Min(furthestWest, guest.World.GetPlayer(me).Position.X);
            }

            Assert.That(furthestWest, Is.EqualTo(-5f).Within(0.01f), "the guest's own prediction stops at the wall, never beyond it");
            Assert.That(guest.World.GetPlayer(me).Position.X, Is.EqualTo(-5f).Within(0.01f));
            Assert.That(rig.HostView(guest).Position.X, Is.EqualTo(-5f).Within(0.01f));
        }

        [Test]
        public void AFriendJoiningARunningGateAppearsAtTheLatestCheckpoint()
        {
            HostSession host = null;
            var network = new SimulatedNetwork(Latency);
            host = new HostSession(GateConfig(), network.HostEndpoint, id => JoinPoint.For(host.World, id, Vec3.Zero));
            var hostId = host.AddLocalPlayer();
            host.World.StartGate("corridor");
            host.World.PlacePlayer(hostId, CorridorCheckpoint);
            host.Tick(PlayerCommand.None);
            Assert.That(host.World.Gate.LatestCheckpoint, Is.EqualTo(0));

            var guest = new ClientSession(GateConfig(), network.Connect(), "alice");
            for (var i = 0; i < Latency * 2 + 2; i++)
            {
                network.Advance();
                host.Tick(PlayerCommand.None);
                guest.Tick(PlayerCommand.None);
            }

            Assert.That(guest.IsJoined, Is.True);
            Assert.That((host.World.GetPlayer(guest.PlayerId.Value).Position - CorridorCheckpoint).Length, Is.LessThan(1.5f));
        }

        [Test]
        public void ADisconnectedPlayerIsAwayUntilTheyRejoin()
        {
            var rig = new Rig();
            var alice = rig.Join("alice");
            rig.Tick(Latency * 2 + 2);
            var id = alice.PlayerId.Value;
            Assert.That(rig.Host.World.GetPlayer(id).IsAway, Is.False);

            alice.Disconnect();
            rig.Clients.Remove(alice);
            rig.Tick(Latency + 2);
            Assert.That(rig.Host.World.GetPlayer(id).IsAway, Is.True);

            rig.Join("alice");
            rig.Tick(Latency * 2 + 2);
            Assert.That(rig.Host.World.GetPlayer(id).IsAway, Is.False);
        }

        [Test]
        public void TheHostAloneWithADisconnectedFriendPlaysByTheSoloRule()
        {
            var rig = new Rig(hostConfig: GateConfig());
            var alice = rig.Join("alice", clientConfig: GateConfig());
            rig.Tick(Latency * 2 + 2);
            rig.Host.World.StartGate("corridor");
            alice.Disconnect();
            rig.Clients.Remove(alice);
            rig.Tick(Latency + 2);

            Assert.That(rig.Host.World.Gate.IsSolo, Is.True, "the friend's character is still there, but away");
        }

        [Test]
        public void AFallenGuestIsBroughtBackWhenTheHostReachesACheckpointAndSeesItHappen()
        {
            var rig = new Rig(hostConfig: GateConfig());
            var guest = rig.Join("alice", clientConfig: GateConfig());
            rig.Tick(Latency * 2 + 2);
            var world = rig.Host.World;
            world.StartGate("corridor");
            var guestId = guest.PlayerId.Value;
            var killer = world.SpawnEnemy(new EnemyDefinition
            {
                Name = "Killer", MaxHealth = 1, MoveSpeed = 0f, AttackDamage = 100000, AttackRange = 1f, AttackWindupSeconds = 0.05f,
            }, world.GetPlayer(guestId).Position);
            rig.Tick(20);
            world.DespawnEnemy(killer);
            Assert.That(world.GetPlayer(guestId).IsDefeated, Is.True);

            rig.Input[guest] = new PlayerCommand(0f, 0f, respawn: true); // asking does nothing in co-op
            rig.Tick(Latency * 2 + 4);
            rig.Input[guest] = PlayerCommand.None;
            Assert.That(world.GetPlayer(guestId).IsDefeated, Is.True);

            world.PlacePlayer(rig.Host.LocalPlayerId, CorridorCheckpoint);
            var seen = TickAndCollect(rig, Latency * 2 + 4);

            Assert.That(world.GetPlayer(guestId).IsDefeated, Is.False);
            Assert.That(guest.World.GetPlayer(guestId).IsDefeated, Is.False, "the guest's world shows them back up");
            Assert.That(seen[guest].OfType<PlayerRevived>().Count(r => r.Player == guestId), Is.EqualTo(1), "and they are told, once");
            Assert.That(seen[guest].OfType<CheckpointActivated>().Count(), Is.EqualTo(1));
        }
        // --- Ticket 19: the hub over the network ------------------------------------------------

        private static SimulationConfig HubConfig()
        {
            var config = GateConfig();
            config.Hub = new HubDefinition { Width = 20f, Length = 16f, Centre = new Vec3(0f, 0f, -40f) };
            return config;
        }

        [Test]
        public void AGuestReadiesUpTheHostStartsAndLeavesAndBothWorldsFollow()
        {
            HostSession host = null;
            var network = new SimulatedNetwork(Latency);
            host = new HostSession(HubConfig(), network.HostEndpoint, id => JoinPoint.For(host.World, id, Vec3.Zero));
            var hostId = host.AddLocalPlayer();
            var guest = new ClientSession(HubConfig(), network.Connect(), "alice");
            void Tick(int times, PlayerCommand hostCommand = default, PlayerCommand guestCommand = default)
            {
                for (var i = 0; i < times; i++)
                {
                    network.Advance();
                    host.Tick(i == 0 ? hostCommand : PlayerCommand.None);
                    guest.Tick(i == 0 ? guestCommand : PlayerCommand.None);
                }
            }
            Tick(Latency * 2 + 2);
            Assert.That(guest.World.InHub, Is.True, "the guest arrives in the hub");
            Assert.That((host.World.GetPlayer(guest.PlayerId.Value).Position - host.World.Hub.Centre).Length, Is.LessThan(4f));

            Tick(1, new PlayerCommand(0f, 0f, setReady: true));
            Assert.That(host.World.TryStartGateFromHub("corridor", out var why), Is.False, "the guest is not ready yet");
            Tick(Latency * 2 + 2, guestCommand: new PlayerCommand(0f, 0f, setReady: true));
            Assert.That(host.World.GetPlayer(guest.PlayerId.Value).IsReady, Is.True, "the guest's ready mark reached the host");
            Assert.That(host.World.TryStartGateFromHub("corridor", out why), Is.True, why);
            Tick(Latency * 2 + 2);
            Assert.That(guest.World.HasGate, Is.True, "and the guest is taken in too");

            host.World.LeaveGate();
            var seen = new List<SimulationEvent>();
            for (var i = 0; i < Latency * 2 + 4; i++)
            {
                network.Advance();
                host.Tick(PlayerCommand.None);
                guest.Tick(PlayerCommand.None);
                seen.AddRange(guest.Events);
            }
            Assert.That(guest.World.InHub, Is.True);
            Assert.That(seen.OfType<ReturnedToHub>().Count(), Is.EqualTo(1), "and is told, once");
            Assert.That(host.World.GetPlayer(hostId).IsReady, Is.False);
        }
    }
}
