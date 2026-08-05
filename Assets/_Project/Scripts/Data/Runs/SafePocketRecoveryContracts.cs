using System;
using System.Collections.Generic;
using ProjectHive.Data.Items;
using UnityEngine;

namespace ProjectHive.Data.Runs
{
    public enum SafePocketRecoveryState
    {
        None = 0,
        Available = 1,
        Claimed = 2,
        Expired = 3
    }

    [Serializable]
    public readonly struct SafePocketRecoverySnapshot
    {
        public SafePocketRecoverySnapshot(
            string recoveryId,
            string mapId,
            Vector3 worldPosition,
            IReadOnlyList<ItemStack> items,
            SafePocketRecoveryState state)
        {
            RecoveryId = recoveryId ?? string.Empty;
            MapId = mapId ?? string.Empty;
            WorldPosition = worldPosition;
            Items = items ?? Array.Empty<ItemStack>();
            State = state;
        }

        public string RecoveryId { get; }
        public string MapId { get; }
        public Vector3 WorldPosition { get; }
        public IReadOnlyList<ItemStack> Items { get; }
        public SafePocketRecoveryState State { get; }
        public bool IsAvailable => State == SafePocketRecoveryState.Available;
    }

    public enum SafePocketRecoveryChangeKind
    {
        Created = 0,
        Claimed = 1,
        Expired = 2
    }

    [Serializable]
    public readonly struct SafePocketRecoveryChange
    {
        public SafePocketRecoveryChange(
            SafePocketRecoveryChangeKind kind,
            in SafePocketRecoverySnapshot recovery)
        {
            Kind = kind;
            Recovery = recovery;
        }

        public SafePocketRecoveryChangeKind Kind { get; }
        public SafePocketRecoverySnapshot Recovery { get; }
    }

    public interface ISafePocketRecoveryService
    {
        SafePocketRecoverySnapshot CurrentRecovery { get; }
        event Action<SafePocketRecoveryChange> RecoveryChanged;

        bool TryCreate(
            string mapId,
            Vector3 deathPosition,
            IReadOnlyList<ItemStack> storedItems);
        bool TryClaim(
            string recoveryId,
            out IReadOnlyList<ItemStack> recoveredItems);
        void ResolveRaidEntry(string selectedMapId);
    }
}
