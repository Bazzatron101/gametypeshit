using System;
using System.IO;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// Where this copy of the game keeps its saves, and whether it keeps any.
    /// <para>
    /// Normally <c>saves/</c> under the game's persistent data folder. <c>-save-dir PATH</c> on the
    /// command line points a copy somewhere else (two copies on one PC, automated runs), and
    /// <c>-no-save</c> makes a copy play a new character and write nothing (bots). Tests set
    /// <see cref="Root"/> to a temporary folder so they never touch the developer's own save.
    /// </para>
    /// </summary>
    public static class GameSaves
    {
        private static string _root;

        public static string Root
        {
            get => _root ??= Argument("-save-dir") ?? Path.Combine(Application.persistentDataPath, "saves");
            set => _root = value;
        }

        /// <summary>True when this copy was started with <c>-no-save</c>.</summary>
        public static bool Disabled => Array.IndexOf(Environment.GetCommandLineArgs(), "-no-save") >= 0;

        /// <summary>Steam Cloud, once Steam is running (set by the online layer); null otherwise.</summary>
        public static ICloudStorage Cloud { get; set; }

        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
