using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using HunterSystem.Simulation;

namespace HunterSystem.Netcode
{
    public enum MessageType : byte
    {
        /// <summary>Client to host, once, on connecting: a stable key (a Steam id in the game) for rejoining, and the character the player brings.</summary>
        Hello = 1,

        /// <summary>Host to one client: your player id and the whole world. Serves joining and rejoining.</summary>
        Welcome = 2,

        /// <summary>Client to host, every tick: one numbered command.</summary>
        Command = 3,

        /// <summary>
        /// Host to every client, every few ticks: the world and each player's last processed command. Compressed, and
        /// usually written against the last world sent to that client (ticket 31): see <see cref="Wire.Snapshot"/>.
        /// </summary>
        Snapshot = 4,

        /// <summary>Host to one client: you cannot join, and why (the session is full).</summary>
        Refused = 5,
    }

    /// <summary>
    /// A world snapshot plus, for each remote player, the number of their last command it includes,
    /// and the host's events since the previous snapshot (encoded by <see cref="SimulationEventCodec"/>)
    /// for clients to show.
    /// </summary>
    public sealed class SnapshotMessage
    {
        public int Tick;
        public readonly Dictionary<int, int> LastProcessedSequence = new Dictionary<int, int>();
        public byte[] World;
        public byte[] Events = Array.Empty<byte>();

        /// <summary>
        /// Party members the cut-down world leaves out, far away in the overworld (ticket 41): where they stand on the
        /// floor, for a map. Sent only now and then; empty in most snapshots.
        /// </summary>
        public readonly Dictionary<int, (float x, float z)> FarPlayers = new Dictionary<int, (float x, float z)>();
    }

    /// <summary>Message encoding. Compact binary, little-endian, no reflection.</summary>
    public static class Wire
    {
        private const byte LightAttackBit = 1, HeavyAttackBit = 2, DodgeBit = 4, RespawnBit = 8, SetReadyBit = 16, ReadyValueBit = 32, JumpBit = 64, SprintBit = 128;
        public static MessageType TypeOf(byte[] message) =>
            message != null && message.Length > 0 ? (MessageType)message[0] : throw new ArgumentException("Empty message.", nameof(message));

        /// <summary>The most a character may weigh on the wire; a real one is a few hundred bytes.</summary>
        public const int MaxCharacterBytes = 64 * 1024;

        /// <param name="character">The player's character as <see cref="CharacterCodec"/> bytes; empty or null for a new one.</param>
        public static byte[] Hello(string clientKey, byte[] character = null) => Write(MessageType.Hello, w =>
        {
            w.Write(clientKey);
            w.Write(character?.Length ?? 0);
            if (character != null) w.Write(character);
        });

        public static (string clientKey, byte[] character) ReadHello(byte[] message) => Read(message, r =>
        {
            var key = r.ReadString();
            var length = r.ReadInt32();
            if (length < 0 || length > MaxCharacterBytes) throw new FormatException("Impossible character size.");
            var character = r.ReadBytes(length);
            if (character.Length != length) throw new FormatException("The character was cut short.");
            return (key, character);
        });

        /// <summary>The whole world, compressed: a welcome has no earlier world to be written against.</summary>
        public static byte[] Welcome(PlayerId you, SnapshotMessage snapshot) => Write(MessageType.Welcome, w =>
        {
            w.Write(you.Value);
            WriteCompressed(w, body => WriteSnapshot(body, snapshot, snapshot.World));
        });

        public static (PlayerId you, SnapshotMessage snapshot) ReadWelcome(byte[] message) =>
            Read(message, r => (new PlayerId(r.ReadInt32()), ReadCompressed(r, body => ReadSnapshot(body, null))));

        private const byte WholeWorld = 0, AgainstBaseline = 1;

        /// <summary>
        /// A snapshot, Deflate-compressed (ticket 31). Given the world this client was last sent (<paramref name="baseline"/>),
        /// the world goes as its byte-by-byte XOR with that one: almost all zeros from one snapshot to the next, so it
        /// compresses to very little. When the two differ in length (something arrived or left) the whole world goes instead.
        /// This relies on the client receiving every snapshot, in order, to know the baseline: the game's channel is
        /// reliable and ordered (ADR 0021).
        /// </summary>
        public static byte[] Snapshot(SnapshotMessage snapshot, byte[] baseline = null) => Write(MessageType.Snapshot, w =>
        {
            var againstBaseline = baseline != null && baseline.Length == snapshot.World.Length;
            w.Write(againstBaseline ? AgainstBaseline : WholeWorld);
            WriteCompressed(w, body => WriteSnapshot(body, snapshot, againstBaseline ? Xor(snapshot.World, baseline) : snapshot.World));
        });

        /// <param name="baseline">The world of the previous snapshot (or welcome) this client received, as rebuilt.</param>
        /// <returns>The snapshot with its <see cref="SnapshotMessage.World"/> whole again.</returns>
        public static SnapshotMessage ReadSnapshot(byte[] message, byte[] baseline) => Read(message, r =>
        {
            var encoding = r.ReadByte();
            if (encoding == AgainstBaseline && baseline == null) throw new FormatException("A snapshot written against a world this client never had.");
            if (encoding != WholeWorld && encoding != AgainstBaseline) throw new FormatException("Unknown snapshot encoding.");
            return ReadCompressed(r, body => ReadSnapshot(body, encoding == AgainstBaseline ? baseline : null));
        });

        private static byte[] Xor(byte[] world, byte[] baseline)
        {
            var result = new byte[world.Length];
            for (var i = 0; i < world.Length; i++) result[i] = (byte)(world[i] ^ baseline[i]);
            return result;
        }

        public static byte[] Command(int sequence, PlayerCommand command) => Write(MessageType.Command, w =>
        {
            w.Write(sequence);
            w.Write(command.MoveX);
            w.Write(command.MoveZ);
            w.Write((byte)((command.LightAttack ? LightAttackBit : 0) | (command.HeavyAttack ? HeavyAttackBit : 0) | (command.Dodge ? DodgeBit : 0) | (command.Respawn ? RespawnBit : 0) |
                (command.SetReady.HasValue ? SetReadyBit : 0) | (command.SetReady == true ? ReadyValueBit : 0) |
                (command.Jump ? JumpBit : 0) | (command.Sprint ? SprintBit : 0)));
            w.Write((sbyte)(command.UseSkillSlot ?? -1));
            w.Write(command.HeldSkillSlots);
            w.Write((sbyte)(command.AllocateStat.HasValue ? (int)command.AllocateStat.Value : -1));
            w.Write(command.AssignSkill.HasValue);
            if (command.AssignSkill.HasValue)
            {
                w.Write(command.AssignSkill.Value.Slot);
                w.Write(command.AssignSkill.Value.SkillId != null);
                if (command.AssignSkill.Value.SkillId != null) w.Write(command.AssignSkill.Value.SkillId);
            }
            w.Write(command.HasAim);
            if (command.HasAim) w.Write(command.AimYaw);
            w.Write(command.SellItemId != null);
            if (command.SellItemId != null) w.Write(command.SellItemId);
            w.Write(command.EquipGearId != null);
            if (command.EquipGearId != null) w.Write(command.EquipGearId);
            w.Write((sbyte)(command.UnequipSlot.HasValue ? (int)command.UnequipSlot.Value : -1));
            w.Write(command.ChangeLook.HasValue);
            if (command.ChangeLook.HasValue)
            {
                w.Write(command.ChangeLook.Value.Colour);
                w.Write((byte)command.ChangeLook.Value.Build);
            }
            w.Write((byte)((command.ToggleMount ? 1 : 0) | (command.Climb > 0 ? 2 : 0) | (command.Climb < 0 ? 4 : 0))); // ticket 90: climbing or diving
            w.Write(command.ChooseSpecialtyId != null);
            if (command.ChooseSpecialtyId != null) w.Write(command.ChooseSpecialtyId);
        });

        public static byte[] Refused(string reason) => Write(MessageType.Refused, w => w.Write(reason));

        public static string ReadRefused(byte[] message) => Read(message, r => r.ReadString());

        /// <summary>
        /// Reads a command, neutralising values no honest client sends: non-finite movement becomes
        /// none, and a stat outside the six becomes no allocation. (Out-of-range slots the simulation ignores itself.)
        /// </summary>
        public static (int sequence, PlayerCommand command) ReadCommand(byte[] message) => Read(message, r =>
        {
            var sequence = r.ReadInt32();
            var moveX = Finite(r.ReadSingle());
            var moveZ = Finite(r.ReadSingle());
            var buttons = r.ReadByte();
            var useSkill = r.ReadSByte();
            var held = r.ReadInt32();
            var stat = r.ReadSByte();
            SkillAssignment? assign = null;
            if (r.ReadBoolean())
            {
                var slot = r.ReadInt32();
                assign = new SkillAssignment(slot, r.ReadBoolean() ? r.ReadString() : null);
            }
            float? aim = r.ReadBoolean() ? r.ReadUInt16() * (360f / PlayerCommand.AimSteps) : (float?)null;
            var sell = r.ReadBoolean() ? r.ReadString() : null;
            var equip = r.ReadBoolean() ? r.ReadString() : null;
            var unequip = r.ReadSByte();
            CharacterLook? look = r.ReadBoolean() ? new CharacterLook(r.ReadByte(), (Build)r.ReadByte()) : (CharacterLook?)null;
            var more = r.ReadByte();
            var specialty = r.ReadBoolean() ? r.ReadString() : null;
            return (sequence, new PlayerCommand(moveX, moveZ,
                lightAttack: (buttons & LightAttackBit) != 0, dodge: (buttons & DodgeBit) != 0, heavyAttack: (buttons & HeavyAttackBit) != 0,
                allocateStat: stat >= 0 && stat < PlayerStats.Count ? (Stat)stat : (Stat?)null,
                useSkillSlot: useSkill >= 0 ? useSkill : (int?)null,
                assignSkill: assign, heldSkillSlots: held, respawn: (buttons & RespawnBit) != 0,
                setReady: (buttons & SetReadyBit) != 0 ? (buttons & ReadyValueBit) != 0 : (bool?)null,
                jump: (buttons & JumpBit) != 0, sprint: (buttons & SprintBit) != 0, aimYawDegrees: aim, sellItemId: sell,
                equipGearId: equip, unequipSlot: unequip >= 0 && unequip < GearDefinition.SlotCount ? (GearSlot)unequip : (GearSlot?)null,
                changeLook: look, toggleMount: (more & 1) != 0, chooseSpecialtyId: specialty,
                climb: (more & 2) != 0 ? 1 : (more & 4) != 0 ? -1 : 0));
        });

        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;

        /// <param name="world">The world as it goes on the wire: whole, or XOR'd with the baseline.</param>
        private static void WriteSnapshot(BinaryWriter w, SnapshotMessage s, byte[] world)
        {
            w.Write(s.Tick);
            w.Write(s.LastProcessedSequence.Count);
            foreach (var pair in s.LastProcessedSequence)
            {
                w.Write(pair.Key);
                w.Write(pair.Value);
            }
            w.Write(world.Length);
            w.Write(world);
            w.Write(s.Events.Length);
            w.Write(s.Events);
            w.Write(s.FarPlayers.Count);
            foreach (var pair in s.FarPlayers)
            {
                w.Write(pair.Key);
                w.Write(pair.Value.x);
                w.Write(pair.Value.z);
            }
        }

        /// <param name="baseline">Null when the world was sent whole; otherwise the world it was XOR'd with.</param>
        private static SnapshotMessage ReadSnapshot(BinaryReader r, byte[] baseline)
        {
            var s = new SnapshotMessage { Tick = r.ReadInt32() };
            for (int i = 0, n = r.ReadInt32(); i < n; i++) s.LastProcessedSequence[r.ReadInt32()] = r.ReadInt32();
            s.World = r.ReadBytes(r.ReadInt32());
            if (baseline != null)
            {
                if (baseline.Length != s.World.Length) throw new FormatException("A snapshot written against a different world.");
                s.World = Xor(s.World, baseline);
            }
            s.Events = r.ReadBytes(r.ReadInt32());
            for (int i = 0, n = r.ReadInt32(); i < n; i++) s.FarPlayers[r.ReadInt32()] = (r.ReadSingle(), r.ReadSingle());
            return s;
        }

        /// <summary>Writes a length-prefixed Deflate block holding whatever <paramref name="body"/> writes.</summary>
        private static void WriteCompressed(BinaryWriter w, Action<BinaryWriter> body)
        {
            using var compressed = new MemoryStream();
            using (var deflate = new DeflateStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            using (var inner = new BinaryWriter(deflate))
            {
                body(inner);
            }
            w.Write((int)compressed.Length);
            w.Write(compressed.GetBuffer(), 0, (int)compressed.Length);
        }

        private static T ReadCompressed<T>(BinaryReader r, Func<BinaryReader, T> body)
        {
            var length = r.ReadInt32();
            if (length < 0) throw new FormatException("Impossible snapshot size.");
            var bytes = r.ReadBytes(length);
            if (bytes.Length != length) throw new FormatException("The snapshot was cut short.");
            using var deflate = new DeflateStream(new MemoryStream(bytes), CompressionMode.Decompress);
            using var inner = new BinaryReader(deflate);
            return body(inner);
        }

        private static byte[] Write(MessageType type, Action<BinaryWriter> body)
        {
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            w.Write((byte)type);
            body(w);
            w.Flush();
            return stream.ToArray();
        }

        private static T Read<T>(byte[] message, Func<BinaryReader, T> body)
        {
            using var r = new BinaryReader(new MemoryStream(message));
            r.ReadByte(); // the type, already checked by the caller
            return body(r);
        }
    }
}
