using System;
using System.IO;
using HunterSystem.Game;
using HunterSystem.Simulation;

namespace HunterSystem.Tests
{
    /// <summary>
    /// Keeps tests away from the developer's own save: each test that loads a scene first points
    /// the game's saves at a new, empty temporary folder, so it starts with a new character.
    /// </summary>
    public static class TestSaves
    {
        /// <param name="classId">
        /// The new character's class; null for one with none yet (the game then asks for one).
        /// Most tests are not about classes, so they get a Warrior and play at once.
        /// </param>
        public static string UseFreshFolder(string classId = "warrior")
        {
            GameSaves.Root = Path.Combine(Path.GetTempPath(), "hunter-test-saves", Guid.NewGuid().ToString("N"));
            if (classId != null) new CharacterSaves(GameSaves.Root).Save(new CharacterRecord { IsNew = true, ClassId = classId });
            return GameSaves.Root;
        }
    }
}
