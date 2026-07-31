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

    public enum EnemyReportSource
    {
        Unknown = 0,
        PlayerNoise = 1,
        EnvironmentalNoise = 2,
        Monster = 3,
        Spotter = 4,
        ExtractionSystem = 5
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
            : this(
                kind,
                position,
                confidence,
                0f,
                InferSource(kind),
                reporterInstanceId,
                occurredAt)
        {
        }

        public EnemyReport(
            EnemyReportKind kind,
            Vector3 position,
            float confidence,
            float uncertaintyRadius,
            EnemyReportSource source,
            int reporterInstanceId,
            float occurredAt)
        {
            Kind = kind;
            Position = position;
            Confidence = Mathf.Clamp01(confidence);
            UncertaintyRadius = Mathf.Max(0f, uncertaintyRadius);
            Source = source;
            ReporterInstanceId = reporterInstanceId;
            OccurredAt = occurredAt;
        }

        public EnemyReportKind Kind { get; }
        public Vector3 Position { get; }
        public float Confidence { get; }
        public float UncertaintyRadius { get; }
        public EnemyReportSource Source { get; }
        public int ReporterInstanceId { get; }
        public float OccurredAt { get; }

        private static EnemyReportSource InferSource(EnemyReportKind kind)
        {
            switch (kind)
            {
                case EnemyReportKind.Noise:
                    return EnemyReportSource.PlayerNoise;
                case EnemyReportKind.SpotterContact:
                    return EnemyReportSource.Spotter;
                case EnemyReportKind.ExtractionActivity:
                    return EnemyReportSource.ExtractionSystem;
                case EnemyReportKind.VisualContact:
                case EnemyReportKind.LostTarget:
                case EnemyReportKind.TargetDown:
                    return EnemyReportSource.Monster;
                default:
                    return EnemyReportSource.Unknown;
            }
        }
    }
}
