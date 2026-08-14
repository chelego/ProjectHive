using UnityEngine;

namespace ProjectHive.AI.Hive
{
    [DisallowMultipleComponent]
    public sealed class RuleBasedHivePolicy : MonoBehaviour, IHiveDecisionPolicy
    {
        [SerializeField, Min(0.05f)] private float decisionCooldown = 0.25f;
        [SerializeField, Min(1f)] private float investigateRadius = 12f;
        [SerializeField, Min(1f)] private float searchRadius = 20f;
        [SerializeField, Min(1f)] private float convergeRadius = 8f;
        [SerializeField, Min(0.1f)] private float commandLifetime = 12f;

        private int lastProcessedBlackboardVersion = -1;
        private float nextDecisionTime;

        public bool TryCreateCommand(
            HiveBlackboard blackboard,
            float currentTime,
            int commandSequence,
            out HiveCommand command)
        {
            if (blackboard == null ||
                !blackboard.HasLastReport ||
                blackboard.Version == lastProcessedBlackboardVersion ||
                currentTime < nextDecisionTime)
            {
                command = default;
                return false;
            }

            EnemyReport report = blackboard.LastReport;
            HiveCommandKind kind;
            float radius;

            switch (report.Kind)
            {
                case EnemyReportKind.VisualContact:
                case EnemyReportKind.SpotterContact:
                    kind = HiveCommandKind.Converge;
                    radius = convergeRadius;
                    break;

                case EnemyReportKind.ExtractionActivity:
                    kind = HiveCommandKind.GuardExtraction;
                    radius = searchRadius;
                    break;

                case EnemyReportKind.LostTarget:
                    kind = HiveCommandKind.SearchArea;
                    radius = searchRadius;
                    break;

                case EnemyReportKind.Noise:
                    kind = HiveCommandKind.Investigate;
                    radius = investigateRadius;
                    break;

                default:
                    kind = HiveCommandKind.ResumeHunt;
                    radius = searchRadius;
                    break;
            }

            radius = Mathf.Max(radius, report.UncertaintyRadius * 2f);

            float priority = Mathf.Clamp01(
                blackboard.AlertScore * 0.65f +
                report.Confidence * 0.35f);

            command = new HiveCommand(
                commandSequence,
                kind,
                report.Position,
                radius,
                priority,
                currentTime,
                currentTime + commandLifetime);

            lastProcessedBlackboardVersion = blackboard.Version;
            nextDecisionTime = currentTime + decisionCooldown;
            return true;
        }
    }
}
