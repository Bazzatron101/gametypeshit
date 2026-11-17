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
        /// Most tests are not about making a hunter, so they get a Warrior named Tester and play at once.
        /// </param>
        /// <param name="name">The new character's name; null (with no class) for a hunter not made at all, so the creator asks first.</param>
        /// <param name="speciesId">The new hunter's species (ticket 81); null for one with none yet (the game then asks for one).</param>
        public static string UseFreshFolder(string classId = "warrior", string name = "Tester", string speciesId = "kangaroo")
        {
            GameSaves.Root = Path.Combine(Path.GetTempPath(), "hunter-test-saves", Guid.NewGuid().ToString("N"));
            if (classId != null || name != null)
                new CharacterSaves(GameSaves.Root).Save(new CharacterRecord { IsNew = true, ClassId = classId, Name = name, SpeciesId = speciesId });
            return GameSaves.Root;
        }
    }
}
