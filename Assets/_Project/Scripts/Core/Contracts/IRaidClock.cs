using System;

namespace ProjectHive.Core.Contracts
{
    public interface IRaidClock
    {
        float ElapsedSeconds { get; }
        TimeSpan RemainingTime { get; }
        bool IsExpired { get; }
        event Action OnTimeExpired;
        event Action<float> OnTimeUpdated;
    }
}
