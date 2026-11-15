using System;
using System.Collections.Generic;
using System.Linq;
using HunterSystem.Netcode;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Drives the simulation from the Unity frame loop. Each frame it samples input, runs as many
    /// fixed ticks as real time calls for, and collects the events those ticks produced. The world
    /// itself knows nothing about Unity; this class and the views are the only bridge.
    /// <para>
    /// Offline (the default) it runs its own world with the players and enemies placed in the scene.
    /// After <see cref="StartHost"/> it runs the authoritative world of a <see cref="HostSession"/>;
    /// after <see cref="StartClient"/>, the predicted world of a <see cref="ClientSession"/>. Either
    /// way views read <see cref="World"/>, and the local player's input goes in one command per tick.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SimulationRunner : MonoBehaviour
    {
        [SerializeField, Tooltip("The game's content and tuning. Every scene points at the same asset.")]
        private GameContentAsset content;

        [SerializeField, Min(1), Tooltip("Online: the host sends a full snapshot every this many ticks")]
        private int snapshotEveryTicks = 3;

        [SerializeField, Tooltip("Gather the party in the content's hub between gates (the co-op scene). Off: an open field (the greybox, the arena).")]
        private bool useHub;

        // The local player's character: loaded from its save at the start, saved as it progresses.
        private PlayerId? _offlineLocalId;
        private bool _savedOnExit;
        private string _saveRoot;
        private bool _savesDisabled;

        private readonly Dictionary<PlayerId, IPlayerCommandSource> _sources = new Dictionary<PlayerId, IPlayerCommandSource>();
        private readonly Dictionary<PlayerId, PlayerCommandBuffer> _buffers = new Dictionary<PlayerId, PlayerCommandBuffer>();
        private readonly Dictionary<PlayerId, PlayerCommand> _commands = new Dictionary<PlayerId, PlayerCommand>();
        private readonly Dictionary<PlayerId, Vec3> _previousPlayerPositions = new Dictionary<PlayerId, Vec3>();
        private readonly Dictionary<EnemyId, Vec3> _previousEnemyPositions = new Dictionary<EnemyId, Vec3>();
        private readonly List<SimulationEvent> _frameEvents = new List<SimulationEvent>();
        private SimulationConfig _config;
        private SimulationWorld _offlineWorld;
        private FixedTimestepClock _clock;

        // Online: one local player, whose id is known once the session says so.
        private IPlayerCommandSource _localSource;
        private PlayerCommandBuffer _localBuffer;

        /// <summary>The world views should draw: offline, the host's, or the client's predicted one (null until a client is welcomed).</summary>
        public SimulationWorld World => Host?.World ?? (Client != null ? Client.World : _offlineWorld);

        public HostSession Host { get; private set; }
        public ClientSession Client { get; private set; }
        public bool IsOnline => Host != null || Client != null;

        /// <summary>The player on this machine: the host's own, the client's once welcomed, or null offline.</summary>
        public PlayerId? LocalPlayerId => Host != null ? Host.LocalPlayerId : Client?.PlayerId;

        /// <summary>The player's save on this PC (and its optional Steam Cloud backup).</summary>
        public CharacterSaves Saves { get; private set; }

        /// <summary>Saves the local player's character as it progresses. Switch it off for a throwaway session (the test arena, bots).</summary>
        public CharacterAutosave Autosave { get; private set; }

        /// <summary>The character the local player brings into the next session: from the save, or new.</summary>
        public CharacterRecord Character { get; private set; }

        /// <summary>Hosting: the host's world progress, saved apart from any character.</summary>
        public WorldRecord HostWorld { get; private set; }

        /// <summary>The classes a character can pick from (ticket 24), in the order the picker shows them.</summary>
        public IReadOnlyList<ClassDefinition> Classes => _config.Classes;

        /// <summary>
        /// True while the local character has no class (a new character, or one from before classes)
        /// and the game has classes to pick from. Offline play waits for the pick; going online needs one.
        /// </summary>
        public bool NeedsClass => !IsOnline && _config.Classes.Count > 0 && Character != null && ClassOf(Character) == null;

        /// <summary>
        /// True while the local character has no name (ticket 28): a new character, or one saved before
        /// names. The character creator asks for a name and looks first; the class picker comes after.
        /// </summary>
        public bool NeedsCreation => !IsOnline && Character != null && Character.Name == null;

        /// <summary>True until the local character is made: named, dressed and given a class. Play and going online wait for it.</summary>
        public bool NeedsSetup => NeedsCreation || NeedsClass;

        /// <summary>The player on this machine: the online one, or the scene's local player offline; null if none.</summary>
        public PlayerId? MyPlayerId => SavedPlayerId;

        /// <summary>
        /// Names the local character and sets its looks (the character creator, ticket 28), and saves
        /// it at once. The name is permanent. Only at the menu or offline, before the class is picked.
        /// </summary>
        public void CreateCharacter(string name, CharacterLook look)
        {
            if (IsOnline) throw new InvalidOperationException("Make your hunter at the menu, before going online.");
            if (!NeedsCreation) throw new InvalidOperationException("This hunter already has a name; it is permanent.");
            var clean = HunterNames.Clean(name) ?? throw new ArgumentException("A hunter needs a name.", nameof(name));
            var record = CharacterCodec.Decode(CharacterCodec.Encode(Character)); // a copy; the loaded record is left alone
            record.IsNew = Character.IsNew;
            record.Name = clean;
            record.Look = look;
            if (_offlineLocalId is { } me && _offlineWorld.HasPlayer(me)) _offlineWorld.SetNameAndLook(me, clean, look);
            Character = record;
            Autosave.SaveNow(record);
        }

        /// <summary>In the hub: change how the player looks (ticket 28). A command like any other, so the host decides.</summary>
        public void RequestLook(PlayerId id, CharacterLook look) => BufferFor(id).QueueLook(look);

        /// <summary>True when the scene has its own local player playing offline (the greybox, the arena).</summary>
        public bool PlaysOffline => _offlineLocalId.HasValue;

        private ClassDefinition ClassOf(CharacterRecord character) => _config.ClassById(character.ClassId);

        /// <summary>The class with this id, or null.</summary>
        public ClassDefinition ClassById(string id) => _config.ClassById(id);

        /// <summary>
        /// Makes the local character a <paramref name="classId"/>, permanently, and saves it at once.
        /// A character that has already levelled (one from before classes) gains the class's stats for
        /// those levels and the class's skills its level has reached. Only at the menu or offline.
        /// </summary>
        public void ChooseClass(string classId)
        {
            if (IsOnline) throw new InvalidOperationException("Pick a class at the menu, before going online.");
            if (!NeedsClass) throw new InvalidOperationException("This character already has a class; the choice is permanent.");
            var chosen = _config.ClassById(classId) ?? throw new ArgumentException($"There is no class '{classId}'.", nameof(classId));

            // A character whose saved class the game no longer has already carries that class's stats.
            var addEarnedStats = Character.ClassId == null;
            CharacterRecord record;
            if (_offlineLocalId is { } me && _offlineWorld.HasPlayer(me))
            {
                record = _offlineWorld.CaptureCharacter(me).WithClass(chosen, _config.Progression, addEarnedStats);
                _offlineWorld.ReplaceCharacter(me, record);
            }
            else
            {
                // A copy without the class id it may have (one this game does not have); the loaded record is left alone.
                var classless = CharacterCodec.Decode(CharacterCodec.Encode(Character));
                classless.ClassId = null;
                record = classless.WithClass(chosen, _config.Progression, addEarnedStats);
            }
            Character = record;
            Autosave.SaveNow(record);
        }

        /// <summary>The game's content list (may be null in a bare test scene).</summary>
        public GameContentAsset Content => content;

        /// <summary>
        /// Events from every tick run this frame, in order. Read them in LateUpdate. On a client these
        /// are its own predicted actions plus everything the host reported (see <see cref="ClientSession.Events"/>).
        /// </summary>
        public IReadOnlyList<SimulationEvent> FrameEvents => _frameEvents;

        private void Awake()
        {
            if (GetComponent<GearPanel>() == null) gameObject.AddComponent<GearPanel>(); // every scene with a runner can change gear (ticket 23)
            if (GetComponent<CharacterCreator>() == null) gameObject.AddComponent<CharacterCreator>(); // and make a hunter, and restyle in the hub (ticket 28)
            if (GetComponent<Nameplates>() == null) gameObject.AddComponent<Nameplates>();
            _config = new SimulationConfig(); // defaults apply when a bare test scene has no content
            if (content != null) content.ApplyTo(_config);
            var overworldScene = FindAnyObjectByType<OverworldScene>(); // an overworld scene's ground and markers (ticket 39)
            if (overworldScene != null && useHub) overworldScene.ApplyTo(_config);
            if (!useHub)
            {
                _config.Hub = null;
                _config.StoryScenes = System.Array.Empty<StorySceneDefinition>(); // the story is told around the hub
            }
            _offlineWorld = new SimulationWorld(_config);
            _clock = new FixedTimestepClock(_config.TickRate);

            _saveRoot = GameSaves.Root; // this runner saves where it loaded from, even if the folder setting changes later
            _savesDisabled = GameSaves.Disabled;
            Saves = new CharacterSaves(_saveRoot, GameSaves.Cloud);
            Autosave = new CharacterAutosave(Saves) { Enabled = !_savesDisabled };
            Character = _savesDisabled ? CharacterRecord.New() : Saves.LoadOrNew();
            if (Saves.LastProblemOnLoad != null) Debug.LogWarning(Saves.LastProblemOnLoad);

            foreach (var view in FindObjectsByType<PlayerView>(FindObjectsSortMode.None))
            {
                var id = view.PlayerId;
                var spawn = ToVec3(view.transform.position);
                if (view.IsLocal && !_offlineLocalId.HasValue)
                {
                    _offlineLocalId = id;
                    _offlineWorld.AddPlayer(id, spawn, Character);
                }
                else
                {
                    _offlineWorld.AddPlayer(id, spawn);
                }
                _previousPlayerPositions[id] = spawn;
                _buffers[id] = new PlayerCommandBuffer();
                _sources[id] = view.IsLocal ? new LocalInputCommandSource() : new ConstantCommandSource(PlayerCommand.None);
            }

            foreach (var view in FindObjectsByType<EnemyView>(FindObjectsSortMode.None))
            {
                var spawn = ToVec3(view.transform.position);
                var id = _offlineWorld.SpawnEnemy(view.CreateDefinition(), spawn);
                _previousEnemyPositions[id] = spawn;
                view.Bind(id);
            }
        }

        // --- Online ------------------------------------------------------------------------

        /// <summary>Hosts a session over <paramref name="transport"/>. The host's own player joins at once.</summary>
        /// <param name="companions">How many of the host's bot teammates come along (ADR 0014): 0 to <see cref="CompanionParty.Size"/>.</param>
        public void StartHost(IHostTransport transport, Func<PlayerId, Vec3> spawnPoint, int companions = 0)
        {
            if (IsOnline) throw new InvalidOperationException("Already online.");
            Host = new HostSession(_config, transport, spawnPoint, snapshotEveryTicks);
            Host.AddLocalPlayer(Character);
            HostWorld = _savesDisabled ? new WorldRecord() : new WorldSaves(_saveRoot).LoadOrNew();
            Host.World.SetClearedGates(HostWorld.ClearedGates); // so the board, and guests, see them
            Host.World.SetSeenStoryScenes(HostWorld.SeenStoryScenes); // so a scene plays once per world
            Host.World.SetFoundGates(HostWorld.FoundGates); // the overworld's found gates and open regions are the host's world's (ticket 38)
            Host.World.SetOpenRegions(HostWorld.OpenRegions);
            AddCompanions(Math.Clamp(companions, 0, CompanionParty.Size));
            StartLocalInput();
        }

        /// <summary>
        /// The host's saved teammates join (a new rookie party the first time), each brought up to the
        /// host's level if it fell behind. The whole party is kept, even the ones left at home this time.
        /// </summary>
        private void AddCompanions(int count)
        {
            _companionsAtHome = null;
            if (count == 0) return;
            var saves = _savesDisabled ? null : new CompanionSaves(_saveRoot);
            var party = saves?.LoadOrNull();
            if (saves?.LastProblem != null) Debug.LogWarning(saves.LastProblem);
            if (party == null || party.Count < CompanionParty.Size)
            {
                var fresh = CompanionParty.NewParty(Character?.ClassId, _config);
                party = (party ?? new List<CompanionRecord>()).Concat(fresh.Skip(party?.Count ?? 0)).ToList();
            }

            var level = Character == null || Character.IsNew ? 1 : Character.Level;
            for (var i = 0; i < party.Count; i++)
            {
                if (i >= count) continue;
                party[i].Character = CompanionParty.CaughtUp(party[i].Character, level, _config);
                Host.AddCompanion(party[i], seed: 7919 * (i + 1));
            }
            _companionsAtHome = party.Skip(count).ToList();
        }

        /// <summary>Teammates who stayed out of this session, kept so the save still has all of them.</summary>
        private List<CompanionRecord> _companionsAtHome;

        private void SaveCompanions()
        {
            if (_savesDisabled || Host == null || Host.Companions.Count == 0) return;
            try
            {
                var party = Host.CaptureCompanions();
                if (_companionsAtHome != null) party.AddRange(_companionsAtHome);
                new CompanionSaves(_saveRoot).Save(party);
            }
            catch (Exception e)
            {
                Debug.LogError("Could not save the teammates: " + e.Message);
            }
        }

        /// <summary>Joins a session over <paramref name="transport"/>; <paramref name="clientKey"/> identifies this player for rejoining.</summary>
        public void StartClient(IClientTransport transport, string clientKey)
        {
            if (IsOnline) throw new InvalidOperationException("Already online.");
            Client = new ClientSession(_config, transport, clientKey, Character);
            StartLocalInput();
        }

        /// <summary>
        /// Leaves the session. The local player's character is saved first, with what it earned
        /// there, and becomes the character brought to the next session; a host's world is saved too.
        /// </summary>
        public void StopOnline()
        {
            SaveProgress();
            Client?.Disconnect();
            Host = null;
            Client = null;
            HostWorld = null;
        }

        /// <summary>
        /// Loads the character from its save again (after restoring it from Steam Cloud). Only where
        /// no character is in play: between sessions, in a scene without a local player of its own.
        /// </summary>
        public void ReloadCharacter()
        {
            if (IsOnline || _offlineLocalId.HasValue) throw new InvalidOperationException("A character is in play; change it from the menu, between sessions.");
            Character = Saves.LoadOrNew();
        }

        /// <summary>The player on this machine whose character is saved: the online one, or the scene's local player offline.</summary>
        private PlayerId? SavedPlayerId => IsOnline ? LocalPlayerId : _offlineLocalId;

        /// <summary>
        /// The local character as it should be saved now, or null if there is none. A guest saves
        /// what the host last confirmed, never its own prediction (ADR 0005).
        /// </summary>
        private CharacterRecord CharacterToSave()
        {
            if (Client != null) return Client.ConfirmedCharacter();
            var me = SavedPlayerId;
            return me.HasValue && World != null && World.HasPlayer(me.Value) ? World.CaptureCharacter(me.Value) : null;
        }

        /// <summary>
        /// Writes the local character now, and a host's world. Each is tried on its own, and a
        /// failure is reported, never thrown: it must not stop a quit or the other save.
        /// </summary>
        private void SaveProgress()
        {
            try
            {
                var character = CharacterToSave();
                if (character != null)
                {
                    if (Autosave.Enabled) Character = character; // kept for the next session even if the write below fails
                    Autosave.SaveNow(character);
                    if (Saves.LastProblem != null) Debug.LogWarning(Saves.LastProblem);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Could not save the character: " + e.Message);
            }

            SaveCompanions();
            try
            {
                if (Host != null && HostWorld != null && !_savesDisabled) new WorldSaves(_saveRoot).Save(HostWorld);
            }
            catch (Exception e)
            {
                Debug.LogError("Could not save the world: " + e.Message);
            }
        }

        private void OnApplicationQuit() => SaveBeforeExit();

        private void OnDestroy() => SaveBeforeExit();

        /// <summary>
        /// The last save of this runner's life; later calls do nothing. Public so that whatever shuts
        /// services down on quit (Steam, for the cloud copy) can have the save happen first.
        /// </summary>
        public void SaveBeforeExit()
        {
            if (_savedOnExit) return;
            _savedOnExit = true;
            SaveProgress();
        }

        private void StartLocalInput()
        {
            _localSource = new LocalInputCommandSource();
            _localBuffer = new PlayerCommandBuffer();
            _previousPlayerPositions.Clear();
            _previousEnemyPositions.Clear();
        }

        // --- Offline helpers (the arena, tests) ------------------------------------------------

        /// <summary>Spawns an enemy mid-run with its greybox visual (the test arena; later, gate spawners).</summary>
        public EnemyView SpawnEnemy(EnemyDefinitionAsset definition, Vector3 position, Material bodyMaterial)
        {
            var view = GreyboxVisuals.CreateEnemy(definition != null ? definition.name : "Enemy", position, definition, bodyMaterial);
            var spawn = ToVec3(position);
            var id = World.SpawnEnemy(view.CreateDefinition(), spawn);
            _previousEnemyPositions[id] = spawn;
            view.Bind(id);
            return view;
        }

        /// <summary>Removes an enemy and its visual outright (no defeat, no XP).</summary>
        public void DespawnEnemy(EnemyView view)
        {
            if (view == null) return;
            if (World.HasEnemy(view.Id)) World.DespawnEnemy(view.Id);
            _previousEnemyPositions.Remove(view.Id);
            Destroy(view.gameObject);
        }

        /// <summary>A loadout change for the player, sent to the simulation as a command on the next tick.</summary>
        public void RequestSkillAssignment(PlayerId id, SkillAssignment assignment) => BufferFor(id).QueueSkillAssignment(assignment);

        /// <summary>
        /// Spends one of the player's unspent stat points on <paramref name="stat"/>. It goes to the
        /// simulation as a command on the next tick, so the simulation stays the authority.
        /// </summary>
        public void RequestStatAllocation(PlayerId id, Stat stat) => BufferFor(id).QueueStatAllocation(stat);

        /// <summary>A defeated player asking to come back, sent as a command on the next tick. The gate's death rules decide what happens.</summary>
        public void RequestRespawn(PlayerId id) => BufferFor(id).QueueRespawn();

        /// <summary>In the hub: marks the player ready (or not) for the next gate, as a command on the next tick.</summary>
        public void RequestReady(PlayerId id, bool ready) => BufferFor(id).QueueSetReady(ready);

        /// <summary>Sells one duplicate at the store the player stands at (the store's menu).</summary>
        public void RequestSale(PlayerId id, string itemId) => BufferFor(id).QueueSale(itemId);

        /// <summary>Wears a piece from the player's bag (the gear panel).</summary>
        public void RequestEquip(PlayerId id, string gearId) => BufferFor(id).QueueEquip(gearId);

        /// <summary>Takes off whatever the player wears in a slot (the gear panel).</summary>
        public void RequestUnequip(PlayerId id, GearSlot slot) => BufferFor(id).QueueUnequip(slot);

        /// <summary>Replaces where a player's commands come from (tests, bots).</summary>
        public void SetCommandSource(PlayerId id, IPlayerCommandSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (IsOnline && id == LocalPlayerId)
            {
                _localSource = source;
                return;
            }
            if (World == null) throw new InvalidOperationException("Not joined yet: there is no world to set a command source in.");
            World.GetPlayer(id); // throws now, not every frame, if the player is not in the simulation
            _sources[id] = source;
        }

        /// <summary>Online: replaces where the local player's commands come from (automated tests, bots).</summary>
        public void SetLocalCommandSource(IPlayerCommandSource source) =>
            _localSource = source ?? throw new ArgumentNullException(nameof(source));

        private PlayerCommandBuffer BufferFor(PlayerId id)
        {
            if (IsOnline && id == LocalPlayerId) return _localBuffer;
            if (_buffers.TryGetValue(id, out var buffer)) return buffer;
            throw new ArgumentException($"{id} has no input buffer on this machine.", nameof(id));
        }

        // --- The frame loop ------------------------------------------------------------------

        private void Update()
        {
            _frameEvents.Clear();
            if (IsOnline) UpdateOnline();
            else UpdateOffline();
            AutosaveCharacter();
        }

        private void AutosaveCharacter()
        {
            var me = SavedPlayerId;
            if (!me.HasValue || World == null || !World.HasPlayer(me.Value)) return;
            Autosave.Observe(_frameEvents, me.Value);
            try
            {
                Autosave.SaveIfDue(CharacterToSave, Time.unscaledTime);
            }
            catch (Exception e)
            {
                Debug.LogError("Could not save the character: " + e.Message);
            }
        }

        private void UpdateOffline()
        {
            if (NeedsSetup && _offlineLocalId.HasValue) return; // the world waits for the hunter to be made (name, looks, class)
            foreach (var pair in _sources) _buffers[pair.Key].Add(pair.Value.Sample());

            var ticks = _clock.Advance(Time.deltaTime);
            for (var i = 0; i < ticks; i++)
            {
                _commands.Clear();
                foreach (var pair in _buffers) _commands[pair.Key] = pair.Value.TakeForTick();
                RememberPositions();
                _offlineWorld.Step(_commands);
                _frameEvents.AddRange(_offlineWorld.Events);
            }
        }

        private void UpdateOnline()
        {
            _localBuffer.Add(_localSource.Sample());

            var ticks = _clock.Advance(Time.deltaTime);
            for (var i = 0; i < ticks; i++)
            {
                RememberPositions();
                var command = _localBuffer.TakeForTick();
                if (Host != null)
                {
                    Host.Tick(command);
                    _frameEvents.AddRange(Host.World.Events);
                    RecordHostWorldProgress(Host.World.Events);
                }
                else
                {
                    Client.Tick(command);
                    _frameEvents.AddRange(Client.Events);
                }
            }
        }

        /// <summary>The host's world keeps which gates it has cleared and which story scenes it has played (ADR 0005: host-world progress), saved at once.</summary>
        private void RecordHostWorldProgress(IReadOnlyList<SimulationEvent> events)
        {
            if (HostWorld == null) return;
            foreach (var e in events)
            {
                if (e is GateFound found && found.GateId != null && !HostWorld.FoundGates.Contains(found.GateId))
                {
                    HostWorld.FoundGates.Add(found.GateId);
                    SaveHostWorld();
                    continue;
                }
                if (e is StorySceneStarted scene && !HostWorld.SeenStoryScenes.Contains(scene.SceneId))
                {
                    HostWorld.SeenStoryScenes.Add(scene.SceneId);
                    SaveHostWorld();
                    continue;
                }
                var id = (e as GateCleared)?.GateId;
                if (id != null) SaveCompanions(); // a clear is what they learn most from
                if (id == null || HostWorld.ClearedGates.Contains(id)) continue;
                HostWorld.ClearedGates.Add(id);
                SaveHostWorld();
            }
        }

        private void SaveHostWorld()
        {
            if (_savesDisabled) return;
            try
            {
                new WorldSaves(_saveRoot).Save(HostWorld);
            }
            catch (Exception error)
            {
                Debug.LogError("Could not save the world: " + error.Message);
            }
        }

        private void RememberPositions()
        {
            var world = World;
            if (world == null) return;
            foreach (var id in world.PlayerIds) _previousPlayerPositions[id] = CurrentPosition(id);
            foreach (var id in world.EnemyIds) _previousEnemyPositions[id] = world.GetEnemy(id).Position;
        }

        /// <summary>Position to draw this frame, interpolated between the last two ticks.</summary>
        public Vector3 GetRenderPosition(PlayerId id)
        {
            var current = CurrentPosition(id);
            return Interpolate(_previousPlayerPositions.TryGetValue(id, out var previous) ? previous : current, current);
        }

        /// <summary>Position to draw this frame, interpolated between the last two ticks.</summary>
        public Vector3 GetRenderPosition(EnemyId id)
        {
            var current = World.GetEnemy(id).Position;
            return Interpolate(_previousEnemyPositions.TryGetValue(id, out var previous) ? previous : current, current);
        }

        /// <summary>On a client, other players glide between snapshots; everyone else is where the world has them.</summary>
        private Vec3 CurrentPosition(PlayerId id) =>
            Client != null && Client.IsJoined ? Client.DisplayPosition(id) : World.GetPlayer(id).Position;

        private Vector3 Interpolate(Vec3 previous, Vec3 current) =>
            Vector3.Lerp(ToVector3(previous), ToVector3(current), _clock.Alpha);

        private static Vec3 ToVec3(Vector3 v) => new Vec3(v.x, v.y, v.z);
        private static Vector3 ToVector3(Vec3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
