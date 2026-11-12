using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HunterSystem.Simulation;

namespace HunterSystem.Game
{
    /// <summary>Somewhere off this PC to keep a copy of a save: Steam Cloud in the game, a fake in tests.</summary>
    public interface ICloudStorage
    {
        /// <summary>False when there is no cloud to talk to (Steam not running, cloud disabled for the account).</summary>
        bool IsAvailable { get; }

        void Write(string name, byte[] data);

        /// <summary>The file's bytes, or null if the cloud has no such file.</summary>
        byte[] Read(string name);
    }

    /// <summary>
    /// One record kept in one file. A save is written beside the file, flushed to disk and swapped
    /// in whole, so a crash mid-save cannot leave half a file; the save it replaces is kept as
    /// <c>.bak</c>.
    /// <para>
    /// A file whose contents cannot be understood is moved aside (never overwritten) and the backup
    /// is tried. A file that cannot even be opened (locked by another program or a second copy of
    /// the game), or cannot be moved aside, is left exactly as it is and saving is switched off
    /// (<see cref="SavingBlocked"/>) until the next load: better to lose this session's progress
    /// than a save that is probably fine.
    /// </para>
    /// </summary>
    internal sealed class RecordFile<T> where T : class
    {
        private readonly Func<T, byte[]> _encode;
        private readonly Func<byte[], T> _decode;
        private readonly string _recordName;

        /// <param name="recordName">What the file holds, for messages: "character", "world".</param>
        public RecordFile(string path, string recordName, Func<T, byte[]> encode, Func<byte[], T> decode)
        {
            Path = path;
            _recordName = recordName;
            _encode = encode;
            _decode = decode;
        }

        public string Path { get; }
        public string BackupPath => Path + ".bak";

        /// <summary>True when the last load found a save it could neither read nor safely move aside. Saves then do nothing.</summary>
        public bool SavingBlocked { get; private set; }

        internal static bool IsFileError(Exception e) => e is IOException || e is UnauthorizedAccessException;

        /// <summary>The record, or null when there is no save. <paramref name="problem"/> says what went wrong, if anything did.</summary>
        public T Load(out string problem)
        {
            problem = null;
            SavingBlocked = false;
            if (!File.Exists(Path)) return File.Exists(BackupPath) ? LoadBackup(ref problem, $"The {_recordName} save was missing.") : null;

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(Path);
            }
            catch (Exception e) when (IsFileError(e))
            {
                SavingBlocked = true;
                problem = $"The {_recordName} save could not be opened ({e.Message}). It has been left alone, and nothing will be saved until the game is restarted.";
                return null;
            }

            try
            {
                return _decode(bytes);
            }
            catch (ArgumentException e)
            {
                string aside;
                try
                {
                    aside = MoveAside("unreadable"); // out of the way of later saves, and of the backup they would replace
                }
                catch (Exception moveError) when (IsFileError(moveError))
                {
                    SavingBlocked = true;
                    problem = $"The {_recordName} save could not be read ({e.Message}) or moved aside. It has been left alone, and nothing will be saved until the game is restarted.";
                    return null;
                }
                return LoadBackup(ref problem, $"The {_recordName} save could not be read (kept as {System.IO.Path.GetFileName(aside)}): {e.Message}");
            }
        }

        private T LoadBackup(ref string problem, string reason)
        {
            try
            {
                if (File.Exists(BackupPath))
                {
                    var record = _decode(File.ReadAllBytes(BackupPath));
                    problem = reason + " Loaded the previous save from the backup.";
                    return record;
                }
            }
            catch (Exception e) when (e is ArgumentException || IsFileError(e))
            {
                reason += " The backup could not be read either.";
            }
            problem = reason + " Starting afresh.";
            return null;
        }

        /// <summary>Moves the file to a new name beside it (<c>.label-time</c>, numbered if that is taken) and returns the name.</summary>
        private string MoveAside(string label)
        {
            var stem = $"{Path}.{label}-{DateTime.Now:yyyyMMdd-HHmmss}";
            var aside = stem;
            for (var n = 2; File.Exists(aside); n++) aside = $"{stem}-{n}";
            File.Move(Path, aside);
            return aside;
        }

        /// <summary>Keeps a lasting copy of the current file beside it; returns its name, or null if there is no file.</summary>
        public string KeepCopy(string label)
        {
            if (!File.Exists(Path)) return null;
            var stem = $"{Path}.{label}-{DateTime.Now:yyyyMMdd-HHmmss}";
            var copy = stem;
            for (var n = 2; File.Exists(copy); n++) copy = $"{stem}-{n}";
            File.Copy(Path, copy);
            return copy;
        }

        /// <summary>The saved bytes, or null if saving is blocked.</summary>
        public byte[] Save(T record)
        {
            if (SavingBlocked) return null;
            var bytes = _encode(record);
            WriteBytes(bytes);
            return bytes;
        }

        /// <summary>Replaces the file with <paramref name="bytes"/> in one step, keeping what was there as the backup.</summary>
        public void WriteBytes(byte[] bytes)
        {
            if (SavingBlocked) return;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            var temporary = Path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true); // on the disk before it takes the real file's place
            }
            if (File.Exists(Path)) File.Replace(temporary, Path, BackupPath);
            else File.Move(temporary, Path);
        }

        public byte[] ReadBytesOrNull() => File.Exists(Path) ? File.ReadAllBytes(Path) : null;
    }

    /// <summary>
    /// The player's character on this PC (one for now; slots arrive with the character creator),
    /// with an optional copy in Steam Cloud. The cloud copy is a backup the player restores on
    /// request. Neither side ever silently replaces the other: restoring is explicit, and
    /// switching backups on does not overwrite a different character already in the cloud.
    /// </summary>
    public sealed class CharacterSaves
    {
        public const string CloudFileName = "hunter.hsc";

        private readonly RecordFile<CharacterRecord> _file;
        private readonly string _settingsPath;
        private bool _cloudBackup;

        /// <param name="root">The game's save folder; characters go in <c>characters/</c> under it.</param>
        public CharacterSaves(string root, ICloudStorage cloud = null)
        {
            _file = new RecordFile<CharacterRecord>(Path.Combine(root, "characters", "hunter.hsc"), "character", CharacterCodec.Encode, CharacterCodec.Decode);
            Cloud = cloud;
            _settingsPath = Path.Combine(root, "save-settings.txt");
            try
            {
                _cloudBackup = File.Exists(_settingsPath) && File.ReadAllText(_settingsPath).Trim() == CloudBackupOn;
            }
            catch (Exception e) when (RecordFile<CharacterRecord>.IsFileError(e))
            {
                _cloudBackup = false; // an unreadable setting means the safe default: nothing leaves the PC
            }
        }

        private const string CloudBackupOn = "cloudBackup=1";
        private const string CloudBackupOff = "cloudBackup=0";

        public string FilePath => _file.Path;

        /// <summary>Where backups go. Settable because Steam may come up after the save was loaded.</summary>
        public ICloudStorage Cloud { get; set; }

        /// <summary>What went wrong in the most recent load, save, restore or setting change; null if nothing did.</summary>
        public string LastProblem { get; private set; }

        /// <summary>What went wrong when the character was loaded; stays until the next load.</summary>
        public string LastProblemOnLoad { get; private set; }

        /// <summary>True when the save on disk could not be read safely, so nothing is being saved this session.</summary>
        public bool SavingBlocked => _file.SavingBlocked;

        public bool CloudAvailable => Cloud != null && Cloud.IsAvailable;

        /// <summary>Whether each save is also copied to Steam Cloud (while Steam is running). Off until the player turns it on; remembered.</summary>
        public bool CloudBackup => _cloudBackup;

        /// <summary>
        /// Switches Steam Cloud backup on or off. Switching on backs up the current save straight
        /// away, unless the cloud already holds a different character (say, from another PC): then
        /// nothing changes and it returns false, and the player must choose between
        /// <see cref="RestoreFromCloud"/> and <see cref="ReplaceCloudBackup"/>.
        /// </summary>
        public bool SetCloudBackup(bool on)
        {
            LastProblem = null;
            if (on == _cloudBackup) return true;
            if (!on) return Remember(false);

            var local = _file.ReadBytesOrNull();
            var inCloud = ReadCloud(out var cloudProblem);
            if (cloudProblem != null)
            {
                LastProblem = cloudProblem;
                return false;
            }
            if (inCloud != null && (local == null || !Same(local, inCloud)))
            {
                var level = PeekCloud()?.Level;
                LastProblem = "Steam Cloud already holds a different hunter" + (level.HasValue ? $" (level {level})" : "") +
                              ". Restore it to this PC, or replace it with this PC's save.";
                return false;
            }
            if (!Remember(true)) return false;
            if (local != null && inCloud == null) BackUp(local);
            return true;
        }

        /// <summary>Switches backup on and overwrites whatever the cloud holds with this PC's save. The player's explicit choice.</summary>
        public bool ReplaceCloudBackup()
        {
            LastProblem = null;
            if (!Remember(true)) return false;
            var local = _file.ReadBytesOrNull();
            if (local != null) BackUp(local);
            return LastProblem == null;
        }

        private bool Remember(bool on)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath));
                File.WriteAllText(_settingsPath, on ? CloudBackupOn : CloudBackupOff);
                _cloudBackup = on;
                return true;
            }
            catch (Exception e) when (RecordFile<CharacterRecord>.IsFileError(e))
            {
                LastProblem = "The setting could not be saved: " + e.Message;
                return false;
            }
        }

        private static bool Same(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>The saved character, or a new one when there is no save (or none that can be read: see <see cref="LastProblemOnLoad"/>).</summary>
        public CharacterRecord LoadOrNew()
        {
            var record = _file.Load(out var problem);
            LastProblem = LastProblemOnLoad = problem;
            return record ?? CharacterRecord.New();
        }

        public void Save(CharacterRecord character)
        {
            LastProblem = null;
            var bytes = _file.Save(character);
            if (bytes == null)
            {
                LastProblem = LastProblemOnLoad; // saving is blocked; say why
                return;
            }
            if (_cloudBackup) BackUp(bytes);
        }

        private void BackUp(byte[] bytes)
        {
            if (!CloudAvailable) return; // no Steam this session: nothing to report on every save
            try
            {
                Cloud.Write(CloudFileName, bytes);
            }
            catch (Exception e)
            {
                LastProblem = "Saved on this PC, but the Steam Cloud backup failed: " + e.Message;
            }
        }

        private byte[] ReadCloud(out string problem)
        {
            problem = null;
            if (!CloudAvailable)
            {
                problem = "Steam Cloud is not available.";
                return null;
            }
            try
            {
                return Cloud.Read(CloudFileName);
            }
            catch (Exception e)
            {
                problem = "Steam Cloud could not be reached: " + e.Message;
                return null;
            }
        }

        public bool CloudHasBackup => ReadCloud(out _) != null;

        /// <summary>The character in Steam Cloud without restoring it (to show the player what they would get); null if none or unreadable.</summary>
        public CharacterRecord PeekCloud()
        {
            var bytes = ReadCloud(out _);
            if (bytes == null) return null;
            try
            {
                return CharacterCodec.Decode(bytes);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Replaces the local save with the Steam Cloud copy and returns it. The save it replaces
        /// is kept twice: as the usual backup, and as a lasting <c>.before-restore</c> copy that
        /// later saves do not touch. Returns null, changing nothing, when the cloud has no usable
        /// copy (<see cref="LastProblem"/> says why).
        /// </summary>
        public CharacterRecord RestoreFromCloud()
        {
            LastProblem = null;
            var bytes = ReadCloud(out var problem);
            if (problem != null)
            {
                LastProblem = problem;
                return null;
            }
            if (bytes == null)
            {
                LastProblem = "There is no backup in Steam Cloud.";
                return null;
            }
            try
            {
                var record = CharacterCodec.Decode(bytes);
                _file.KeepCopy("before-restore");
                _file.WriteBytes(bytes);
                return record;
            }
            catch (ArgumentException e)
            {
                LastProblem = "The Steam Cloud backup could not be read: " + e.Message;
                return null;
            }
            catch (Exception e) when (RecordFile<CharacterRecord>.IsFileError(e))
            {
                LastProblem = "The restored save could not be written: " + e.Message;
                return null;
            }
        }
    }

    /// <summary>
    /// Progress that belongs to a host's world, not to any character: which gates are cleared, and
    /// later story and map progress (spec: "Story and map progress live in the host's world").
    /// Nothing clears gates yet; the gate runtime (ticket 17) will fill this in.
    /// </summary>
    public sealed class WorldRecord
    {
        public List<string> ClearedGates = new List<string>();
    }

    /// <summary>The host's world on this PC, in its own folder, apart from characters.</summary>
    public sealed class WorldSaves
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("HSWD");
        private const byte Version = 1;

        private readonly RecordFile<WorldRecord> _file;

        /// <param name="root">The game's save folder; the world goes in <c>worlds/</c> under it.</param>
        public WorldSaves(string root)
        {
            _file = new RecordFile<WorldRecord>(Path.Combine(root, "worlds", "world.hsw"), "world", Encode, Decode);
        }

        public string FilePath => _file.Path;

        public string LastProblem { get; private set; }

        public WorldRecord LoadOrNew()
        {
            var record = _file.Load(out var problem);
            LastProblem = problem;
            return record ?? new WorldRecord();
        }

        public void Save(WorldRecord world)
        {
            if (_file.Save(world) != null) LastProblem = null;
        }

        private static byte[] Encode(WorldRecord world)
        {
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream);
            w.Write(Magic);
            w.Write(Version);
            w.Write(world.ClearedGates.Count);
            foreach (var gate in world.ClearedGates) w.Write(gate);
            w.Flush();
            return stream.ToArray();
        }

        private static WorldRecord Decode(byte[] bytes)
        {
            try
            {
                using var r = new BinaryReader(new MemoryStream(bytes));
                var magic = r.ReadBytes(Magic.Length);
                if (magic.Length != Magic.Length) throw new FormatException("Not a world save.");
                for (var i = 0; i < Magic.Length; i++) if (magic[i] != Magic[i]) throw new FormatException("Not a world save.");
                var version = r.ReadByte();
                if (version > Version) throw new ArgumentException($"This world was saved by a newer version of the game (format {version}).");
                var world = new WorldRecord();
                var count = r.ReadInt32();
                if (count < 0 || count > bytes.Length) throw new FormatException("Impossible collection size.");
                for (var i = 0; i < count; i++) world.ClearedGates.Add(r.ReadString());
                return world;
            }
            catch (Exception e) when (e is EndOfStreamException || e is IOException || e is FormatException)
            {
                throw new ArgumentException("Not a readable world save.", nameof(bytes), e);
            }
        }
    }
}
