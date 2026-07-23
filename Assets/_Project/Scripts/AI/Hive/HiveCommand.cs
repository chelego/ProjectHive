using System;
using UnityEngine;

namespace ProjectHive.AI.Hive
{
    public enum HiveCommandKind
    {
        None = 0,
        Investigate = 1,
        SearchArea = 2,
        Converge = 3,
        GuardExtraction = 4,
        ResumeHunt = 5
    }

    [Serializable]
    public readonly struct HiveCommand
    {
        public HiveCommand(
            int sequence,
            HiveCommandKind kind,
            Vector3 targetPosition,
            float radius,
            float priority,
            float issuedAt,
            float expiresAt)
        {
            Sequence = sequence;
            Kind = kind;
            TargetPosition = targetPosition;
            Radius = Mathf.Max(0f, radius);
            Priority = Mathf.Clamp01(priority);
            IssuedAt = issuedAt;
            ExpiresAt = Mathf.Max(issuedAt, expiresAt);
        }

        public int Sequence { get; }
        public HiveCommandKind Kind { get; }
        public Vector3 TargetPosition { get; }
        public float Radius { get; }
        public float Priority { get; }
        public float IssuedAt { get; }
        public float ExpiresAt { get; }
        public bool IsExpired(float currentTime) => currentTime > ExpiresAt;
    }
}
