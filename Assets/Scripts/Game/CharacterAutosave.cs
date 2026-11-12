using System;
using System.Collections.Generic;
using HunterSystem.Simulation;

namespace HunterSystem.Game
{
    /// <summary>
    /// Decides when the local player's character is written to its save: soon after something
    /// worth keeping happens to it (XP, a level, a stat point, a skill or its place on the bar, a
    /// modifier), never more often than a set interval, and at once when asked (leaving a session,
    /// quitting). Switched off, it never writes: the test arena and bots use a character without
    /// keeping what happens to it.
    /// </summary>
    public sealed class CharacterAutosave
    {
        private readonly CharacterSaves _saves;
        private readonly float _minimumSecondsBetweenSaves;
        private bool _unsaved;
        private float _lastSaveAt = float.NegativeInfinity;

        public CharacterAutosave(CharacterSaves saves, float minimumSecondsBetweenSaves = 1f)
        {
            _saves = saves ?? throw new ArgumentNullException(nameof(saves));
            _minimumSecondsBetweenSaves = minimumSecondsBetweenSaves;
        }

        public bool Enabled { get; set; } = true;

        /// <summary>Looks through a tick's (or a frame's) events for progress made by <paramref name="me"/>.</summary>
        public void Observe(IReadOnlyList<SimulationEvent> events, PlayerId me)
        {
            if (_unsaved || events == null) return;
            foreach (var e in events)
            {
                if (!IsProgressFor(e, me)) continue;
                _unsaved = true;
                return;
            }
        }

        /// <summary>Saves if there is unsaved progress and the last save was long enough ago. True if it saved.</summary>
        public bool SaveIfDue(SimulationWorld world, PlayerId me, float now) => SaveIfDue(() => Capture(world, me), now);

        /// <param name="capture">The character as it should be saved now; may return null when there is none to save.</param>
        public bool SaveIfDue(Func<CharacterRecord> capture, float now)
        {
            if (!Enabled || !_unsaved || now - _lastSaveAt < _minimumSecondsBetweenSaves) return false;
            _lastSaveAt = now;
            return Write(capture());
        }

        /// <summary>Saves the character as it stands, whether or not anything was observed (leaving, quitting).</summary>
        public void SaveNow(SimulationWorld world, PlayerId me) => SaveNow(Capture(world, me));

        public void SaveNow(CharacterRecord character)
        {
            if (Enabled) Write(character);
        }

        private static CharacterRecord Capture(SimulationWorld world, PlayerId me) =>
            world != null && world.HasPlayer(me) ? world.CaptureCharacter(me) : null;

        private bool Write(CharacterRecord character)
        {
            if (character == null) return false; // stays unsaved; tried again later
            _saves.Save(character);
            _unsaved = false;
            return true;
        }

        private static bool IsProgressFor(SimulationEvent e, PlayerId me) => e switch
        {
            XpGained x => x.Player == me,
            PlayerLeveledUp l => l.Player == me,
            StatPointAllocated s => s.Player == me,
            SkillUnlocked s => s.Player == me,
            SkillSlotChanged s => s.Player == me,
            ModifierGranted m => m.Player == me,
            ModifierRevoked m => m.Player == me,
            _ => false,
        };
    }
}
