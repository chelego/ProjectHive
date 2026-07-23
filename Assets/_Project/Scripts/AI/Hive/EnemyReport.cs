using System;
using UnityEngine;

namespace ProjectHive.AI.Hive
{
    public enum EnemyReportKind
    {
        Unknown = 0,
        Noise = 1,
        VisualContact = 2,
        SpotterContact = 3,
        LostTarget = 4,
        ExtractionActivity = 5,
        TargetDown = 6
    }

    [Serializable]
    public readonly struct EnemyReport
    {
        public EnemyReport(
            EnemyReportKind kind,
            Vector3 position,
            float confidence,
            int reporterInstanceId,
            float occurredAt)
        {
            Kind = kind;
            Position = position;
            Confidence = Mathf.Clamp01(confidence);
            ReporterInstanceId = reporterInstanceId;
            OccurredAt = occurredAt;
        }

        public EnemyReportKind Kind { get; }
        public Vector3 Position { get; }
        public float Confidence { get; }
        public int ReporterInstanceId { get; }
        public float OccurredAt { get; }
    }
}
