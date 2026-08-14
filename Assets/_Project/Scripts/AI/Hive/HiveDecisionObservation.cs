using System;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace ProjectHive.AI.Hive
{
    [Serializable]
    public readonly struct HiveDecisionObservation
    {
        public const int ObservationSize = 35;
        public const int ReportKindCount = 7;
        public const int ReportSourceCount = 6;
        public const int CommandKindCount = 6;
        public const float UnitCountNormalization = 8f;

        public HiveDecisionObservation(
            float alertScore,
            float reportConfidence,
            float normalizedReportAge,
            Vector2 normalizedReportedVelocity,
            float normalizedUncertainty,
            bool extractionActive,
            float normalizedRemainingTime,
            HiveUnitGroupSummary unitSummary,
            Vector2 normalizedExtractionOffset,
            EnemyReportKind reportKind,
            EnemyReportSource reportSource,
            HiveCommandKind lastCommandKind)
        {
            AlertScore = Mathf.Clamp01(alertScore);
            ReportConfidence = Mathf.Clamp01(reportConfidence);
            NormalizedReportAge = Mathf.Clamp01(normalizedReportAge);
            NormalizedReportedVelocity =
                Vector2.ClampMagnitude(normalizedReportedVelocity, 1f);
            NormalizedUncertainty = Mathf.Clamp01(normalizedUncertainty);
            ExtractionActive = extractionActive;
            NormalizedRemainingTime = Mathf.Clamp01(normalizedRemainingTime);
            UnitSummary = unitSummary;
            NormalizedExtractionOffset =
                Vector2.ClampMagnitude(normalizedExtractionOffset, 1f);
            ReportKind = reportKind;
            ReportSource = reportSource;
            LastCommandKind = lastCommandKind;
        }

        public float AlertScore { get; }
        public float ReportConfidence { get; }
        public float NormalizedReportAge { get; }
        public Vector2 NormalizedReportedVelocity { get; }
        public float NormalizedUncertainty { get; }
        public bool ExtractionActive { get; }
        public float NormalizedRemainingTime { get; }
        public HiveUnitGroupSummary UnitSummary { get; }
        public Vector2 NormalizedExtractionOffset { get; }
        public EnemyReportKind ReportKind { get; }
        public EnemyReportSource ReportSource { get; }
        public HiveCommandKind LastCommandKind { get; }

        public void WriteTo(VectorSensor sensor)
        {
            sensor.AddObservation(AlertScore);
            sensor.AddObservation(ReportConfidence);
            sensor.AddObservation(NormalizedReportAge);
            sensor.AddObservation(NormalizedReportedVelocity);
            sensor.AddObservation(NormalizedUncertainty);
            sensor.AddObservation(ExtractionActive);
            sensor.AddObservation(NormalizedRemainingTime);

            sensor.AddObservation(
                Mathf.Clamp01(UnitSummary.AvailableCount / UnitCountNormalization));
            sensor.AddObservation(
                Mathf.Clamp01(UnitSummary.HunterCount / UnitCountNormalization));
            sensor.AddObservation(
                Mathf.Clamp01(UnitSummary.ScoutCount / UnitCountNormalization));
            sensor.AddObservation(
                Mathf.Clamp01(UnitSummary.HeavyCount / UnitCountNormalization));
            sensor.AddObservation(UnitSummary.NormalizedMinimumDistance);
            sensor.AddObservation(UnitSummary.NormalizedAverageDistance);
            sensor.AddObservation(NormalizedExtractionOffset);

            AddOneHot(sensor, (int)ReportKind, ReportKindCount);
            AddOneHot(sensor, (int)ReportSource, ReportSourceCount);
            AddOneHot(sensor, (int)LastCommandKind, CommandKindCount);
        }

        private static void AddOneHot(VectorSensor sensor, int value, int count)
        {
            for (int index = 0; index < count; index++)
                sensor.AddObservation(index == value ? 1f : 0f);
        }
    }
}
