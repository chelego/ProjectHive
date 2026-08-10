using System;
using UnityEngine;

namespace ProjectHive.AI.Hive
{
    [Serializable]
    public sealed class HiveBlackboard
    {
        [SerializeField, Range(0f, 1f)] private float alertScore;
        [SerializeField] private int version;
        [SerializeField] private bool hasLastReport;
        [SerializeField] private EnemyReport lastReport;

        private EnemyReport[] recentReports;
        private int writeIndex;
        private int count;

        public HiveBlackboard(int reportCapacity)
        {
            recentReports = new EnemyReport[Mathf.Max(4, reportCapacity)];
        }

        public float AlertScore => alertScore;
        public int Version => version;
        public int ReportCount => count;
        public bool HasLastReport => hasLastReport;
        public EnemyReport LastReport => lastReport;

        public void Record(in EnemyReport report)
        {
            EnsureStorage();

            recentReports[writeIndex] = report;
            writeIndex = (writeIndex + 1) % recentReports.Length;
            count = Mathf.Min(count + 1, recentReports.Length);

            lastReport = report;
            hasLastReport = true;
            alertScore = Mathf.Clamp01(alertScore + GetAlertIncrease(report));
            version++;
        }

        public void DecayAlert(float amount)
        {
            alertScore = Mathf.Max(0f, alertScore - Mathf.Max(0f, amount));
        }

        public bool TryGetRecent(int newestOffset, out EnemyReport report)
        {
            EnsureStorage();

            if (newestOffset < 0 || newestOffset >= count)
            {
                report = default;
                return false;
            }

            int index = writeIndex - 1 - newestOffset;
            while (index < 0)
                index += recentReports.Length;

            report = recentReports[index];
            return true;
        }

        public void Clear()
        {
            EnsureStorage();
            Array.Clear(recentReports, 0, recentReports.Length);
            writeIndex = 0;
            count = 0;
            alertScore = 0f;
            hasLastReport = false;
            lastReport = default;
            version++;
        }

        private void EnsureStorage()
        {
            if (recentReports == null || recentReports.Length == 0)
                recentReports = new EnemyReport[32];
        }

        private static float GetAlertIncrease(in EnemyReport report)
        {
            float baseIncrease;
            switch (report.Kind)
            {
                case EnemyReportKind.VisualContact:
                    baseIncrease = 0.35f;
                    break;
                case EnemyReportKind.SpotterContact:
                    baseIncrease = 0.5f;
                    break;
                case EnemyReportKind.ExtractionActivity:
                    baseIncrease = 0.45f;
                    break;
                case EnemyReportKind.Noise:
                    baseIncrease = 0.15f;
                    break;
                case EnemyReportKind.LostTarget:
                    baseIncrease = 0.08f;
                    break;
                default:
                    baseIncrease = 0.05f;
                    break;
            }

            float uncertaintyReliability =
                1f / (1f + report.UncertaintyRadius * 0.05f);
            return baseIncrease *
                   Mathf.Lerp(0.35f, 1f, report.Confidence) *
                   uncertaintyReliability;
        }
    }
}
