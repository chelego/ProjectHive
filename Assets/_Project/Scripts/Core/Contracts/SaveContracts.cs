using System;

namespace ProjectHive.Core.Contracts
{
    public enum SaveCheckpointReason
    {
        RaidStarted = 0,
        InventoryChanged = 1,
        SafePocketChanged = 2,
        Periodic = 3,
        RaidResolved = 4
    }

    public interface ISaveCheckpointService
    {
        bool HasPendingChanges { get; }
        event Action<SaveCheckpointReason> CheckpointCompleted;

        void RequestCheckpoint(SaveCheckpointReason reason);
        bool FlushPending();
    }
}
