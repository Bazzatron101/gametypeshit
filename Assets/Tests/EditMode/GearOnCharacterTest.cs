using System.Linq;
using HunterSystem.Game;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Gear shows on the character (ticket 34). Every piece in the game has a look to draw, and what
    /// each character wears reaches everyone: the host, its guests, and bot teammates, which put on
    /// what suits their role. Drawing it in the scenes is covered in PlayMode (GearOnCharacterSceneTest).
    /// </summary>
    public class GearOnCharacterTest
    {
        private const int Latency = 6;

        // --- Looks -----------------------------------------------------------------------------

        private static GameContentAsset Content() =>
            AssetDatabase.FindAssets("t:GameContentAsset")
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameContentAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .Single();

        [Test]
        public void EveryPieceInTheGameHasALookThatCanBeDrawnButNotBumpedInto()
        {
            foreach (var gear in Content().Gear)
            {
                Assert.That(gear.Look, Is.Not.Null, $"'{gear.Id}' has a look");
                Assert.That(gear.Look.GetComponentsInChildren<Renderer>(true), Is.Not.Empty, $"'{gear.Id}' draws something");
                Assert.That(gear.Look.GetComponentsInChildren<Collider>(true), Is.Empty, $"'{gear.Id}' is drawn, never touched");
            }
        }

        [Test]
        public void PiecesOfOneKindLookDifferent()
        {
            var weapons = Content().Gear.Where(g => g.Slot == GearSlot.Weapon).Select(g => g.Look).ToArray();
            Assert.That(weapons.Length, Is.GreaterThan(1));
            Assert.That(weapons, Is.Unique, "each weapon its own look, so teammates with different weapons look different");
        }

        [Test]
        public void TheCatalogFindsEachLookByGearId()
        {
            var content = Content();
            var catalog = GearLooks.Catalog(content);

            foreach (var gear in content.Gear) Assert.That(catalog[gear.Id], Is.SameAs(gear.Look));
            Assert.That(GearLooks.Catalog(null), Is.Empty, "a bare scene draws stand-ins");
        }

        [Test]
        public void EachSlotHasItsOwnPlaceOnTheBody()
        {
            var anchors = System.Enum.GetValues(typeof(GearSlot)).Cast<GearSlot>().Select(GearLooks.Anchor).ToArray();
            Assert.That(anchors.Length, Is.EqualTo(GearDefinition.SlotCount));
            Assert.That(anchors, Is.Unique);
            Assert.That(GearLooks.Anchor(GearSlot.Head).y, Is.GreaterThan(GearLooks.Anchor(GearSlot.Body).y));
            Assert.That(GearLooks.Anchor(GearSlot.Feet).y, Is.LessThan(GearLooks.Anchor(GearSlot.Hands).y));
        }

        // --- Everyone sees everyone's gear --------------------------------------------------------

        private static GearDefinition Piece(string id, GearSlot slot, params string[] tags) =>
            new GearDefinition { Id = id, Name = id, Slot = slot, Tags = tags };

        private static readonly GearDefinition Staff = Piece("staff", GearSlot.Weapon, "fire", "magic");
        private static readonly GearDefinition Cleaver = Piece("cleaver", GearSlot.Weapon, "melee", "physical");
        private static readonly GearDefinition Hood = Piece("hood", GearSlot.Head, "fire");
        private static readonly GearDefinition Plate = Piece("plate", GearSlot.Body, "melee", "physical");

        private static SimulationConfig Config() => new SimulationConfig
        {
            TickRate = 60,
            Gear = new[] { Staff, Cleaver, Hood, Plate },
            Classes = new[] { new ClassDefinition { Id = "wizard", Name = "Wizard" }, new ClassDefinition { Id = "warrior", Name = "Warrior" } },
            Progression = new ProgressionDefinition
            {
                LevelCap = 10, XpToNextLevel = new[] { 50, 100, 150, 200, 250, 300, 350, 400, 450 }, FreePointsPerLevel = 3, StartingStatValue = 10,
            },
        };

        private static CharacterRecord Carrying(string classId, params string[] gear)
        {
            var character = new CharacterRecord { ClassId = classId }; // not a new one: a new character comes with nothing
            character.Gear.AddRange(gear);
            return character;
        }

        private static string[] Worn(SimulationWorld world, PlayerId id) => world.GetPlayer(id).Worn.Select(g => g?.Id).ToArray();

        [Test]
        public void HostGuestAndBotTeammatesEachSeeEveryonesOwnGear()
        {
            var network = new SimulatedNetwork(Latency);
            var host = new HostSession(Config(), network.HostEndpoint, _ => Vec3.Zero);
            var me = host.AddLocalPlayer(Carrying("warrior", "cleaver", "plate"));
            var wizard = host.AddCompanion(new CompanionRecord { Name = "Ash", Character = Carrying("wizard", "cleaver", "staff", "hood") }, seed: 7);
            var guest = new ClientSession(Config(), network.Connect(), "guest", Carrying("warrior", "staff", "hood"));
            var guestCommand = PlayerCommand.None;

            void Tick(int times)
            {
                for (var i = 0; i < times; i++)
                {
                    network.Advance();
                    host.Tick(PlayerCommand.None);
                    guest.Tick(guestCommand);
                    guestCommand = PlayerCommand.None;
                }
            }

            Tick(Latency * 2 + 2);
            Assert.That(guest.IsJoined, Is.True);
            var them = guest.PlayerId.Value;

            host.World.GiveGear(me, "cleaver", equip: true); // the host puts on a cleaver and plate
            host.World.GiveGear(me, "plate", equip: true);
            guestCommand = new PlayerCommand(0f, 0f, equipGearId: "staff"); // the guest wears its staff, through a command
            Tick(1);
            guestCommand = new PlayerCommand(0f, 0f, equipGearId: "hood");
            Tick(Latency * 3 + 10);

            var hostWears = new[] { "cleaver", null, "plate", null, null, null };
            var guestWears = new[] { "staff", "hood", null, null, null, null };
            var wizardWears = new[] { "staff", "hood", null, null, null, null }; // a caster wants fire and magic: the staff, not the cleaver it also carries

            Assert.That(Worn(host.World, me), Is.EqualTo(hostWears));
            Assert.That(Worn(host.World, them), Is.EqualTo(guestWears), "the host sees the guest's own gear");
            Assert.That(Worn(host.World, wizard), Is.EqualTo(wizardWears), "the bot teammate dressed for its role");
            Assert.That(Worn(guest.World, me), Is.EqualTo(hostWears), "the guest sees the host's gear, from snapshots");
            Assert.That(Worn(guest.World, them), Is.EqualTo(guestWears), "and its own");
            Assert.That(Worn(guest.World, wizard), Is.EqualTo(wizardWears), "and the bot teammate's");
        }
    }
}
