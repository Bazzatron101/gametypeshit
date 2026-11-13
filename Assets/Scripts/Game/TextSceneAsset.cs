using System;
using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;

namespace HunterSystem.Game
{
    /// <summary>
    /// A text-only story scene (ticket 20): who speaks and what they say, line by line, and what
    /// makes it play. It is played in-engine as text, with no cutscene or animation (spec: "Story
    /// is told through text-only in-engine scenes"). The words are the developer's to write.
    /// </summary>
    [CreateAssetMenu(menuName = "Hunter System/Text Scene", fileName = "NewScene")]
    public sealed class TextSceneAsset : ScriptableObject
    {
        /// <summary>Text that still holds this marker has not been written yet.</summary>
        public const string Unwritten = "write me";

        [Serializable]
        public struct Line
        {
            [Tooltip("Who speaks: an NPC's name, \"System\", or empty for narration")] public string speaker;
            [TextArea(2, 6)] public string text;
        }

        [SerializeField, Tooltip("Stable id: the host's world remembers by it that the scene has played. Never change it once players have it.")] private string id = "new-scene";
        [SerializeField] private StoryTrigger trigger = StoryTrigger.FirstHubArrival;
        [SerializeField, Tooltip("For a gate-clear trigger: which gate")] private GateDefinitionAsset gate;
        [SerializeField] private Line[] lines = new Line[0];

        public string Id => id;
        public StoryTrigger Trigger => trigger;
        public GateDefinitionAsset Gate => gate;
        public IReadOnlyList<Line> Lines => lines;

        public StorySceneDefinition ToDefinition() => new StorySceneDefinition { Id = id, Trigger = trigger, GateId = gate != null ? gate.Id : null };

        /// <summary>For editor tooling that creates scenes in code.</summary>
        public void Set(string sceneId, StoryTrigger sceneTrigger, Line[] sceneLines, GateDefinitionAsset forGate = null)
        {
            id = sceneId;
            trigger = sceneTrigger;
            lines = sceneLines ?? new Line[0];
            gate = forGate;
        }
    }
}
