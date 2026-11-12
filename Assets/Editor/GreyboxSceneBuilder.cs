using System.Collections.Generic;
using System.Linq;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using HunterSystem.Game;
using HunterSystem.Online;
using HunterSystem.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HunterSystem.Editor
{
    /// <summary>
    /// Builds the two greybox scenes from shared parts and keeps the game's content list current:
    /// <list type="bullet">
    /// <item>Assets/Scenes/Greybox.unity: the first fight. A player, three Goblins, the System HUD,
    /// skill visuals and the F5-F8 modifier toggles.</item>
    /// <item>Assets/Scenes/Arena.unity: the test arena. A player and the arena panel (F1) to spawn
    /// enemies and set level, loadout and modifiers.</item>
    /// </list>
    /// Data assets are created once and then left alone, so Inspector tuning survives a rebuild.
    /// GameContent lists the player tuning, progression, and every skill, modifier and enemy asset in
    /// the project; both scenes use it.
    /// Regenerate from the menu or with:
    /// Unity -batchmode -quit -projectPath . -executeMethod HunterSystem.Editor.GreyboxSceneBuilder.BuildAll
    /// </summary>
    public static class GreyboxSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Greybox.unity";
        public const string ArenaScenePath = "Assets/Scenes/Arena.unity";
        public const string CoopScenePath = "Assets/Scenes/Coop.unity";
        private const string MaterialsFolder = "Assets/Art/Materials";
        private const string DataFolder = "Assets/Data";
        private const string ProgressionPath = "Assets/Data/Progression.asset";
        private const string PlayerTuningPath = "Assets/Data/PlayerTuning.asset";
        private const string ContentPath = "Assets/Data/GameContent.asset";
        private static readonly Color GoblinColour = new Color(0.8f, 0.2f, 0.2f);

        /// <summary>Demo modifiers, as created on a fresh project.</summary>
        private static readonly ModifierDefinition[] DemoModifierData =
        {
            new ModifierDefinition
            {
                Id = "searing", Name = "Searing", Description = "Fire skills deal 50% more damage.",
                AppliesToTag = SkillTags.Fire, DamagePercent = 50,
            },
            new ModifierDefinition
            {
                Id = "split-shot", Name = "Split Shot", Description = "Projectile skills fire three bolts in a fan.",
                AppliesToTag = SkillTags.Projectile, ExtraProjectiles = 2, ProjectileSpreadDegrees = 20f,
            },
            new ModifierDefinition
            {
                Id = "ember-core", Name = "Ember Core", Description = "Every fire hit bursts, burning nearby enemies for half the damage.",
                AppliesToTag = SkillTags.Fire, ExplosionRadius = 2.5f, ExplosionDamagePercent = 50,
            },
            new ModifierDefinition
            {
                Id = "deep-rend", Name = "Deep Rend", Description = "Rending Slash: 50% more damage, 25% shorter cooldown, 1 m longer reach.",
                AppliesToSkillId = "rending-slash", DamagePercent = 50, CooldownPercent = -25, RangeAdd = 1f,
            },
        };

        /// <summary>
        /// Every skill as created on a fresh project, in bar order (starters first, then by unlock level).
        /// After that the assets are the source of truth: rebuilding never overwrites them.
        /// </summary>
        private static readonly SkillDefinition[] SkillData =
        {
            new SkillDefinition
            {
                Id = "rending-slash", Name = "Rending Slash", Effect = SkillEffectKind.Cone, Damage = 30, Range = 3f, ArcDegrees = 160f,
                CooldownSeconds = 4f, UnlockLevel = 1, ScalingStat = Stat.Strength, Tags = new[] { "melee", "physical" },
                ChargeSeconds = 0.8f, ChargedDamagePercent = 100, ChargeUnlockLevel = 4,
            },
            new SkillDefinition
            {
                Id = "ground-slam", Name = "Ground Slam", Effect = SkillEffectKind.Area, Damage = 20, Radius = 3.5f,
                CooldownSeconds = 6f, UnlockLevel = 1, ScalingStat = Stat.Strength, Tags = new[] { "melee", "area", "physical" },
                ChargeSeconds = 1f, ChargedSizePercent = 100, ChargeUnlockLevel = 6,
            },
            new SkillDefinition
            {
                Id = "fire-bolt", Name = "Fire Bolt", Effect = SkillEffectKind.Projectile, Damage = 25, Range = 12f, Radius = 0.5f,
                ProjectileSpeed = 15f, CooldownSeconds = 2f, UnlockLevel = 1, ScalingStat = Stat.Intelligence,
                Tags = new[] { "fire", "projectile", "magic" },
            },
            new SkillDefinition
            {
                Id = "flame-burst", Name = "Flame Burst", Effect = SkillEffectKind.Area, Damage = 35, Radius = 4f,
                CooldownSeconds = 8f, UnlockLevel = 3, ScalingStat = Stat.Intelligence, Tags = new[] { "fire", "area", "magic" },
                ChargeSeconds = 1f, ChargedSizePercent = 60, ChargeUnlockLevel = 7,
            },
            new SkillDefinition
            {
                Id = "hunters-javelin", Name = "Hunter's Javelin", Effect = SkillEffectKind.Projectile, Damage = 40, Range = 16f, Radius = 0.6f,
                ProjectileSpeed = 22f, CooldownSeconds = 5f, UnlockLevel = 5, ScalingStat = Stat.Strength,
                Tags = new[] { "projectile", "physical" },
                ChargeSeconds = 1f, ChargedDamagePercent = 100, ChargeUnlockLevel = 8,
            },
            new SkillDefinition
            {
                Id = "whirlwind", Name = "Whirlwind", Effect = SkillEffectKind.Area, Damage = 30, Radius = 3.2f,
                CooldownSeconds = 7f, UnlockLevel = 10, ScalingStat = Stat.Strength, Tags = new[] { "melee", "area", "physical" },
                ChargeSeconds = 1.2f, ChargedSizePercent = 80, ChargeUnlockLevel = 12,
            },
            new SkillDefinition
            {
                Id = "inferno-wave", Name = "Inferno Wave", Effect = SkillEffectKind.Cone, Damage = 45, Range = 6f, ArcDegrees = 70f,
                CooldownSeconds = 10f, UnlockLevel = 15, ScalingStat = Stat.Intelligence, Tags = new[] { "fire", "magic" },
                ChargeSeconds = 1f, ChargedSizePercent = 60, ChargeUnlockLevel = 18,
            },
            new SkillDefinition
            {
                // The capstone: no charge, it is already enormous.
                Id = "monarchs-decree", Name = "Monarch's Decree", Effect = SkillEffectKind.Area, Damage = 80, Radius = 6f,
                CooldownSeconds = 20f, UnlockLevel = 20, ScalingStat = Stat.Intelligence, Tags = new[] { "magic", "area" },
            },
        };

        /// <summary>Three Goblins at 25 XP each: enough for the first level-up (50 XP) in one fight.</summary>
        private static readonly Vector3[] GoblinSpawns = { new Vector3(0f, 0f, 8f), new Vector3(-4f, 0f, 10f), new Vector3(4f, 0f, 10f) };

        [MenuItem("Hunter System/Rebuild All Scenes")]
        public static void BuildAll()
        {
            Build();
            BuildArena();
            BuildCoop();
        }

        [MenuItem("Hunter System/Rebuild Greybox Scene")]
        public static void Build() => BuildScene(ScenePath, simulation =>
        {
            simulation.AddComponent<DebugModifierToggles>();
            var goblin = GetOrCreateGoblinDefinition();
            var goblinMaterial = GetOrCreateMaterial("Greybox_Goblin", GoblinColour);
            for (var i = 0; i < GoblinSpawns.Length; i++)
                GreyboxVisuals.CreateEnemy($"Goblin {i + 1}", GoblinSpawns[i], goblin, goblinMaterial);
        });

        [MenuItem("Hunter System/Rebuild Test Arena Scene")]
        public static void BuildArena() => BuildScene(ArenaScenePath, simulation =>
            Wire(simulation.AddComponent<ArenaPanel>(), ("enemyMaterial", GetOrCreateMaterial("Greybox_Goblin", GoblinColour))));

        /// <summary>
        /// The co-op spike scene: no player placed (players appear as they join), the online views,
        /// the launcher menu, and two inactive network managers: Tugboat for local play, FishyFacepunch for Steam.
        /// </summary>
        [MenuItem("Hunter System/Rebuild Co-op Scene")]
        public static void BuildCoop() => BuildScene(CoopScenePath, simulation =>
        {
            Wire(simulation.AddComponent<OnlineViews>(),
                ("localPlayerMaterial", GetOrCreateMaterial("Greybox_Player", new Color(0.2f, 0.45f, 0.85f))),
                ("remotePlayerMaterial", GetOrCreateMaterial("Greybox_Ally", new Color(0.25f, 0.75f, 0.4f))),
                ("swingMaterial", GetOrCreateMaterial("Greybox_Swing", new Color(1f, 0.85f, 0.3f))),
                ("enemyMaterial", GetOrCreateMaterial("Greybox_Goblin", GoblinColour)));

            var local = CreateNetwork<Tugboat>("Network (Local UDP)");
            var steam = CreateNetwork<FishyFacepunch.FishyFacepunch>("Network (Steam)");
            Wire(simulation.AddComponent<CoopLauncher>(), ("localNetwork", local), ("steamNetwork", steam));
        }, includePlayer: false);

        /// <summary>
        /// An inactive Fish-Net network manager with the given transport; the launcher activates the
        /// one it uses. Fish-Net sends on its own network tick (30 Hz by default), which would add up
        /// to a tick of delay each way on top of ours, so it runs at the simulation's 60 Hz.
        /// </summary>
        private static GameObject CreateNetwork<TTransport>(string name) where TTransport : Component
        {
            var network = new GameObject(name);
            network.SetActive(false);
            network.AddComponent<TTransport>();
            var time = network.AddComponent<FishNet.Managing.Timing.TimeManager>();
            var so = new SerializedObject(time);
            so.FindProperty("_tickRate").intValue = 60;
            so.ApplyModifiedPropertiesWithoutUndo();
            network.AddComponent<NetworkManager>();
            return network;
        }

        /// <summary>A scene with the shared parts (light, camera, floor, simulation, player), plus <paramref name="addExtras"/>.</summary>
        private static void BuildScene(string path, System.Action<GameObject> addExtras, bool includePlayer = true)
        {
            // New scene first: opening it unloads unused assets, which would orphan anything loaded before.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var content = GetOrCreateContent();
            var cameraRig = CreateLightCameraAndFloor();
            var simulation = CreateSimulation(content);
            if (includePlayer) CreatePlayer(cameraRig);
            addExtras(simulation);

            EditorSceneManager.SaveScene(scene, path);
            RegisterScenes();
        }

        // --- Shared scene parts ------------------------------------------------------------

        private static ThirdPersonCamera CreateLightCameraAndFloor()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            // Matches the rig's starting orbit so the scene view looks right before Play.
            var focus = Vector3.up * ThirdPersonCamera.DefaultTargetHeight;
            camera.transform.position = CameraMath.OrbitPosition(focus, 0f, ThirdPersonCamera.DefaultPitch, ThirdPersonCamera.DefaultDistance);
            camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position, Vector3.up);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(5f, 1f, 5f); // 50 m x 50 m
            floor.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("Greybox_Floor", new Color(0.55f, 0.55f, 0.55f));

            return camera.gameObject.AddComponent<ThirdPersonCamera>();
        }

        private static GameObject CreateSimulation(GameContentAsset content)
        {
            var simulation = new GameObject("Simulation");
            Wire(simulation.AddComponent<SimulationRunner>(), ("content", content));
            simulation.AddComponent<SystemHud>();
            Wire(simulation.AddComponent<SkillEffectsView>(),
                ("projectileMaterial", GetOrCreateMaterial("Greybox_Fire", new Color(1f, 0.45f, 0.1f))),
                ("areaMaterial", GetOrCreateMaterial("Greybox_SkillArea", new Color(1f, 0.75f, 0.3f))),
                ("coneMaterial", GetOrCreateMaterial("Greybox_SkillCone", new Color(0.6f, 0.85f, 1f))),
                ("explosionMaterial", GetOrCreateMaterial("Greybox_Explosion", new Color(1f, 0.3f, 0.05f))));
            return simulation;
        }

        /// <summary>The player sits at the feet; the capsule and the swing marker are children above.</summary>
        private static void CreatePlayer(ThirdPersonCamera cameraRig)
        {
            var player = new GameObject("Player 1");
            var playerView = player.AddComponent<PlayerView>();
            var playerBody = GreyboxVisuals.CreateVisual(player, PrimitiveType.Capsule, "Capsule", new Vector3(0f, 1f, 0f), Vector3.one,
                GetOrCreateMaterial("Greybox_Player", new Color(0.2f, 0.45f, 0.85f)));
            // A flat slab in front of the player, shown for a moment on each light attack.
            var swing = GreyboxVisuals.CreateVisual(player, PrimitiveType.Cube, "Swing Marker", new Vector3(0f, 1f, 1.1f), new Vector3(1.6f, 0.1f, 0.9f),
                GetOrCreateMaterial("Greybox_Swing", new Color(1f, 0.85f, 0.3f)));
            Wire(playerView, ("swingMarker", swing.gameObject), ("body", playerBody));
            Wire(cameraRig, ("target", player.transform));
        }

        /// <summary>Greybox first (the default build scene), then the arena, then anything else.</summary>
        private static void RegisterScenes()
        {
            var builtScenes = new[] { ScenePath, ArenaScenePath, CoopScenePath };
            var existing = builtScenes.Where(p => AssetDatabase.LoadAssetAtPath<SceneAsset>(p) != null).Select(p => new EditorBuildSettingsScene(p, true));
            var others = EditorBuildSettings.scenes.Where(s => !builtScenes.Contains(s.path));
            EditorBuildSettings.scenes = existing.Concat(others).ToArray();
            AssetDatabase.SaveAssets();
        }

        // --- Content -----------------------------------------------------------------------

        /// <summary>
        /// The GameContent asset, with every skill, modifier and enemy asset in the project listed.
        /// Existing order is kept (it sets the starting bar) and newly found assets are appended, so
        /// adding content is a matter of creating its asset.
        /// </summary>
        private static GameContentAsset GetOrCreateContent()
        {
            var tuning = GetOrCreateSingle<PlayerTuningAsset>(PlayerTuningPath);
            var progression = GetOrCreateSingle<ProgressionAsset>(ProgressionPath); // its defaults hold the default curve
            var skills = SkillData.Select(GetOrCreateSkill).ToArray();
            var modifiers = DemoModifierData.Select(GetOrCreateModifier).ToArray();
            var enemies = new[] { GetOrCreateGoblinDefinition(), GetOrCreateHobgoblinDefinition() };

            var content = AssetDatabase.LoadAssetAtPath<GameContentAsset>(ContentPath);
            if (content == null)
            {
                content = ScriptableObject.CreateInstance<GameContentAsset>();
                AssetDatabase.CreateAsset(content, ContentPath);
            }

            content.ReplaceContent(tuning, progression,
                WithEveryProjectAsset(content.Skills, skills),
                WithEveryProjectAsset(content.Modifiers, modifiers),
                WithEveryProjectAsset(content.Enemies, enemies));
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();
            // Saving re-imports the asset; hand back the reloaded object, or scene references to it come out empty.
            return AssetDatabase.LoadAssetAtPath<GameContentAsset>(ContentPath);
        }

        /// <summary>
        /// The current list, then the given defaults, then every other asset of the type found anywhere
        /// in the project, without repeats. (The content test still fails on assets added after the last rebuild.)
        /// </summary>
        private static T[] WithEveryProjectAsset<T>(IReadOnlyList<T> current, IEnumerable<T> defaults) where T : Object
        {
            var everyAsset = AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));
            return current.Where(a => a != null).Concat(defaults).Concat(everyAsset).Distinct().ToArray();
        }

        /// <summary>Assigns private [SerializeField] references, as dragging them in the Inspector would.</summary>
        private static void Wire(Object target, params (string field, Object value)[] fields)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in fields) so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SkillDefinitionAsset GetOrCreateSkill(SkillDefinition data) =>
            GetOrCreate<SkillDefinitionAsset>("Skills", data.Name, a => a.CopyFrom(data));

        private static ModifierDefinitionAsset GetOrCreateModifier(ModifierDefinition data) =>
            GetOrCreate<ModifierDefinitionAsset>("Modifiers", data.Name, a => a.CopyFrom(data));

        private static EnemyDefinitionAsset GetOrCreateGoblinDefinition() =>
            GetOrCreate<EnemyDefinitionAsset>("Enemies", "Goblin", a => a.SetStats("Goblin", maxHealth: 30, moveSpeed: 3f, stopDistance: 1.2f));

        /// <summary>A slower, tougher enemy with a longer, harder-hitting telegraph, so the arena has a real choice.</summary>
        private static EnemyDefinitionAsset GetOrCreateHobgoblinDefinition() =>
            GetOrCreate<EnemyDefinitionAsset>("Enemies", "Hobgoblin", a =>
            {
                a.SetStats("Hobgoblin", maxHealth: 90, moveSpeed: 2.2f, stopDistance: 1.4f);
                a.SetAttackAndReward(damage: 20, range: 1.9f, windupSeconds: 0.8f, cooldownSeconds: 2.2f, xpReward: 60);
            });

        /// <summary>A settings asset at a fixed path, created with its field defaults if missing and never overwritten.</summary>
        private static T GetOrCreateSingle<T>(string path) where T : ScriptableObject
        {
            EnsureFolder("Assets", "Data");
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset; // keep any tuning done in the Inspector

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Loads <c>Assets/Data/subfolder/Name.asset</c>, or creates it with <paramref name="initialise"/>. Existing assets are never overwritten.</summary>
        private static T GetOrCreate<T>(string subfolder, string displayName, System.Action<T> initialise) where T : ScriptableObject
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder(DataFolder, subfolder);
            // File names keep letters and digits only ("Hunter's Javelin" -> HuntersJavelin.asset).
            var fileName = new string(displayName.Where(char.IsLetterOrDigit).ToArray());
            var path = $"{DataFolder}/{subfolder}/{fileName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset; // keep any tuning done in the Inspector

            asset = ScriptableObject.CreateInstance<T>();
            initialise(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MaterialsFolder)) AssetDatabase.CreateFolder("Assets/Art", "Materials");
            var path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline.defaultShader
                    : Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
