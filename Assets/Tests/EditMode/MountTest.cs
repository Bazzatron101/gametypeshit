using System.Collections.Generic;
using System.Linq;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Mounts (ticket 42). A character with a mount rides it in the overworld at 16 m/s, whatever their
    /// agility. A hit, or starting an attack or a dodge, puts the rider on foot, and so do
    /// a gate and going down; jumping does not. A mount does not climb. It belongs to the character and is
    /// saved with it; a new character has none.
    /// </summary>
    public class MountTest
    {
        private static readonly PlayerId P = new PlayerId(1);
        private static readonly Vec3 OutOfTown = new Vec3(100f, 0f, 0f); // the town is 40 m round the origin

        private static GateDefinition Gate() => new GateDefinition { Id = "den", Name = "Den", Rooms = { new RoomDefinition() } };

        private static SimulationConfig Config(bool overworld = true, IGround ground = null) => new SimulationConfig
        {
            TickRate = 60, PlayerMoveSpeed = 5f, SprintSpeedPercent = 160, MountSpeed = 16f,
            Hub = new HubDefinition { Centre = Vec3.Zero },
            Overworld = overworld ? new OverworldDefinition { TownRadius = 40f } : null,
            Ground = ground,
            Gates = new[] { Gate() },
        };

        private static SimulationWorld World(Vec3? at = null, bool mount = true, bool overworld = true, IGround ground = null)
        {
            var world = new SimulationWorld(Config(overworld, ground));
            world.AddPlayer(P, at ?? OutOfTown);
            if (mount) world.GrantMount(P);
            Step(world, PlayerCommand.None);
            return world;
        }

        private static List<SimulationEvent> Step(SimulationWorld world, PlayerCommand command, int ticks = 1)
        {
            var all = new List<SimulationEvent>();
            for (var i = 0; i < ticks; i++)
            {
                world.Step(new Dictionary<PlayerId, PlayerCommand> { [P] = command });
                all.AddRange(world.Events);
            }
            return all;
        }

        private static readonly PlayerCommand Toggle = new PlayerCommand(0f, 0f, toggleMount: true);

        private static PlayerState Me(SimulationWorld world) => world.GetPlayer(P);

        // --- Getting on -----------------------------------------------------------------------------------

        [Test]
        public void ANewCharacterHasNoMountAndCannotRide()
        {
            var world = World(mount: false);

            Step(world, Toggle);

            Assert.That(Me(world).HasMount, Is.False);
            Assert.That(Me(world).IsMounted, Is.False);
        }

        [Test]
        public void AGrantedMountCanBeRiddenAndLeft()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P, OutOfTown);
            world.GrantMount(P);
            var events = Step(world, PlayerCommand.None);
            Assert.That(events.OfType<MountGranted>().Single().Player, Is.EqualTo(P));
            Assert.That(Me(world).HasMount, Is.True);

            events = Step(world, Toggle);
            Assert.That(Me(world).IsMounted, Is.True);
            Assert.That(events.OfType<MountChanged>().Single().Mounted, Is.True);

            events = Step(world, Toggle);
            Assert.That(Me(world).IsMounted, Is.False);
            Assert.That(events.OfType<MountChanged>().Single().Mounted, Is.False);
        }

        [Test]
        public void ARiderGoesSixteenMetresASecondWhateverSprintSays()
        {
            var walk = World();
            var ride = World();
            Step(ride, Toggle);

            Step(walk, new PlayerCommand(1f, 0f), 60);
            Step(ride, new PlayerCommand(1f, 0f, sprint: true), 60);

            Assert.That(Me(walk).Position.X - OutOfTown.X, Is.EqualTo(5f).Within(0.01f));
            Assert.That(Me(ride).Position.X - OutOfTown.X, Is.EqualTo(16f).Within(0.01f), "2× sprint, sprint held or not");
            Assert.That(Me(ride).IsSprinting, Is.False);
        }

        // --- Where riding ends ------------------------------------------------------------------------------

        [Test]
        public void RidingInTheTownIsAllowedForNow()
        {
            var inTown = World(at: new Vec3(10f, 0f, 0f));
            Step(inTown, Toggle);
            Assert.That(Me(inTown).IsMounted, Is.True, "the developer lifted the town rule (2026-11-17)");

            Step(inTown, new PlayerCommand(-1f, 0f), 60); // rides across the town's middle
            Assert.That(Me(inTown).IsMounted, Is.True, "riding through the town keeps the rider up");
            Assert.That(Me(inTown).Position.X, Is.EqualTo(10f - 16f).Within(0.5f), "16 m in a second");
        }

        [Test]
        public void TheGameCanSayWhyRDoesNothing()
        {
            Assert.That(World(mount: false).WhyCannotMount(P), Is.EqualTo(MountBlock.NoMount));
            Assert.That(World(overworld: false).WhyCannotMount(P), Is.EqualTo(MountBlock.NotInOverworld));
            Assert.That(World(at: new Vec3(10f, 0f, 0f)).WhyCannotMount(P), Is.EqualTo(MountBlock.None), "in the town too");

            var outside = World();
            Assert.That(outside.WhyCannotMount(P), Is.EqualTo(MountBlock.None));
            Step(outside, new PlayerCommand(0f, 0f, jump: true));
            Assert.That(outside.WhyCannotMount(P), Is.EqualTo(MountBlock.Busy), "mid-jump");
        }

        [Test]
        public void EnteringAGatePutsARiderOnFoot()
        {
            var world = World();
            Step(world, Toggle);

            world.StartGate("den");

            Assert.That(Me(world).IsMounted, Is.False);
            Step(world, Toggle);
            Assert.That(Me(world).IsMounted, Is.False, "and nobody rides in a gate");
        }

        [Test]
        public void WithoutAnOverworldThereIsNoRiding()
        {
            var world = World(overworld: false);

            Step(world, Toggle);

            Assert.That(Me(world).HasMount, Is.True);
            Assert.That(Me(world).IsMounted, Is.False);
        }

        // --- No fighting from the saddle ---------------------------------------------------------------------

        [Test]
        public void AttackingOrDodgingPutsARiderOnFoot()
        {
            var attack = World();
            Step(attack, Toggle);
            Step(attack, new PlayerCommand(0f, 0f, lightAttack: true));
            Assert.That(Me(attack).IsMounted, Is.False, "an attack");

            var dodge = World();
            Step(dodge, Toggle);
            Step(dodge, new PlayerCommand(0f, 0f, dodge: true));
            Assert.That(Me(dodge).IsMounted, Is.False, "a dodge");
            Assert.That(Me(dodge).IsDodging, Is.True, "and the dodge still happens");
        }

        [Test]
        public void JumpingDoesNotPutARiderOnFoot()
        {
            var world = World();
            Step(world, Toggle);

            Step(world, new PlayerCommand(1f, 0f, jump: true));
            Step(world, new PlayerCommand(1f, 0f), 50);

            Assert.That(Me(world).IsMounted, Is.True);
        }

        [Test]
        public void AHitKnocksARiderOff()
        {
            var world = World();
            Step(world, Toggle);
            world.SpawnEnemy(new EnemyDefinition
            {
                Name = "Wolf", MaxHealth = 10, MoveSpeed = 0f, AttackDamage = 5, AttackRange = 3f, AttackWindupSeconds = 0.1f, AttackCooldownSeconds = 1f,
            }, OutOfTown + new Vec3(1f, 0f, 0f));

            var events = Step(world, PlayerCommand.None, 30);

            Assert.That(events.OfType<PlayerDamaged>().Any(), Is.True, "test setup: the wolf bit");
            Assert.That(Me(world).IsMounted, Is.False);
        }

        // --- Ground --------------------------------------------------------------------------------------

        [Test]
        public void AMountDoesNotClimb()
        {
            // 20 × 1 cells of 10 m from (0, -5); the cells from x 150 to 160 are a climbable face.
            var kinds = Enumerable.Range(0, 20).Select(i => i == 15 ? GroundKind.Climbable : GroundKind.Walkable).ToArray();
            var ground = new HeightmapGround(0f, -5f, 10f, 20, 1, new float[42], kinds);

            var rider = World(ground: ground);
            Step(rider, Toggle);
            Step(rider, new PlayerCommand(1f, 0f), 60 * 4);
            Assert.That(Me(rider).Position.X, Is.LessThan(150f), "the rider stops at the face");

            var walker = World(ground: ground);
            Step(walker, new PlayerCommand(1f, 0f), 60 * 14);
            Assert.That(Me(walker).Position.X, Is.GreaterThan(160f), "on foot it can be climbed");
        }

        // --- Saved with the character ---------------------------------------------------------------------

        [Test]
        public void TheMountIsSavedWithTheCharacter()
        {
            var world = World();
            var saved = CharacterCodec.Decode(CharacterCodec.Encode(world.CaptureCharacter(P)));
            Assert.That(saved.HasMount, Is.True);

            var elsewhere = new SimulationWorld(Config());
            elsewhere.AddPlayer(P, OutOfTown, saved);
            Assert.That(elsewhere.GetPlayer(P).HasMount, Is.True, "a guest brings their own");
            Assert.That(elsewhere.GetPlayer(P).IsMounted, Is.False, "and arrives on foot");
        }

        [Test]
        public void ASaveFromBeforeMountsHasNone()
        {
            var old = CharacterCodec.Decode(CharacterCodec.EncodeVersion5ForTests(new CharacterRecord { Level = 3, HasMount = true }));

            Assert.That(old.Level, Is.EqualTo(3));
            Assert.That(old.HasMount, Is.False);
        }

        [Test]
        public void ASnapshotKeepsTheRider()
        {
            var world = World();
            Step(world, Toggle);

            var copy = SimulationWorld.FromSnapshot(Config(), world.CaptureSnapshot());

            Assert.That(copy.GetPlayer(P).HasMount, Is.True);
            Assert.That(copy.GetPlayer(P).IsMounted, Is.True);
        }

        // --- Over the network ----------------------------------------------------------------------------

        [Test]
        public void AGuestsRideIsPredictedAndAgreesWithTheHost()
        {
            const int latency = 4;
            var network = new SimulatedNetwork(latency);
            var host = new HostSession(Config(), network.HostEndpoint, _ => OutOfTown, 3);
            host.AddLocalPlayer();
            var guest = new ClientSession(Config(), network.Connect(), "rider");

            void Tick(PlayerCommand command, int times = 1)
            {
                for (var i = 0; i < times; i++)
                {
                    network.Advance();
                    host.Tick(PlayerCommand.None);
                    guest.Tick(command);
                }
            }

            Tick(PlayerCommand.None, latency * 2 + 2);
            var me = guest.PlayerId.Value;
            host.World.GrantMount(me);
            Tick(PlayerCommand.None, latency * 3);
            Tick(new PlayerCommand(0f, 0f, toggleMount: true));
            Assert.That(guest.World.GetPlayer(me).IsMounted, Is.True, "predicted at once");
            Tick(new PlayerCommand(1f, 0f), 60);
            Tick(PlayerCommand.None, latency * 3);

            var onHost = host.World.GetPlayer(me);
            Assert.That(onHost.IsMounted, Is.True);
            Assert.That(onHost.Position.X - OutOfTown.X, Is.EqualTo(16f).Within(0.05f), "a second at 16 m/s on the host");
            Assert.That(guest.World.GetPlayer(me).Position.X, Is.EqualTo(onHost.Position.X).Within(1e-3f));
            Assert.That(guest.LargestCorrection, Is.LessThan(1e-3f), "never pulled back");
        }

        // --- On the real test patch (the playtest: "I'm not able to get onto the mount") ----------------

        [Test]
        public void ARiderGetsOnAndRidesOnTheRealTestPatchFromTheTown()
        {
            var patch = UnityEditor.AssetDatabase.LoadAssetAtPath<HunterSystem.Game.OverworldAsset>("Assets/Data/Overworld/TestPatch.asset");
            var content = UnityEditor.AssetDatabase.LoadAssetAtPath<HunterSystem.Game.GameContentAsset>("Assets/Data/GameContent.asset");
            Assume.That(patch != null && patch.IsBaked, "the test patch is baked");
            var ground = patch.LoadGround();
            var town = new Vec3(patch.TownCentre.x, 0f, patch.TownCentre.z);

            foreach (var (x, z) in new[] { (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
            {
                SimulationWorld Fresh(bool mounted)
                {
                    var config = new SimulationConfig();
                    content.ApplyTo(config);
                    patch.ApplyTo(config, ground);
                    var world = new SimulationWorld(config);
                    world.AddPlayer(P, town);
                    world.GrantMount(P);
                    Step(world, PlayerCommand.None);
                    if (mounted) Step(world, Toggle);
                    return world;
                }

                var walker = Fresh(mounted: false);
                var rider = Fresh(mounted: true);
                Assert.That(Me(rider).IsMounted, Is.True, $"gets on in the town ({x}, {z})");

                var go = new PlayerCommand(x, z);
                Step(walker, go, 180);
                Step(rider, go, 180);

                Assert.That(Me(rider).IsMounted, Is.True, $"still riding after 3 s ({x}, {z})");
                var walked = Flat(Me(walker).Position - town);
                var rode = Flat(Me(rider).Position - town);
                Assert.That(rode, Is.GreaterThanOrEqualTo(walked - 0.5f), $"a rider gets at least as far as a walker ({x}, {z}): rode {rode:0.0} m, walked {walked:0.0} m");
            }
        }

        private static float Flat(Vec3 v) => (float)System.Math.Sqrt(v.X * v.X + v.Z * v.Z);
    }
}