using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HunterSystem.Simulation;

namespace HunterSystem.Netcode
{
    /// <summary>
    /// What a bot teammate has been through, which is how it gets better (ADR 0014). Every fight,
    /// boss, clear and fall adds to its experience, and its <see cref="Proficiency"/> grows from that:
    /// quickly at first, then levelling off. Kept with the host's world, so it carries over between sessions.
    /// </summary>
    public sealed class CompanionExperience
    {
        /// <summary>A brand-new teammate's proficiency: it plays its role, but slowly and sloppily.</summary>
        public const float Rookie = 0.2f;

        /// <summary>Experience points at which a teammate has covered about 63% of the way from rookie to perfect.</summary>
        public const float LearningScale = 250f;

        public int EnemiesDefeated;
        public int BossesDefeated;
        public int GatesCleared;
        public int TimesDowned;
        public int AttacksDodged;
        public int SecondsInCombat;

        /// <summary>
        /// Points of experience: a kill is worth 1, a dodge ½, half a minute of fighting 1, a fall 4
        /// (it learns from those too), a boss 20 and a clear 30.
        /// </summary>
        public float Points =>
            EnemiesDefeated + AttacksDodged * 0.5f + SecondsInCombat / 30f + TimesDowned * 4f + BossesDefeated * 20f + GatesCleared * 30f;

        /// <summary>
        /// How well it plays, from <see cref="Rookie"/> up towards 1. Roughly: about 0.45 after the first
        /// gate, 0.75 after four, 0.9 after eight.
        /// </summary>
        public float Proficiency => ProficiencyFor(Points);

        public static float ProficiencyFor(float points) =>
            Rookie + (1f - Rookie) * (1f - MathF.Exp(-Math.Max(0f, points) / LearningScale));

        public static string Rank(float proficiency) =>
            proficiency < 0.4f ? "Rookie" : proficiency < 0.6f ? "Seasoned" : proficiency < 0.8f ? "Veteran" : "Elite";

        public CompanionExperience Clone() => (CompanionExperience)MemberwiseClone();
    }

    /// <summary>One bot teammate: a name, its character (class, level, stats, skills) and its experience.</summary>
    public sealed class CompanionRecord
    {
        public string Name = "";
        public CharacterRecord Character = CharacterRecord.New();
        public CompanionExperience Experience = new CompanionExperience();

        public string ClassId => Character?.ClassId;
        public CompanionRole Role => CompanionRoles.ForClass(ClassId);
    }

    /// <summary>The host's bot teammates as bytes, for the save file. Each character uses the character format.</summary>
    public static class CompanionCodec
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("HSCP");

        /// <summary>1: names, characters and experience.</summary>
        private const byte Version = 1;

        public static byte[] Encode(IReadOnlyList<CompanionRecord> companions)
        {
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            w.Write(Magic);
            w.Write(Version);
            w.Write(companions.Count);
            foreach (var c in companions)
            {
                w.Write(c.Name ?? "");
                var character = CharacterCodec.Encode(c.Character ?? CharacterRecord.New());
                w.Write(character.Length);
                w.Write(character);
                var x = c.Experience ?? new CompanionExperience();
                w.Write(x.EnemiesDefeated);
                w.Write(x.BossesDefeated);
                w.Write(x.GatesCleared);
                w.Write(x.TimesDowned);
                w.Write(x.AttacksDodged);
                w.Write(x.SecondsInCombat);
            }
            w.Flush();
            return stream.ToArray();
        }

        public static List<CompanionRecord> Decode(byte[] bytes)
        {
            try
            {
                using var r = new BinaryReader(new MemoryStream(bytes));
                var magic = r.ReadBytes(Magic.Length);
                if (magic.Length != Magic.Length) throw new FormatException("Not a teammates save.");
                for (var i = 0; i < Magic.Length; i++) if (magic[i] != Magic[i]) throw new FormatException("Not a teammates save.");
                var version = r.ReadByte();
                if (version > Version) throw new ArgumentException($"These teammates were saved by a newer version of the game (format {version}).");
                int Count()
                {
                    var n = r.ReadInt32();
                    if (n < 0 || n > bytes.Length) throw new FormatException("Impossible size.");
                    return n;
                }

                var list = new List<CompanionRecord>();
                for (int i = 0, n = Count(); i < n; i++)
                {
                    var record = new CompanionRecord { Name = r.ReadString() };
                    record.Character = CharacterCodec.Decode(r.ReadBytes(Count()));
                    record.Experience = new CompanionExperience
                    {
                        EnemiesDefeated = Math.Max(0, r.ReadInt32()),
                        BossesDefeated = Math.Max(0, r.ReadInt32()),
                        GatesCleared = Math.Max(0, r.ReadInt32()),
                        TimesDowned = Math.Max(0, r.ReadInt32()),
                        AttacksDodged = Math.Max(0, r.ReadInt32()),
                        SecondsInCombat = Math.Max(0, r.ReadInt32()),
                    };
                    list.Add(record);
                }
                return list;
            }
            catch (Exception e) when (e is EndOfStreamException || e is IOException || e is FormatException)
            {
                throw new ArgumentException("Not a readable teammates save.", nameof(bytes), e);
            }
        }
    }
}
