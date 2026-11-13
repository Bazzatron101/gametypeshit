using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>Host to every client, every few ticks: the whole world and each player's last processed command.</summary>
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
    }

    /// <summary>Message encoding. Compact binary, little-endian, no reflection.</summary>
    public static class Wire
    {
        private const byte LightAttackBit = 1, HeavyAttackBit = 2, DodgeBit = 4, RespawnBit = 8;
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

        public static byte[] Welcome(PlayerId you, SnapshotMessage snapshot) => Write(MessageType.Welcome, w =>
        {
            w.Write(you.Value);
            WriteSnapshot(w, snapshot);
        });

        public static (PlayerId you, SnapshotMessage snapshot) ReadWelcome(byte[] message) =>
            Read(message, r => (new PlayerId(r.ReadInt32()), ReadSnapshot(r)));

        public static byte[] Snapshot(SnapshotMessage snapshot) => Write(MessageType.Snapshot, w => WriteSnapshot(w, snapshot));

        public static SnapshotMessage ReadSnapshot(byte[] message) => Read(message, ReadSnapshot);

        public static byte[] Command(int sequence, PlayerCommand command) => Write(MessageType.Command, w =>
        {
            w.Write(sequence);
            w.Write(command.MoveX);
            w.Write(command.MoveZ);
            w.Write((byte)((command.LightAttack ? LightAttackBit : 0) | (command.HeavyAttack ? HeavyAttackBit : 0) | (command.Dodge ? DodgeBit : 0) | (command.Respawn ? RespawnBit : 0)));
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
            return (sequence, new PlayerCommand(moveX, moveZ,
                lightAttack: (buttons & LightAttackBit) != 0, dodge: (buttons & DodgeBit) != 0, heavyAttack: (buttons & HeavyAttackBit) != 0,
                allocateStat: stat >= 0 && stat < PlayerStats.Count ? (Stat)stat : (Stat?)null,
                useSkillSlot: useSkill >= 0 ? useSkill : (int?)null,
                assignSkill: assign, heldSkillSlots: held, respawn: (buttons & RespawnBit) != 0));
        });

        private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;

        private static void WriteSnapshot(BinaryWriter w, SnapshotMessage s)
        {
            w.Write(s.Tick);
            w.Write(s.LastProcessedSequence.Count);
            foreach (var pair in s.LastProcessedSequence)
            {
                w.Write(pair.Key);
                w.Write(pair.Value);
            }
            w.Write(s.World.Length);
            w.Write(s.World);
            w.Write(s.Events.Length);
            w.Write(s.Events);
        }

        private static SnapshotMessage ReadSnapshot(BinaryReader r)
        {
            var s = new SnapshotMessage { Tick = r.ReadInt32() };
            for (int i = 0, n = r.ReadInt32(); i < n; i++) s.LastProcessedSequence[r.ReadInt32()] = r.ReadInt32();
            s.World = r.ReadBytes(r.ReadInt32());
            s.Events = r.ReadBytes(r.ReadInt32());
            return s;
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
