using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HunterSystem.Simulation
{
    /// <summary>
    /// What a player keeps between sessions: level, XP, stats, known skills, the skill bar and
    /// modifiers. It lives in the player's own save and goes with them into a host's session
    /// (spec: "Characters live in each player's own save"). Position, health and cooldowns are
    /// not part of it: a character arrives fresh wherever it is brought in.
    /// <para>
    /// Skills are named by id and looked up in the game's content when the character is brought in.
    /// Modifiers are kept in full, as in snapshots. There is no gear yet; when items exist they
    /// join this record under a new <see cref="CharacterCodec"/> version.
    /// </para>
    /// </summary>
    public sealed class CharacterRecord
    {
        public int Level = 1;
        public int Xp;
        public int UnspentStatPoints;

        /// <summary>One value per <see cref="Stat"/>, or null for the game's starting values.</summary>
        public int[] Stats;

        public List<string> KnownSkills = new List<string>();

        /// <summary>Skill id in each bar slot; null for an empty slot.</summary>
        public string[] SkillBar = new string[SimulationWorld.SkillSlotCount];

        public List<ModifierDefinition> Modifiers = new List<ModifierDefinition>();

        /// <summary>
        /// True for a character that has never been played. Bringing one in gives exactly what a
        /// player with no save gets: level 1, starting stats, the starter skills on the bar.
        /// </summary>
        public bool IsNew;

        public static CharacterRecord New() => new CharacterRecord { IsNew = true };

        /// <summary>
        /// True when this character has more progress than <paramref name="other"/>: a higher level,
        /// or the same level with more XP. Used when two copies of one character meet (a returning
        /// guest's own save and the one the session kept) to keep the one that is ahead.
        /// </summary>
        public bool IsFurtherAlongThan(CharacterRecord other)
        {
            if (other == null || other.IsNew) return !IsNew;
            if (IsNew) return false;
            return Level != other.Level ? Level > other.Level : Xp > other.Xp;
        }
    }

    /// <summary>
    /// Characters as bytes, for save files and for sending a guest's character to a host. The
    /// format carries a version; bytes from a newer version of the game, or bytes that cannot be
    /// read, are rejected with an <see cref="ArgumentException"/> that says which.
    /// </summary>
    public static class CharacterCodec
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("HSCH");

        /// <summary>Version 1: level, XP, unspent points, stats, known skills, bar, modifiers.</summary>
        public const byte Version = 1;

        public static byte[] Encode(CharacterRecord c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            w.Write(Magic);
            w.Write(Version);
            w.Write(c.IsNew);
            w.Write(c.Level);
            w.Write(c.Xp);
            w.Write(c.UnspentStatPoints);
            var stats = c.Stats ?? Array.Empty<int>();
            w.Write(stats.Length);
            foreach (var value in stats) w.Write(value);
            w.Write(c.KnownSkills.Count);
            foreach (var id in c.KnownSkills) w.Write(id);
            w.Write(c.SkillBar.Length);
            foreach (var id in c.SkillBar) SimulationWorld.WriteNullable(w, id);
            w.Write(c.Modifiers.Count);
            foreach (var m in c.Modifiers) SimulationWorld.WriteModifier(w, m);
            w.Flush();
            return stream.ToArray();
        }

        public static CharacterRecord Decode(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            try
            {
                using var r = new BinaryReader(new MemoryStream(bytes));
                var magic = r.ReadBytes(Magic.Length);
                for (var i = 0; i < Magic.Length; i++)
                    if (magic.Length != Magic.Length || magic[i] != Magic[i]) throw new FormatException("Not a character.");
                var version = r.ReadByte();
                if (version > Version)
                    throw new ArgumentException($"This character was saved by a newer version of the game (format {version}; this game reads up to {Version}).", nameof(bytes));
                if (version < 1) throw new FormatException("Unknown character format.");

                var c = new CharacterRecord
                {
                    IsNew = r.ReadBoolean(),
                    Level = r.ReadInt32(),
                    Xp = r.ReadInt32(),
                    UnspentStatPoints = r.ReadInt32(),
                };
                var stats = new int[Count(r, bytes)];
                for (var i = 0; i < stats.Length; i++) stats[i] = r.ReadInt32();
                c.Stats = stats.Length > 0 ? stats : null;
                for (int i = 0, n = Count(r, bytes); i < n; i++) c.KnownSkills.Add(r.ReadString());
                c.SkillBar = new string[Count(r, bytes)];
                for (var i = 0; i < c.SkillBar.Length; i++) c.SkillBar[i] = SimulationWorld.ReadNullable(r);
                for (int i = 0, n = Count(r, bytes); i < n; i++) c.Modifiers.Add(SimulationWorld.ReadModifier(r));
                return c;
            }
            catch (Exception e) when (e is EndOfStreamException || e is IOException || e is FormatException)
            {
                throw new ArgumentException("Not a readable character.", nameof(bytes), e);
            }
        }

        /// <summary>A collection size, checked so damaged bytes cannot ask for a huge allocation.</summary>
        private static int Count(BinaryReader r, byte[] bytes)
        {
            var n = r.ReadInt32();
            if (n < 0 || n > bytes.Length) throw new FormatException("Impossible collection size.");
            return n;
        }
    }
}
