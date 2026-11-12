using System;
using System.IO;
using HunterSystem.Game;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Keeps tests away from the developer's own save: each test that loads a scene first points
    /// the game's saves at a new, empty temporary folder, so it starts with a new character.
    /// </summary>
    public static class TestSaves
    {
        public static string UseFreshFolder()
        {
            GameSaves.Root = Path.Combine(Path.GetTempPath(), "hunter-test-saves", Guid.NewGuid().ToString("N"));
            return GameSaves.Root;
        }
    }
}
