namespace ProjectHive.Core.Contracts
{
    public enum ExtractionGateState
    {
        Locked = 0,
        Available = 1,
        Opening = 2,
        Open = 3
    }

    public interface IExtractionGate
    {
        string GateId { get; }
        bool IsEntryOnly { get; }
        ExtractionGateState State { get; }
        float OpeningDurationSeconds { get; }
        bool TryBeginOpening(in InteractionContext context);
    }
}
