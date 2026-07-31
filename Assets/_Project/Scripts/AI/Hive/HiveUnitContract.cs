using System;
using UnityEngine;

namespace ProjectHive.AI.Hive
{
    public enum HiveUnitRole
    {
        Unknown = 0,
        Hunter = 1,
        Scout = 2,
        Heavy = 3,
        Support = 4
    }

    [Flags]
    public enum HiveUnitCapabilities
    {
        None = 0,
        GroundMovement = 1 << 0,
        Flight = 1 << 1,
        Attack = 1 << 2,
        Investigate = 1 << 3,
        Guard = 1 << 4,
        Intercept = 1 << 5,
        ReportTarget = 1 << 6
    }

    [Serializable]
    public readonly struct HiveUnitSnapshot
    {
        public HiveUnitSnapshot(
            int unitId,
            Vector3 position,
            HiveUnitRole role,
            HiveUnitCapabilities capabilities,
            float movementSpeed,
            bool isAvailable,
            int currentCommandSequence)
        {
            UnitId = unitId;
            Position = position;
            Role = role;
            Capabilities = capabilities;
            MovementSpeed = Mathf.Max(0f, movementSpeed);
            IsAvailable = isAvailable;
            CurrentCommandSequence = currentCommandSequence;
        }

        public int UnitId { get; }
        public Vector3 Position { get; }
        public HiveUnitRole Role { get; }
        public HiveUnitCapabilities Capabilities { get; }
        public float MovementSpeed { get; }
        public bool IsAvailable { get; }
        public int CurrentCommandSequence { get; }

        public bool HasCapabilities(HiveUnitCapabilities required)
        {
            return (Capabilities & required) == required;
        }
    }

    public interface IHiveControllable
    {
        HiveUnitSnapshot GetHiveUnitSnapshot();
        bool TryAcceptHiveCommand(in HiveCommand command);
    }
}
