using System.IO;
using HunterSystem.Game;
using Steamworks;

namespace HunterSystem.Online
{
    /// <summary>
    /// Steam Cloud as a place for save backups (Steam's remote storage for this app and account).
    /// Available only while Steam is running and the player has cloud enabled for the game.
    /// </summary>
    public sealed class SteamCloudStorage : ICloudStorage
    {
        public bool IsAvailable => SteamClient.IsValid && SteamRemoteStorage.IsCloudEnabled;

        public void Write(string name, byte[] data)
        {
            if (!SteamRemoteStorage.FileWrite(name, data)) throw new IOException("Steam did not accept the file (is the cloud quota full?).");
        }

        public byte[] Read(string name) => SteamRemoteStorage.FileExists(name) ? SteamRemoteStorage.FileRead(name) : null;
    }
}
