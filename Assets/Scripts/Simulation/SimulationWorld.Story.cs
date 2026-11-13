using System.Collections.Generic;
using System.Linq;

namespace HunterSystem.Simulation
{
    /// <summary>What makes a story scene play (ticket 20). Each scene plays once per host world.</summary>
    public enum StoryTrigger : byte
    {
        /// <summary>The first time anyone is in the hub of this world.</summary>
        FirstHubArrival,

        /// <summary>The first clear of <see cref="StorySceneDefinition.GateId"/>, once the party is back in the hub.</summary>
        GateCleared,
    }

    /// <summary>
    /// When a text-only story scene plays. What it says lives with the presentation (the game's
    /// text scene assets); the simulation only decides the moment, so every machine agrees on it.
    /// </summary>
    public sealed class StorySceneDefinition
    {
        public string Id { get; set; }
        public StoryTrigger Trigger { get; set; }

        /// <summary>For <see cref="StoryTrigger.GateCleared"/>: which gate.</summary>
        public string GateId { get; set; }
    }

    /// <summary>
    /// Story scenes: the simulation watches for each scene's trigger and, the first time it is met
    /// in this world, announces the scene. Which scenes a world has seen is host-world progress
    /// (ADR 0005): the host loads it from its save, and snapshots carry it to guests, so a scene
    /// never plays twice, not even in a guest's prediction.
    /// </summary>
    public sealed partial class SimulationWorld
    {
        private readonly List<string> _seenStoryScenes = new List<string>();

        public IReadOnlyList<string> SeenStoryScenes => _seenStoryScenes;

        /// <summary>Host only: the scenes the host's world has already seen (from its save), replacing any here.</summary>
        public void SetSeenStoryScenes(IEnumerable<string> sceneIds)
        {
            _seenStoryScenes.Clear();
            foreach (var id in sceneIds ?? Enumerable.Empty<string>())
                if (id != null && !_seenStoryScenes.Contains(id)) _seenStoryScenes.Add(id);
        }

        /// <summary>At the end of each tick: any scene whose trigger has now been met, and that this world has not seen, starts.</summary>
        private void UpdateStory(int tick)
        {
            var scenes = _config.StoryScenes;
            if (scenes.Count == 0 || !_players.Any(p => !p.Away)) return; // an empty world tells no story
            foreach (var scene in scenes)
            {
                if (scene?.Id == null || _seenStoryScenes.Contains(scene.Id) || !Triggered(scene)) continue;
                _seenStoryScenes.Add(scene.Id);
                _events.Add(new StorySceneStarted(tick, scene.Id));
            }
        }

        private bool Triggered(StorySceneDefinition scene)
        {
            switch (scene.Trigger)
            {
                case StoryTrigger.FirstHubArrival:
                    return InHub;
                case StoryTrigger.GateCleared:
                    return InHub && scene.GateId != null && _clearedGates.Contains(scene.GateId); // told at home, not mid-gate
                default:
                    return false;
            }
        }
    }
}
