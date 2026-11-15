using System.Collections.Generic;
using System.Linq;
using HunterSystem.Game;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;

namespace HunterSystem.Tests
{
    /// <summary>
    /// The character creator's rules (ticket 28): a hunter has a name, chosen once, and looks (a body
    /// colour and a build) that can change in the hub. Both are saved with the character, travel in
    /// snapshots so other players see them, and change nothing in play.
    /// </summary>
    public class CharacterCreatorTest
    {
        private static readonly PlayerId P1 = new PlayerId(1);
        private static readonly PlayerId P2 = new PlayerId(2);

        private static readonly CharacterLook RedBroad = new CharacterLook(1, Build.Broad);

        private static SimulationConfig Config(bool hub = true) => new SimulationConfig
        {
            TickRate = 60,
            PlayerMoveSpeed = 5f,
            Hub = hub ? new HubDefinition { Width = 20f, Length = 16f, Centre = Vec3.Zero, BoardOffset = new Vec3(0f, 0f, 6f), ReturnCountdownSeconds = 2f } : null,
        };

        private static List<SimulationEvent> Step(SimulationWorld world, PlayerId who, PlayerCommand command)
        {
            world.Step(new Dictionary<PlayerId, PlayerCommand> { [who] = command });
            return world.Events.ToList();
        }

        // --- Names ---------------------------------------------------------------------------

        [TestCase("Ayla", "Ayla")]
        [TestCase("  Ayla   the   Bold ", "Ayla the Bold")]
        [TestCase("O'Neil-Ray_2", "O'Neil-Ray_2")]
        [TestCase("<b>Ayla</b>!", "bAylab")]
        [TestCase("Abcdefghijklmnopqrstuvwxyz", "Abcdefghijklmnop")]
        public void NamesKeepLettersAndASpaceAtATimeAndAreCutToSixteen(string typed, string kept)
        {
            Assert.That(HunterNames.Clean(typed), Is.EqualTo(kept));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("!!!")]
        public void ANameWithNothingUsableIsNoName(string typed)
        {
            Assert.That(HunterNames.Clean(typed), Is.Null);
        }

        // --- Looks ---------------------------------------------------------------------------

        [Test]
        public void LooksOutsideTheChoicesBecomeValidOnes()
        {
            Assert.That(new CharacterLook(99, Build.Slim).Colour, Is.EqualTo(CharacterLook.ColourCount - 1));
            Assert.That(new CharacterLook(-3, Build.Slim).Colour, Is.EqualTo(0));
            Assert.That(new CharacterLook(2, (Build)77).Build, Is.EqualTo(Build.Average));
            Assert.That(default(CharacterLook), Is.EqualTo(new CharacterLook(0, Build.Average)), "the default look is the first colour, average build");
        }

        // --- Saves ---------------------------------------------------------------------------

        [Test]
        public void TheNameAndLooksAreSavedWithTheCharacter()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, Vec3.Zero, new CharacterRecord { IsNew = true, Name = "Ayla", Look = RedBroad });

            var back = CharacterCodec.Decode(CharacterCodec.Encode(world.CaptureCharacter(P1)));
            var next = new SimulationWorld(Config());
            next.AddPlayer(P1, Vec3.Zero, back);

            Assert.That(back.Name, Is.EqualTo("Ayla"));
            Assert.That(back.Look, Is.EqualTo(RedBroad));
            Assert.That(next.GetPlayer(P1).Name, Is.EqualTo("Ayla"));
            Assert.That(next.GetPlayer(P1).Look, Is.EqualTo(RedBroad));
        }

        [Test]
        public void ASaveFromBeforeNamesLoadsWithNoNameAndTheDefaultLook()
        {
            var old = new CharacterRecord { Level = 4, ClassId = "warrior", Name = "ignored by format 4", Look = RedBroad };

            var back = CharacterCodec.Decode(CharacterCodec.EncodeVersion4ForTests(old));

            Assert.That(back.Level, Is.EqualTo(4));
            Assert.That(back.ClassId, Is.EqualTo("warrior"));
            Assert.That(back.Name, Is.Null, "the creator will ask for a name");
            Assert.That(back.Look, Is.EqualTo(default(CharacterLook)));
        }

        [Test]
        public void ASavedNameIsCleanedWhenItIsBroughtIn()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, Vec3.Zero, new CharacterRecord { IsNew = true, Name = "  <Ayla>  " });

            Assert.That(world.GetPlayer(P1).Name, Is.EqualTo("Ayla"));
        }

        [Test]
        public void PickingAClassKeepsTheNameAndLooks()
        {
            var warrior = new ClassDefinition { Id = "warrior", Name = "Warrior" };
            var made = new CharacterRecord { IsNew = true, Name = "Ayla", Look = RedBroad };

            var classed = made.WithClass(warrior, new ProgressionDefinition());

            Assert.That(classed.Name, Is.EqualTo("Ayla"));
            Assert.That(classed.Look, Is.EqualTo(RedBroad));
        }

        // --- Seen by everyone ----------------------------------------------------------------

        [Test]
        public void SnapshotsCarryEveryonesNameAndLooks()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, Vec3.Zero, new CharacterRecord { IsNew = true, Name = "Ayla", Look = RedBroad });
            world.AddPlayer(P2, new Vec3(2f, 0f, 0f), new CharacterRecord { IsNew = true, Name = "Brann", Look = new CharacterLook(4, Build.Slim) });

            var guest = SimulationWorld.FromSnapshot(Config(), world.CaptureSnapshot());

            Assert.That(guest.GetPlayer(P1).Name, Is.EqualTo("Ayla"));
            Assert.That(guest.GetPlayer(P1).Look, Is.EqualTo(RedBroad));
            Assert.That(guest.GetPlayer(P2).Name, Is.EqualTo("Brann"));
            Assert.That(guest.GetPlayer(P2).Look, Is.EqualTo(new CharacterLook(4, Build.Slim)));
        }

        [Test]
        public void NameplatesShowEveryoneElseWithANameButNotYou()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, Vec3.Zero, new CharacterRecord { IsNew = true, Name = "Ayla" });
            world.AddPlayer(P2, Vec3.Zero, new CharacterRecord { IsNew = true, Name = "Brann" });
            world.AddPlayer(new PlayerId(3), Vec3.Zero); // a bare test player, with no name

            Assert.That(Nameplates.Whose(world, P1).Select(n => n.name), Is.EqualTo(new[] { "Brann" }));
            Assert.That(Nameplates.Whose(world, null).Select(n => n.name), Is.EqualTo(new[] { "Ayla", "Brann" }), "a machine with no hunter of its own sees all");
        }

        // --- Changing looks in the hub -------------------------------------------------------

        [Test]
        public void LooksChangeInTheHubByACommand()
        {
            var world = new SimulationWorld(Config());
            world.AddPlayer(P1, Vec3.Zero, new CharacterRecord { IsNew = true, Name = "Ayla" });

            var events = Step(world, P1, new PlayerCommand(0f, 0f, changeLook: RedBroad));

            Assert.That(world.GetPlayer(P1).Look, Is.EqualTo(RedBroad));
            Assert.That(events.OfType<LookChanged>().Single().Look, Is.EqualTo(RedBroad));
            Assert.That(world.GetPlayer(P1).Name, Is.EqualTo("Ayla"), "the name stays");
            Assert.That(Step(world, P1, new PlayerCommand(0f, 0f, changeLook: RedBroad)).OfType<LookChanged>(), Is.Empty, "the same look again changes nothing");
        }

        [Test]
        public void LooksCannotChangeOutsideTheHub()
        {
            var world = new SimulationWorld(Config(hub: false));
            world.AddPlayer(P1, Vec3.Zero, new CharacterRecord { IsNew = true, Name = "Ayla" });

            var events = Step(world, P1, new PlayerCommand(0f, 0f, changeLook: RedBroad));

            Assert.That(world.GetPlayer(P1).Look, Is.EqualTo(default(CharacterLook)));
            Assert.That(events.OfType<LookChanged>(), Is.Empty);
        }

        [Test]
        public void ALookChangeSurvivesTheWireAndTheEventCodec()
        {
            var (_, command) = Wire.ReadCommand(Wire.Command(7, new PlayerCommand(0f, 0f, changeLook: RedBroad)));
            Assert.That(command.ChangeLook, Is.EqualTo(RedBroad));
            var (_, none) = Wire.ReadCommand(Wire.Command(8, new PlayerCommand(1f, 0f)));
            Assert.That(none.ChangeLook, Is.Null);

            var decoded = SimulationEventCodec.Decode(SimulationEventCodec.Encode(new SimulationEvent[] { new LookChanged(3, P1, RedBroad) }), Config());
            var changed = (LookChanged)decoded.Single();
            Assert.That(changed.Player, Is.EqualTo(P1));
            Assert.That(changed.Look, Is.EqualTo(RedBroad));
        }

        [Test]
        public void ALookChangeIsSavedSoon()
        {
            var autosave = new CharacterAutosave(new CharacterSaves(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hunter-test-saves", System.Guid.NewGuid().ToString("N"))));
            autosave.Observe(new SimulationEvent[] { new LookChanged(1, P1, RedBroad) }, P1);

            Assert.That(autosave.SaveIfDue(() => new CharacterRecord { Name = "Ayla", Look = RedBroad }, now: 10f), Is.True);
        }

        [Test]
        public void TheCommandBufferSendsTheLatestLookOnce()
        {
            var buffer = new PlayerCommandBuffer();
            buffer.QueueLook(new CharacterLook(2, Build.Slim));
            buffer.QueueLook(RedBroad);

            Assert.That(buffer.TakeForTick().ChangeLook, Is.EqualTo(RedBroad));
            Assert.That(buffer.TakeForTick().ChangeLook, Is.Null);
        }

        // --- Bot teammates -------------------------------------------------------------------

        [Test]
        public void BotTeammatesAreNamedAndEachWearsADifferentColour()
        {
            var config = new SimulationConfig
            {
                Classes = new[]
                {
                    new ClassDefinition { Id = "warrior", Name = "Warrior" }, new ClassDefinition { Id = "rogue", Name = "Rogue" },
                    new ClassDefinition { Id = "archer", Name = "Archer" }, new ClassDefinition { Id = "wizard", Name = "Wizard" },
                },
            };

            var party = CompanionParty.NewParty("wizard", config);

            Assert.That(party.Select(c => c.Character.Name), Is.EqualTo(party.Select(c => c.Name)));
            Assert.That(party.Select(c => c.Character.Look.Colour).Distinct().Count(), Is.EqualTo(party.Count));
            Assert.That(party.Where(c => c.ClassId == "warrior").Select(c => c.Character.Look.Build), Is.All.EqualTo(Build.Broad), "the tank is broad");
        }
    }
}
