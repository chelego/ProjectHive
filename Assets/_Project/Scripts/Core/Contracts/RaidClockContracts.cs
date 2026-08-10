using System;
using UnityEngine;

namespace ProjectHive.Core.Contracts
{
    [Serializable]
    public readonly struct RaidClockSnapshot
    {
        public RaidClockSnapshot(
            float elapsedSeconds,
            float remainingSeconds,
            int worldHour,
            int worldMinute)
        {
            ElapsedSeconds = Mathf.Max(0f, elapsedSeconds);
            RemainingSeconds = Mathf.Max(0f, remainingSeconds);
            WorldHour = Mathf.Abs(worldHour) % 24;
            WorldMinute = Mathf.Abs(worldMinute) % 60;
        }

        public float ElapsedSeconds { get; }
        public float RemainingSeconds { get; }
        public int WorldHour { get; }
        public int WorldMinute { get; }
        public bool IsExpired => RemainingSeconds <= 0f;
    }

    public interface IRaidClock
    {
        RaidClockSnapshot ClockState { get; }
        event Action<RaidClockSnapshot> ClockChanged;
    }
}
