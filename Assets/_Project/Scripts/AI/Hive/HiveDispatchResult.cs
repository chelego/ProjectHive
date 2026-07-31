using System;

namespace ProjectHive.AI.Hive
{
    [Serializable]
    public readonly struct HiveDispatchResult
    {
        public HiveDispatchResult(
            int commandSequence,
            int candidatesConsidered,
            int assignmentsAccepted,
            int assignmentsRejected)
        {
            CommandSequence = commandSequence;
            CandidatesConsidered = candidatesConsidered;
            AssignmentsAccepted = assignmentsAccepted;
            AssignmentsRejected = assignmentsRejected;
        }

        public int CommandSequence { get; }
        public int CandidatesConsidered { get; }
        public int AssignmentsAccepted { get; }
        public int AssignmentsRejected { get; }
    }
}
