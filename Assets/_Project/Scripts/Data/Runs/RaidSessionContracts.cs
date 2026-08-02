using System;

namespace ProjectHive.Data.Runs
{
    public enum RaidPhase
    {
        Preparing = 0,
        Active = 1,
        Resolving = 2,
        Completed = 3
    }

    [Serializable]
    public readonly struct RaidSessionSnapshot
    {
        public RaidSessionSnapshot(
            string raidId,
            string mapId,
            RaidPhase phase,
            RunOutcome outcome)
        {
            RaidId = raidId ?? string.Empty;
            MapId = mapId ?? string.Empty;
            Phase = phase;
            Outcome = outcome;
        }

        public string RaidId { get; }
        public string MapId { get; }
        public RaidPhase Phase { get; }
        public RunOutcome Outcome { get; }
        public bool IsActive => Phase == RaidPhase.Active || Phase == RaidPhase.Resolving;
        public bool IsResolved => Phase == RaidPhase.Completed && Outcome != RunOutcome.None;
    }

    public interface IRaidSessionState
    {
        RaidSessionSnapshot SessionState { get; }
        event Action<RaidSessionSnapshot> SessionChanged;
    }

    public enum RaidEndTrigger
    {
        Extraction = 0,
        PlayerDeath = 1,
        TimeExpired = 2,
        ApplicationExit = 3
    }

    [Serializable]
    public readonly struct RaidEndRequest
    {
        public RaidEndRequest(RaidEndTrigger trigger, string sourceId)
        {
            Trigger = trigger;
            SourceId = sourceId ?? string.Empty;
        }

        public RaidEndTrigger Trigger { get; }
        public string SourceId { get; }
    }

    public enum RaidResolutionResult
    {
        Accepted = 0,
        AlreadyResolved = 1,
        NoActiveRaid = 2,
        InvalidRequest = 3
    }

    public interface IRaidResolutionService
    {
        RaidResolutionResult TryResolve(
            in RaidEndRequest request,
            out RunResult result);
    }
}
