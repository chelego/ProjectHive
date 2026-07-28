using UnityEngine;

namespace ProjectHive.AI.Hive.Training
{
    [DisallowMultipleComponent]
    public sealed class HiveTrainingEnvironment : MonoBehaviour
    {
        [Header("Simulation")]
        [SerializeField, Min(5f)] private float arenaHalfSize = 20f;
        [SerializeField, Min(0.1f)] private float simulationSecondsPerDecision = 0.25f;
        [SerializeField, Min(0.1f)] private float playerSpeed = 2.8f;
        [SerializeField, Min(0.1f)] private float hunterSpeed = 3.6f;
        [SerializeField, Min(0.1f)] private float catchDistance = 1.25f;
        [SerializeField, Min(1)] private int episodeDecisionLimit = 320;

        [Header("Report Model")]
        [SerializeField, Min(1)] private int minimumReportInterval = 3;
        [SerializeField, Min(2)] private int maximumReportInterval = 12;
        [SerializeField, Range(0f, 1f)] private float visualReportChance = 0.2f;
        [SerializeField, Range(0f, 1f)] private float extractionEpisodeChance = 0.35f;

        private Vector2 playerPosition;
        private Vector2 playerVelocity;
        private Vector2 hunterPosition;
        private Vector2 extractionPosition;
        private Vector2 reportedPosition;
        private Vector2 previousReportedPosition;
        private Vector2 playerHeading;
        private EnemyReportKind reportKind;
        private HiveCommandKind lastCommandKind;
        private float reportConfidence;
        private float reportAge;
        private float alertScore;
        private int decisionsElapsed;
        private int decisionsUntilReport;
        private bool extractionActive;
        private bool hasPreviousReport;

        public float AlertScore => alertScore;
        public float ReportConfidence => reportConfidence;
        public float NormalizedReportAge => Mathf.Clamp01(reportAge / 5f);
        public Vector2 NormalizedReportedVelocity =>
            hasPreviousReport
                ? Vector2.ClampMagnitude((reportedPosition - previousReportedPosition) / arenaHalfSize, 1f)
                : Vector2.zero;
        public bool ExtractionActive => extractionActive;
        public float NormalizedRemainingTime =>
            1f - Mathf.Clamp01((float)decisionsElapsed / episodeDecisionLimit);
        public EnemyReportKind ReportKind => reportKind;
        public HiveCommandKind LastCommandKind => lastCommandKind;

        public void ResetEpisode()
        {
            playerPosition = RandomPoint(0.75f);
            hunterPosition = RandomPoint(0.75f);
            extractionPosition = RandomPoint(0.85f);
            playerHeading = Random.insideUnitCircle.normalized;
            playerVelocity = playerHeading * playerSpeed;
            decisionsElapsed = 0;
            alertScore = Random.Range(0.05f, 0.35f);
            reportAge = 0f;
            extractionActive = Random.value < extractionEpisodeChance;
            lastCommandKind = HiveCommandKind.None;
            hasPreviousReport = false;
            CreateReport(forceVisual: false);
        }

        public float SimulateDecision(
            HiveCommandKind commandKind,
            int targetSource,
            out bool episodeEnded)
        {
            episodeEnded = false;
            decisionsElapsed++;

            AdvancePlayer();
            reportAge += simulationSecondsPerDecision;
            decisionsUntilReport--;
            if (decisionsUntilReport <= 0)
                CreateReport(forceVisual: false);

            float oldDistance = Vector2.Distance(hunterPosition, playerPosition);
            Vector2 target = ResolveTarget(targetSource);
            float commandSpeedScale = GetCommandSpeedScale(commandKind);

            if (commandKind != HiveCommandKind.None)
            {
                hunterPosition = Vector2.MoveTowards(
                    hunterPosition,
                    target,
                    hunterSpeed * commandSpeedScale * simulationSecondsPerDecision);
            }

            hunterPosition = ClampToArena(hunterPosition);
            float newDistance = Vector2.Distance(hunterPosition, playerPosition);
            float reward = Mathf.Clamp((oldDistance - newDistance) * 0.025f, -0.05f, 0.05f);

            reward -= 0.001f;
            if (commandKind == HiveCommandKind.None && alertScore > 0.4f)
                reward -= 0.01f;
            if (targetSource == 2 && !extractionActive)
                reward -= 0.015f;
            if (commandKind == lastCommandKind)
                reward -= 0.0015f;

            if (extractionActive &&
                Vector2.Distance(playerPosition, extractionPosition) < 2.5f &&
                Vector2.Distance(hunterPosition, extractionPosition) < 4f)
            {
                reward += 0.025f;
            }

            lastCommandKind = commandKind;
            alertScore = Mathf.Max(0f, alertScore - 0.0025f);

            if (newDistance <= catchDistance)
            {
                reward += 2f;
                episodeEnded = true;
            }
            else if (decisionsElapsed >= episodeDecisionLimit)
            {
                reward -= 0.25f;
                episodeEnded = true;
            }

            return reward;
        }

        public int GetHeuristicCommand()
        {
            switch (reportKind)
            {
                case EnemyReportKind.VisualContact:
                case EnemyReportKind.SpotterContact:
                    return (int)HiveCommandKind.Converge;
                case EnemyReportKind.ExtractionActivity:
                    return (int)HiveCommandKind.GuardExtraction;
                case EnemyReportKind.LostTarget:
                    return (int)HiveCommandKind.SearchArea;
                case EnemyReportKind.Noise:
                    return (int)HiveCommandKind.Investigate;
                default:
                    return (int)HiveCommandKind.ResumeHunt;
            }
        }

        private void AdvancePlayer()
        {
            Vector2 desiredDirection;
            if (extractionActive && decisionsElapsed > episodeDecisionLimit / 3)
                desiredDirection = (extractionPosition - playerPosition).normalized;
            else
                desiredDirection = playerHeading;

            if (Random.value < 0.08f)
                playerHeading = Random.insideUnitCircle.normalized;

            playerVelocity = Vector2.Lerp(
                playerVelocity,
                desiredDirection * playerSpeed,
                0.3f);
            playerPosition += playerVelocity * simulationSecondsPerDecision;

            if (Mathf.Abs(playerPosition.x) >= arenaHalfSize ||
                Mathf.Abs(playerPosition.y) >= arenaHalfSize)
            {
                playerPosition = ClampToArena(playerPosition);
                playerHeading = (-playerPosition).normalized;
                playerVelocity = playerHeading * playerSpeed;
            }
        }

        private void CreateReport(bool forceVisual)
        {
            previousReportedPosition = reportedPosition;
            hasPreviousReport = decisionsElapsed > 0;

            bool visual = forceVisual || Random.value < visualReportChance;
            if (extractionActive &&
                Vector2.Distance(playerPosition, extractionPosition) < 5f &&
                Random.value < 0.4f)
            {
                reportKind = EnemyReportKind.ExtractionActivity;
                reportConfidence = Random.Range(0.75f, 1f);
                reportedPosition = extractionPosition;
            }
            else if (visual)
            {
                reportKind = Random.value < 0.35f
                    ? EnemyReportKind.SpotterContact
                    : EnemyReportKind.VisualContact;
                reportConfidence = Random.Range(0.78f, 1f);
                reportedPosition = playerPosition + Random.insideUnitCircle * 0.75f;
            }
            else
            {
                reportKind = Random.value < 0.15f
                    ? EnemyReportKind.LostTarget
                    : EnemyReportKind.Noise;
                reportConfidence = Random.Range(0.35f, 0.8f);
                float errorRadius = Mathf.Lerp(6f, 1f, reportConfidence);
                reportedPosition = playerPosition + Random.insideUnitCircle * errorRadius;
            }

            reportedPosition = ClampToArena(reportedPosition);
            reportAge = 0f;
            alertScore = Mathf.Clamp01(
                alertScore + GetAlertIncrease(reportKind) * reportConfidence);
            decisionsUntilReport = Random.Range(
                minimumReportInterval,
                maximumReportInterval + 1);
        }

        private Vector2 ResolveTarget(int targetSource)
        {
            switch (Mathf.Clamp(targetSource, 0, 3))
            {
                case 0:
                    return reportedPosition;
                case 1:
                    return ClampToArena(
                        reportedPosition + (reportedPosition - previousReportedPosition));
                case 2:
                    return extractionPosition;
                default:
                    return Vector2.zero;
            }
        }

        private Vector2 RandomPoint(float scale)
        {
            return new Vector2(
                Random.Range(-arenaHalfSize, arenaHalfSize),
                Random.Range(-arenaHalfSize, arenaHalfSize)) * scale;
        }

        private Vector2 ClampToArena(Vector2 point)
        {
            return new Vector2(
                Mathf.Clamp(point.x, -arenaHalfSize, arenaHalfSize),
                Mathf.Clamp(point.y, -arenaHalfSize, arenaHalfSize));
        }

        private static float GetCommandSpeedScale(HiveCommandKind commandKind)
        {
            switch (commandKind)
            {
                case HiveCommandKind.Converge:
                    return 1.15f;
                case HiveCommandKind.Investigate:
                    return 0.9f;
                case HiveCommandKind.SearchArea:
                    return 0.75f;
                case HiveCommandKind.GuardExtraction:
                    return 0.85f;
                case HiveCommandKind.ResumeHunt:
                    return 0.7f;
                default:
                    return 0f;
            }
        }

        private static float GetAlertIncrease(EnemyReportKind kind)
        {
            switch (kind)
            {
                case EnemyReportKind.SpotterContact:
                    return 0.5f;
                case EnemyReportKind.VisualContact:
                    return 0.35f;
                case EnemyReportKind.ExtractionActivity:
                    return 0.45f;
                case EnemyReportKind.Noise:
                    return 0.15f;
                default:
                    return 0.08f;
            }
        }
    }
}
