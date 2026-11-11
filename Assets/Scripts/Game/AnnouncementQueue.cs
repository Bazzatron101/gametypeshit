using System.Collections.Generic;

namespace HunterSystem.Game
{
    /// <summary>
    /// System announcements shown one at a time, in arrival order, each for a fixed time with a
    /// fade at the end. A level-up and a skill unlock on the same tick play back to back instead of
    /// overwriting each other. Pure logic: callers pass the time.
    /// </summary>
    public sealed class AnnouncementQueue
    {
        public readonly struct Announcement
        {
            public readonly string Title;
            public readonly string Body;

            /// <summary>1 while fully shown, falling to 0 over the fade.</summary>
            public readonly float Alpha;

            public Announcement(string title, string body, float alpha)
            {
                Title = title;
                Body = body;
                Alpha = alpha;
            }
        }

        private readonly Queue<(string title, string body)> _waiting = new Queue<(string, string)>();
        private readonly float _showSeconds;
        private readonly float _fadeSeconds;
        private (string title, string body)? _current;
        private float _currentStartedAt;

        public AnnouncementQueue(float showSeconds = 3.5f, float fadeSeconds = 0.6f)
        {
            _showSeconds = showSeconds;
            _fadeSeconds = fadeSeconds;
        }

        public void Enqueue(string title, string body, float now)
        {
            _waiting.Enqueue((title, body));
            if (_current == null) Advance(now);
        }

        /// <summary>The announcement to draw at <paramref name="now"/>, or null when there is none.</summary>
        public Announcement? Current(float now)
        {
            while (_current != null && now - _currentStartedAt >= _showSeconds)
                Advance(_currentStartedAt + _showSeconds);
            if (_current == null) return null;

            var remaining = _showSeconds - (now - _currentStartedAt);
            var alpha = _fadeSeconds > 0f && remaining < _fadeSeconds ? remaining / _fadeSeconds : 1f;
            return new Announcement(_current.Value.title, _current.Value.body, alpha);
        }

        private void Advance(float startTime)
        {
            _current = _waiting.Count > 0 ? _waiting.Dequeue() : ((string, string)?)null;
            _currentStartedAt = startTime;
        }
    }
}
