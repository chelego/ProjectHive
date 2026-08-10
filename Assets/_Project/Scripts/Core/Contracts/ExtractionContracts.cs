using System;
using System.Collections.Generic;
using UnityEngine;

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

    [Serializable]
    public readonly struct ExtractionGateSnapshot
    {
        public ExtractionGateSnapshot(
            string gateId,
            ExtractionGateState state,
            bool isEntryOnly,
            float openingProgress,
            Vector3 worldPosition)
        {
            GateId = gateId ?? string.Empty;
            State = state;
            IsEntryOnly = isEntryOnly;
            OpeningProgress = Mathf.Clamp01(openingProgress);
            WorldPosition = worldPosition;
        }

        public string GateId { get; }
        public ExtractionGateState State { get; }
        public bool IsEntryOnly { get; }
        public float OpeningProgress { get; }
        public Vector3 WorldPosition { get; }
    }

    public interface IExtractionRegistry
    {
        IReadOnlyList<ExtractionGateSnapshot> Gates { get; }
        event Action<ExtractionGateSnapshot> GateChanged;

        bool TryGetGate(string gateId, out IExtractionGate gate);
    }
}
