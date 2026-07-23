using System;

namespace ProjectHive.Core.Flow
{
    [Serializable]
    public readonly struct GameStateTransition
    {
        public GameStateTransition(
            GameState previous,
            GameState current,
            string reason,
            float occurredAt)
        {
            Previous = previous;
            Current = current;
            Reason = reason ?? string.Empty;
            OccurredAt = occurredAt;
        }

        public GameState Previous { get; }
        public GameState Current { get; }
        public string Reason { get; }
        public float OccurredAt { get; }
    }
}
