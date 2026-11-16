using System.Linq;
using HunterSystem.Game;
using HunterSystem.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Content validation. The game's single content list (GameContent) loads into a simulation
    /// through the same path the runner uses, every entry passes the simulation's checks, and no
    /// skill, modifier or enemy asset exists outside the list. A bad asset fails here, before
    /// anyone meets it in a fight.
    /// </summary>
    public class ContentValidationTest
    {
        private static T[] All<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();

        private static GameContentAsset Content()
        {
            var all = All<GameContentAsset>();
            Assert.That(all.Length, Is.EqualTo(1), "exactly one GameContent asset");
            return all[0];
        }

        private static SimulationWorld WorldFromContent(GameContentAsset content)
        {
            var config = new SimulationConfig();
            content.ApplyTo(config);
            return new SimulationWorld(config);
        }

        [Test]
        public void ContentHasNoEmptyEntriesAndOneOfEachSettingsAsset()
        {
            var content = Content();

            Assert.That(All<ProgressionAsset>(), Is.EqualTo(new[] { content.Progression }), "one progression asset, the listed one");
            Assert.That(All<PlayerTuningAsset>(), Is.EqualTo(new[] { content.PlayerTuning }), "one tuning asset, the listed one");
            Assert.That(content.Skills, Is.Not.Empty.And.All.Not.Null);
            Assert.That(content.Modifiers, Is.All.Not.Null);
            Assert.That(content.Enemies, Is.Not.Empty.And.All.Not.Null);
        }

        [Test]
        public void EveryAssetIsInTheContentListSoNothingIsDefinedTwiceOrForgotten()
        {
            var content = Content();

            Assert.That(All<ClassDefinitionAsset>(), Is.SubsetOf(content.Classes));
            Assert.That(All<ShopAsset>(), Is.SubsetOf(content.Shops));
            Assert.That(All<SkillDefinitionAsset>(), Is.SubsetOf(content.Skills));
            Assert.That(All<ModifierDefinitionAsset>(), Is.SubsetOf(content.Modifiers));
            Assert.That(All<GearDefinitionAsset>(), Is.SubsetOf(content.Gear));
            Assert.That(content.Gear, Is.Unique.And.All.Not.Null);
            Assert.That(All<EnemyDefinitionAsset>(), Is.SubsetOf(content.Enemies));
            Assert.That(All<GateDefinitionAsset>(), Is.SubsetOf(content.Gates));
            Assert.That(All<BossDefinitionAsset>(), Is.SubsetOf(content.Bosses));
            Assert.That(content.Bosses, Is.Unique);
            Assert.That(content.Gates, Is.Unique);
            Assert.That(content.Skills, Is.Unique);
            Assert.That(content.Modifiers, Is.Unique);
            Assert.That(content.Enemies, Is.Unique);
        }

        [Test]
        public void EveryGateIsACompleteChainOfRoomPieces()
        {
            var content = Content();
            Assert.That(content.Gates, Is.Not.Empty.And.All.Not.Null);
            Assert.That(content.Gates.Select(g => g.Id), Is.Unique, "gate ids name gates in saves and over the network");
            var config = new SimulationConfig();
            content.ApplyTo(config);

            foreach (var asset in content.Gates)
            {
                Assert.That(asset.Id, Is.Not.Empty, asset.name);
                Assert.That(asset.Rooms, Is.Not.Empty.And.All.Not.Null, $"{asset.name}: rooms");
                Assert.That(asset.Rooms[0].Spawns, Is.Empty, $"{asset.name}: the entrance room is safe to arrive in");
                foreach (var room in asset.Rooms)
                {
                    foreach (var spawn in room.Spawns)
                    {
                        Assert.That(spawn.enemy, Is.Not.Null, $"{room.name}: a spawn with no enemy");
                        Assert.That(content.Enemies, Has.Member(spawn.enemy), $"{room.name}: an enemy that is not in the content list");
                        Assert.That(Mathf.Abs(spawn.offset.x), Is.LessThanOrEqualTo(room.Width / 2f), $"{room.name}: a spawn outside the room");
                        Assert.That(Mathf.Abs(spawn.offset.z), Is.LessThanOrEqualTo(room.Length / 2f), $"{room.name}: a spawn outside the room");
                    }
                }

                foreach (var modifier in asset.RewardPool)
                    Assert.That(content.Modifiers, Has.Member(modifier), $"{asset.name}: a reward that is not in the content list");
                foreach (var gear in asset.GearPool)
                    Assert.That(content.Gear, Has.Member(gear), $"{asset.name}: gear that is not in the content list");
                foreach (var room in asset.Rooms.Where(r => r.Boss != null))
                {
                    Assert.That(content.Bosses, Has.Member(room.Boss), $"{room.name}: a boss that is not in the content list");
                    Assert.That(Mathf.Abs(room.BossOffset.x) <= room.Width / 2f && Mathf.Abs(room.BossOffset.z) <= room.Length / 2f, Is.True,
                        $"{room.name}: the boss waits outside the room");
                }

                var gate = config.Gates.Single(g => g.Id == asset.Id);
                var layout = new GateLayout(gate); // throws on a room with no floor
                Assert.That(layout.Checkpoints, Is.Not.Empty, $"{asset.name}: at least one checkpoint");
                Assert.That(gate.SoloRevives, Is.EqualTo(3), $"{asset.name}: the spec's three solo revives");
                Assert.That(gate.Rooms.SelectMany(r => r.Spawns).Select(sp => sp.Enemy).Distinct().Count(),
                    Is.EqualTo(asset.Rooms.SelectMany(r => r.Spawns).Select(sp => sp.enemy).Distinct().Count() +
                               asset.Rooms.Where(r => r.Boss != null).Select(r => r.Boss).Distinct().Count()),
                    $"{asset.name}: each enemy type is one shared definition, so snapshots stay small");
            }
        }

        [Test]
        public void EveryBossHasReadableAttacksAndUsableAdds()
        {
            var content = Content();
            Assert.That(content.Bosses, Is.Not.Empty.And.All.Not.Null);
            foreach (var boss in content.Bosses)
            {
                Assert.That(boss.Attacks, Is.Not.Empty, $"{boss.name}: attacks");
                Assert.That(boss.Attacks.Select(a => a.kind), Is.Unique, $"{boss.name}: at most one attack of each kind");
                Assert.That(boss.Attacks.Select(a => a.windupSeconds), Has.All.GreaterThanOrEqualTo(0.5f),
                    $"{boss.name}: every telegraph long enough to read (half a second)");
                Assert.That(boss.Attacks.Where(a => a.kind == BossAttackKind.Charge).Select(a => a.speed), Has.All.GreaterThan(0f),
                    $"{boss.name}: a charge needs speed");
                foreach (var add in boss.EnrageAdds)
                    Assert.That(content.Enemies, Has.Member(add.enemy), $"{boss.name}: an add that is not in the content list");
            }
        }

        [Test]
        public void TheHubSitsClearOfEveryGate()
        {
            var content = Content();
            Assert.That(content.Hub, Is.Not.Null, "the content has a hub");
            Assert.That(All<HubAsset>().Length, Is.EqualTo(1), "one hub");
            var config = new SimulationConfig();
            content.ApplyTo(config);
            var hub = config.Hub.Floor;
            foreach (var gate in config.Gates)
            foreach (var room in new GateLayout(gate).Rooms)
            {
                var overlaps = room.MinX < hub.MaxX && room.MaxX > hub.MinX && room.MinZ < hub.MaxZ && room.MaxZ > hub.MinZ;
                Assert.That(overlaps, Is.False, $"{gate.Id}: a room overlaps the hub");
            }
        }

        [Test]
        public void EveryStorySceneHasLinesAndAValidTrigger()
        {
            var content = Content();
            Assert.That(content.Scenes, Is.Not.Empty.And.All.Not.Null, "the content has a story scene");
            Assert.That(All<TextSceneAsset>(), Is.SubsetOf(content.Scenes));
            Assert.That(content.Scenes.Select(s => s.Id), Is.Unique, "scene ids name scenes in saves");
            foreach (var scene in content.Scenes)
            {
                Assert.That(scene.Id, Is.Not.Empty, scene.name);
                Assert.That(scene.Lines, Is.Not.Empty, $"{scene.name}: lines");
                Assert.That(scene.Lines.Select(l => l.text), Has.All.Not.Empty, $"{scene.name}: an empty line");
                if (scene.Trigger == StoryTrigger.GateCleared)
                    Assert.That(content.Gates, Has.Member(scene.Gate), $"{scene.name}: plays after a gate that is not in the content");
                if (scene.Trigger == StoryTrigger.RankPromotion)
                    Assert.That(RankIds(content), Has.Member(scene.Rank).And.Not.EqualTo(RankIds(content).FirstOrDefault()),
                        $"{scene.name}: plays on a promotion to a rank the progression does not have");
            }
        }

        private static string[] RankIds(GameContentAsset content)
        {
            var config = new SimulationConfig();
            content.ApplyTo(config);
            return config.Progression.Ranks.Select(r => r.Id).ToArray();
        }

        [Test]
        public void TheRanksRiseEToSAndOpenNewRegions()
        {
            // Ticket 25: each rank's band and the regions it opens are configuration.
            var content = Content();
            var config = new SimulationConfig();
            content.ApplyTo(config);
            var ranks = config.Progression.Ranks;
            Assert.That(ranks.Select(r => r.Id), Is.EqualTo(new[] { "E", "D", "C", "B", "A", "S" }), "the spec's ranks, lowest first");
            Assert.That(ranks[0].FirstLevel, Is.EqualTo(1), "every new hunter is E-rank");
            Assert.That(ranks.Select(r => r.FirstLevel), Is.Ordered.Ascending.And.Unique, "each band starts above the last");
            Assert.That(ranks.Single(r => r.Id == "A").FirstLevel, Is.LessThanOrEqualTo(config.Progression.LevelCap), "A-rank is reachable in the main game");
            Assert.That(ranks.Count(r => r.GrantsMount), Is.EqualTo(1), "one rank brings the first mount");

            var continent = AssetDatabase.LoadAssetAtPath<OverworldAsset>("Assets/Data/Overworld/Continent.asset");
            Assert.That(continent, Is.Not.Null, "the continent");
            var starting = continent.ToDefinition().StartingRegions;
            foreach (var rank in ranks)
            foreach (var region in rank.OpensRegions)
                Assert.That(!string.IsNullOrEmpty(region) && !starting.Contains(region), Is.True, $"{rank.Id}-rank opens '{region}', which is empty or open from the start");

            foreach (var gate in content.Gates)
                Assert.That(string.IsNullOrEmpty(gate.Rank) || ranks.Any(r => r.Id == gate.Rank), Is.True, $"{gate.name}: licensed at a rank the progression does not have");
        }

        [Test]
        public void EveryClassesSpecialtyTreeHasATrialPerTier()
        {
            // Ticket 26: a tree of two tiers, two at D and two at B under each D one, each tier unlocked by the class's
            // trial at that rank. A class whose tree is all stubs may have no trials yet; one with a built specialty must.
            var content = Content();
            var config = new SimulationConfig();
            content.ApplyTo(config);
            var world = new SimulationWorld(config); // also validates the trees, the trials and the specialty skills
            var progression = config.Progression;
            var continent = AssetDatabase.LoadAssetAtPath<OverworldAsset>("Assets/Data/Overworld/Continent.asset").ToDefinition();
            foreach (var c in config.Classes)
            {
                if (!c.Specialties.Any(world.IsSpecialtyBuilt)) continue;
                var d = c.Specialties.Where(s => s.Rank == "D").ToList();
                Assert.That(d.Count, Is.EqualTo(2), $"{c.Name}: two D-rank specialties");
                Assert.That(d.All(s => s.ParentId == null), Is.True, $"{c.Name}: the D tier is the root");
                foreach (var root in d)
                    Assert.That(c.Specialties.Count(s => s.Rank == "B" && s.ParentId == root.Id), Is.EqualTo(2), $"{c.Name}: two B-rank branches under {root.Name}");
                Assert.That(c.Specialties.All(s => s.Rank == "D" || s.Rank == "B"), Is.True, $"{c.Name}: the tiers are D and B");

                var trials = c.TrialGateIds.Select(id => config.Gates.Single(g => g.Id == id)).ToList();
                Assert.That(trials.Select(g => g.Rank), Is.EquivalentTo(new[] { "D", "B" }), $"{c.Name}: one trial per tier");
                foreach (var trial in trials)
                    Assert.That(continent.SiteOf(trial.Id), Is.Null, $"{c.Name}: the guild posts {trial.Name}; it has no site to find");
                foreach (var built in c.Specialties.Where(s => s.ParentId != null && world.IsSpecialtyBuilt(s)))
                    Assert.That(world.IsSpecialtyBuilt(c.Specialties.Single(s => s.Id == built.ParentId)), Is.True, $"{c.Name}: {built.Name} is built, so the way to it must be too");
            }
            foreach (var id in new[] { "warrior", "rogue", "archer", "wizard", "monk", "cleric", "warlock", "summoner" })
                Assert.That(config.Classes.Single(c => c.Id == id).Specialties.Where(world.IsSpecialtyBuilt).Count(), Is.EqualTo(6), $"{id}: ticket 54 fills the whole tree");
            foreach (var skill in config.Skills.Where(s => s.SpecialtyId != null))
            {
                var tier = config.SpecialtyById(skill.SpecialtyId).Rank;
                Assert.That(progression.RankIndexAt(skill.UnlockLevel), Is.GreaterThanOrEqualTo(progression.RankIndexOf(tier)), $"{skill.Name}: a {tier}-rank specialty's skill comes at {tier}-rank or later");
            }
        }

        [Test]
        public void ListTheWritingStillToDo()
        {
            // Not a failure: a list for the developer of every text slot still holding its "write me" marker.
            var content = Content();
            var unwritten = content.Gates.Where(g => g.Intel == null || g.Intel.Contains(TextSceneAsset.Unwritten)).Select(g => $"gate intel: {g.DisplayName}")
                .Concat(content.Scenes.SelectMany(s => s.Lines.Select((l, i) => (s, l, i)))
                    .Where(x => (x.l.text ?? "").Contains(TextSceneAsset.Unwritten) || (x.l.speaker ?? "").StartsWith("["))
                    .Select(x => $"scene {x.s.Id}, line {x.i + 1}: {x.l.text}"))
                .ToList();
            TestContext.WriteLine(unwritten.Count == 0 ? "All text is written." : $"{unwritten.Count} text slots to write:\n  " + string.Join("\n  ", unwritten));
            Assert.Pass($"{unwritten.Count} text slots to write");
        }

        [Test]
        public void PartyScalingMakesBiggerPartiesFaceMoreNeverLess()
        {
            var content = Content();
            Assert.That(content.PartyScaling, Is.Not.Null, "the content has party scaling");
            var config = new SimulationConfig();
            content.ApplyTo(config);
            var tiers = new[] { config.PartyScaling.Solo, config.PartyScaling.Small, config.PartyScaling.Large };
            Assert.That((tiers[0].HealthPercent, tiers[0].DamagePercent, tiers[0].SpawnPercent), Is.EqualTo((100, 100, 100)),
                "solo is the gate as authored, so every normal gate stays soloable");
            for (var i = 1; i < tiers.Length; i++)
            {
                Assert.That(tiers[i].HealthPercent, Is.GreaterThanOrEqualTo(tiers[i - 1].HealthPercent), "health never drops as the party grows");
                Assert.That(tiers[i].DamagePercent, Is.GreaterThanOrEqualTo(tiers[i - 1].DamagePercent), "damage never drops as the party grows");
                Assert.That(tiers[i].SpawnPercent, Is.GreaterThanOrEqualTo(tiers[i - 1].SpawnPercent), "numbers never drop as the party grows");
            }
        }

        [Test]
        public void ABaseJumpClearsNoEnemy()
        {
            // Jumping over an enemy is for classes that raise the jump (the developer's rule).
            var config = new SimulationConfig();
            var content = Content();
            content.ApplyTo(config);
            var heights = content.Enemies.Select(e => e.ToDefinition().Height)
                .Concat(config.Gates.SelectMany(g => g.Rooms).SelectMany(r => r.Spawns).Select(s => s.Enemy.Height));
            Assert.That(heights, Has.All.GreaterThan(config.JumpHeight));
        }

        [Test]
        public void EveryGateWithABossHasARewardPool()
        {
            foreach (var gate in Content().Gates.Where(g => g.Rooms.Any(r => r != null && r.Boss != null)))
                Assert.That(gate.RewardPool, Is.Not.Empty.And.All.Not.Null, $"{gate.name}: a boss gate gives a guaranteed reward");
        }

        [Test]
        public void EverySkillPassesValidation()
        {
            Assert.DoesNotThrow(() => WorldFromContent(Content()));
        }

        [Test]
        public void EveryModifierPassesValidation()
        {
            var content = Content();
            var world = WorldFromContent(content);
            var player = new PlayerId(1);
            world.AddPlayer(player, Vec3.Zero);

            foreach (var asset in content.Modifiers)
                Assert.DoesNotThrow(() => world.GrantModifier(player, asset.ToDefinition()), asset.name);
        }

        [Test]
        public void EveryEnemyLoads()
        {
            var content = Content();
            var world = WorldFromContent(content);

            foreach (var asset in content.Enemies)
                Assert.DoesNotThrow(() => world.SpawnEnemy(asset.ToDefinition(), Vec3.Zero), asset.name);
        }

        [Test]
        public void EveryClassHasItsOwnKit()
        {
            var config = new SimulationConfig();
            Content().ApplyTo(config);

            Assert.That(config.Classes.Select(c => c.Id), Is.SupersetOf(new[] { "warrior", "rogue", "archer", "wizard", "monk", "cleric", "warlock", "summoner" }));
            foreach (var c in config.Classes)
            {
                var kit = config.Skills.Where(s => s.ClassId == c.Id).ToArray();
                Assert.That(kit.Count(s => s.UnlockLevel == 1), Is.GreaterThanOrEqualTo(1), $"{c.Id} starts with a skill");
                Assert.That(kit.Length, Is.GreaterThanOrEqualTo(4), $"{c.Id} has skills to unlock");
                Assert.That(c.StatsPerLevel.Sum(), Is.GreaterThan(0), $"{c.Id} gains stats each level");
            }
            // Magic belongs to the caster classes (ticket 58 changed ADR 0012's "the Wizard's alone").
            Assert.That(config.Skills.Where(s => s.Tags.Contains("magic")).Select(s => s.ClassId).Distinct(), Is.SubsetOf(new[] { "wizard", "warlock", "cleric" }), "magic is the casters'");

            // Only the Rogue jumps over an enemy: its jump clears the tallest one in the game.
            var tallest = Content().Bosses.Select(b => b.ToDefinition(new System.Collections.Generic.Dictionary<ScriptableObject, EnemyDefinition>()).Height).Max();
            foreach (var c in config.Classes)
            {
                var clears = config.JumpHeight * c.JumpHeightPercent / 100f >= tallest;
                Assert.That(clears, Is.EqualTo(c.Id == "rogue"), $"{c.Id} {(clears ? "clears" : "does not clear")} a {tallest} m boss");
            }
        }

        [Test]
        public void TheGuildsStoreIsInTheHubAndEveryRewardSells()
        {
            var config = new SimulationConfig();
            Content().ApplyTo(config);
            var world = new SimulationWorld(config);

            var guild = config.Shops.SingleOrDefault(s => s.Id == "guild-exchange");
            Assert.That(guild, Is.Not.Null, "the guild has a store");
            Assert.That(config.Hub.Floor.Contains(guild.Position), Is.True, "in the hub");
            Assert.That((guild.Position - config.Hub.Board).Length, Is.GreaterThan(guild.Radius + config.Hub.BoardRadius), "clear of the board, so E opens one or the other");
            Assert.That(config.Shops.Where(s => s != guild).Select(s => s.SellPercent), Has.All.GreaterThan(guild.SellPercent), "the guild pays the least");
            foreach (var gate in config.Gates)
            foreach (var item in gate.RewardPool)
                Assert.That(item.SellValue, Is.GreaterThan(0), $"{item.Id} in {gate.Id} sells for something");
            Assert.That(world.Shops.Count, Is.EqualTo(config.Shops.Count));
        }

        [Test]
        public void SkillAndModifierIdsAreUnique()
        {
            var content = Content();

            Assert.That(content.Skills.Select(s => s.Id), Is.Unique);
            Assert.That(content.Modifiers.Select(m => m.Id), Is.Unique);
        }

        [Test]
        public void ItemIdsAreUniqueAcrossModifiersAndGear()
        {
            var config = new SimulationConfig();
            Content().ApplyTo(config);
            var modifierAssets = Content().Modifiers.Select(m => m.ToDefinition());

            var ids = modifierAssets.Select(m => m.Id)
                .Concat(config.Gear.Select(g => g.Id))
                .Concat(config.Gear.SelectMany(g => g.Modifiers).Select(m => m.Id));
            Assert.That(ids, Is.Unique, "saves and snapshots name items by id");
        }

        [Test]
        public void EveryRoleFindsGearInTheFirstGate()
        {
            var config = new SimulationConfig();
            Content().ApplyTo(config);
            var pool = config.Gates[0].RewardPool.OfType<GearDefinition>().ToList();

            Assert.That(pool, Is.Not.Empty);
            foreach (var role in new[] { HunterSystem.Netcode.CompanionRole.Tank, HunterSystem.Netcode.CompanionRole.Marksman, HunterSystem.Netcode.CompanionRole.Caster })
                Assert.That(pool.Any(g => HunterSystem.Netcode.CompanionRoles.GearScore(role, g) > 0), Is.True, $"something for the {role}");
        }
    }
}
