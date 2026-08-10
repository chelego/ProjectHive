using ProjectHive.Core.Events;
using UnityEngine;

namespace ProjectHive.AI.Hive
{
    public static class HiveReportFactory
    {
        public static bool TryCreateFromNoise(
            in NoiseEvent noiseEvent,
            out EnemyReport report)
        {
            if (noiseEvent.Affiliation == NoiseAffiliation.Monster)
            {
                report = default;
                return false;
            }

            EnemyReportSource source =
                noiseEvent.Affiliation == NoiseAffiliation.Environment ||
                noiseEvent.Category == NoiseCategory.WildlifeAlarm
                    ? EnemyReportSource.EnvironmentalNoise
                    : EnemyReportSource.PlayerNoise;

            float uncertaintyRadius = noiseEvent.LocationUncertaintyRadius > 0f
                ? noiseEvent.LocationUncertaintyRadius
                : GetDefaultUncertainty(noiseEvent.Category);

            report = new EnemyReport(
                EnemyReportKind.Noise,
                noiseEvent.Position,
                Mathf.Clamp01(noiseEvent.Loudness),
                uncertaintyRadius,
                source,
                noiseEvent.SourceInstanceId,
                noiseEvent.OccurredAt);
            return true;
        }

        private static float GetDefaultUncertainty(NoiseCategory category)
        {
            switch (category)
            {
                case NoiseCategory.Gunshot:
                    return 1f;
                case NoiseCategory.Footstep:
                    return 3f;
                case NoiseCategory.ThrownObject:
                    return 2f;
                case NoiseCategory.WildlifeAlarm:
                    return 6f;
                case NoiseCategory.DoorOrMachine:
                    return 1.5f;
                default:
                    return 2.5f;
            }
        }
    }
}
