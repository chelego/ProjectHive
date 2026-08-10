using Unity.MLAgents;
using UnityEngine;

namespace ProjectHive.AI.Hive.Training
{
    [DisallowMultipleComponent]
    public sealed class HiveTrainingEnvironment : MonoBehaviour
    {
        private const int MaximumUnitCount = 8;

        private struct SimulatedUnit
        {
            public Vector2 Position;
            public float Speed;
            public HiveUnitRole Role;
            public bool Available;
        }

        [Header("Domain Randomization")]
        [SerializeField, Min(8f)] private float minimumArenaHalfSize = 16f;
        [SerializeField, Min(8f)] private float maximumArenaHalfSize = 30f;
        [SerializeField, Min(1)] private int minimumUnitCount = 3;
        [SerializeField, Min(1)] private int maximumUnitCount = MaximumUnitCount;
        [SerializeField, Range(0f, 0.8f)] private float unavailableUnitChance = 0.12f;
        [SerializeField, Range(0.5f, 1.2f)] private float minimumTerrainSpeedScale = 0.68f;
        [SerializeField, Range(0.5f, 1.2f)] private float maximumTerrainSpeedScale = 1.05f;

        [Header("Simulation")]
        [SerializeField, Min(0.05f)] private float simulationSecondsPerDecision = 0.25f;
        [SerializeField, Min(0.1f)] private float minimumPlayerSpeed = 2.2f;
        [SerializeField, Min(0.1f)] private float maximumPlayerSpeed = 3.8f;
        [SerializeField, Min(0.1f)] private float hunterSpeed = 3.6f;
        [SerializeField, Min(0.1f)] private float scoutSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float heavySpeed = 2.6f;
        [SerializeField, Min(0.1f)] private float catchDistance = 1.25f;
        [SerializeField, Min(1)] private int episodeDecisionLimit = 320;

        [Header("Report Model")]
        [SerializeField, Min(1)] private int minimumReportInterval = 3;
        [SerializeField, Min(2)] private int maximumReportInterval = 12;
        [SerializeField, Range(0f, 1f)] private float visualReportChance = 0.2f;
        [SerializeField, Range(0f, 1f)] private float environmentalNoiseChance = 0.22f;
        [SerializeField, Range(0f, 1f)] private float extractionEpisodeChance = 0.4f;

        private readonly SimulatedUnit[] units =
            new SimulatedUnit[MaximumUnitCount];
        private readonly int[] selectedUnitIndices =
            new int[MaximumUnitCount];
        private readonly float[] selectedUnitDistances =
            new float[MaximumUnitCount];

        private Vector2 playerPosition;
        private Vector2 playerVelocity;
        private Vector2 extractionPosition;
        private Vector2 reportedPosition;
        private Vector2 previousReportedPosition;
        private Vector2 playerHeading;
        private EnemyReportKind reportKind;
        private EnemyReportSource reportSource;
        private HiveCommandKind lastCommandKind;
        private float reportConfidence;
        private float reportUncertainty;
        private float reportAge;
        private float alertScore;
        private float arenaHalfSize;
        private float playerSpeed;
        private float terrainSpeedScale;
        private int unitCount;
        private int decisionsElapsed;
        private int decisionsUntilReport;
        private bool extractionActive;
        private bool hasPreviousReport;

        public bool ExtractionActive => extractionActive;
        public EnemyReportKind ReportKind => reportKind;

        public void ResetEpisode()
        {
            arenaHalfSize = Random.Range(
                Mathf.Min(minimumArenaHalfSize, maximumArenaHalfSize),
                Mathf.Max(minimumArenaHalfSize, maximumArenaHalfSize));
            playerSpeed = Random.Range(
                Mathf.Min(minimumPlayerSpeed, maximumPlayerSpeed),
                Mathf.Max(minimumPlayerSpeed, maximumPlayerSpeed));
            terrainSpeedScale = Random.Range(
                Mathf.Min(minimumTerrainSpeedScale, maximumTerrainSpeedScale),
                Mathf.Max(minimumTerrainSpeedScale, maximumTerrainSpeedScale));

            playerPosition = RandomPoint(0.75f);
            extractionPosition = RandomPoint(0.85f);
            playerHeading = GetRandomDirection();
            playerVelocity = playerHeading * playerSpeed;
            decisionsElapsed = 0;
            alertScore = Random.Range(0.05f, 0.35f);
            reportAge = 0f;
            extractionActive = Random.value < extractionEpisodeChance;
            lastCommandKind = HiveCommandKind.None;
            hasPreviousReport = false;

            ResetUnits();
            CreateReport(forceVisual: false);
        }

        public HiveDecisionObservation CreateObservation()
        {
            HiveUnitGroupSummary summary = CreateUnitSummary(reportedPosition);
            Vector2 extractionOffset =
                (extractionPosition - reportedPosition) /
                Mathf.Max(1f, arenaHalfSize * 2f);

            return new HiveDecisionObservation(
                alertScore,
                reportConfidence,
                Mathf.Clamp01(reportAge / 5f),
                hasPreviousReport
                    ? Vector2.ClampMagnitude(
                        (reportedPosition - previousReportedPosition) /
                        Mathf.Max(1f, arenaHalfSize),
                        1f)
                    : Vector2.zero,
                Mathf.Clamp01(reportUncertainty / Mathf.Max(1f, arenaHalfSize)),
                extractionActive,
                1f - Mathf.Clamp01(
                    (float)decisionsElapsed / episodeDecisionLimit),
                summary,
                extractionOffset,
                reportKind,
                reportSource,
                lastCommandKind);
        }

        public float SimulateDecision(
            HiveCommandKind commandKind,
            int targetSource,
            int requestedUnitCount,
            out bool episodeEnded)
        {
            episodeEnded = false;
            decisionsElapsed++;

            float oldClosestDistance = GetClosestUnitDistance(playerPosition);
            AdvancePlayer();
            reportAge += simulationSecondsPerDecision;
            decisionsUntilReport--;
            if (decisionsUntilReport <= 0)
                CreateReport(forceVisual: false);

            Vector2 target = ResolveTarget(targetSource);
            int selectedCount = SelectNearestUnits(target, requestedUnitCount);
            float commandSpeedScale = GetCommandSpeedScale(commandKind);
            if (commandKind != HiveCommandKind.None)
                MoveSelectedUnits(target, selectedCount, commandSpeedScale);

            float newClosestDistance = GetClosestUnitDistance(playerPosition);
            float reward = Mathf.Clamp(
                (oldClosestDistance - newClosestDistance) * 0.03f,
                -0.06f,
                0.06f);
            reward -= 0.001f;

            ApplyDecisionQualityRewards(
                commandKind,
                targetSource,
                requestedUnitCount,
                target,
                ref reward);

            lastCommandKind = commandKind;
            alertScore = Mathf.Max(0f, alertScore - 0.0025f);

            if (newClosestDistance <= catchDistance)
            {
                reward += 2f;
                RecordEpisodeStats(caughtPlayer: true, playerEscaped: false);
                episodeEnded = true;
                return reward;
            }

            if (PlayerCanExtract())
            {
                bool extractionBlocked =
                    GetClosestUnitDistance(extractionPosition) <= 5f;
                if (extractionBlocked)
                {
                    reward += 0.2f;
                    playerHeading = GetRandomDirection();
                    playerVelocity = playerHeading * playerSpeed;
                }
                else
                {
                    reward -= 1.5f;
                    RecordEpisodeStats(caughtPlayer: false, playerEscaped: true);
                    episodeEnded = true;
                    return reward;
                }
            }

            if (decisionsElapsed >= episodeDecisionLimit)
            {
                reward -= 0.2f;
                RecordEpisodeStats(caughtPlayer: false, playerEscaped: false);
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

        public int GetHeuristicUnitCountChoice()
        {
            int desired = GetDesiredUnitCount();
            if (desired <= 1)
                return 0;
            return desired <= 3 ? 1 : 2;
        }

        private void ResetUnits()
        {
            int minimum = Mathf.Clamp(minimumUnitCount, 1, MaximumUnitCount);
            int maximum = Mathf.Clamp(maximumUnitCount, minimum, MaximumUnitCount);
            unitCount = Random.Range(minimum, maximum + 1);

            bool anyAvailable = false;
            for (int index = 0; index < unitCount; index++)
            {
                HiveUnitRole role;
                float speed;
                switch (index % 4)
                {
                    case 1:
                        role = HiveUnitRole.Scout;
                        speed = scoutSpeed;
                        break;
                    case 3:
                        role = HiveUnitRole.Heavy;
                        speed = heavySpeed;
                        break;
                    default:
                        role = HiveUnitRole.Hunter;
                        speed = hunterSpeed;
                        break;
                }

                bool available = Random.value >= unavailableUnitChance;
                units[index] = new SimulatedUnit
                {
                    Position = RandomPoint(0.85f),
                    Speed = speed * Random.Range(0.9f, 1.1f),
                    Role = role,
                    Available = available
                };
                anyAvailable |= available;
            }

            if (!anyAvailable)
            {
                SimulatedUnit first = units[0];
                first.Available = true;
                units[0] = first;
            }
        }

        private void AdvancePlayer()
        {
            Vector2 desiredDirection =
                extractionActive && decisionsElapsed > episodeDecisionLimit / 3
                    ? (extractionPosition - playerPosition).normalized
                    : playerHeading;

            if (Random.value < 0.08f)
                playerHeading = GetRandomDirection();

            playerVelocity = Vector2.Lerp(
                playerVelocity,
                desiredDirection * playerSpeed,
                0.3f);
            playerPosition +=
                playerVelocity *
                terrainSpeedScale *
                simulationSecondsPerDecision;

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
                Random.value < 0.45f)
            {
                reportKind = EnemyReportKind.ExtractionActivity;
                reportSource = EnemyReportSource.ExtractionSystem;
                reportConfidence = Random.Range(0.75f, 1f);
                reportUncertainty = Random.Range(0.25f, 1.5f);
                reportedPosition =
                    extractionPosition +
                    Random.insideUnitCircle * reportUncertainty;
            }
            else if (visual)
            {
                bool spotter = Random.value < 0.35f;
                reportKind = spotter
                    ? EnemyReportKind.SpotterContact
                    : EnemyReportKind.VisualContact;
                reportSource = spotter
                    ? EnemyReportSource.Spotter
                    : EnemyReportSource.Monster;
                reportConfidence = Random.Range(0.78f, 1f);
                reportUncertainty = Random.Range(0.15f, 1.2f);
                reportedPosition =
                    playerPosition +
                    Random.insideUnitCircle * reportUncertainty;
            }
            else
            {
                bool environmentalNoise =
                    Random.value < environmentalNoiseChance;
                bool lostTarget = !environmentalNoise && Random.value < 0.15f;
                reportKind = lostTarget
                    ? EnemyReportKind.LostTarget
                    : EnemyReportKind.Noise;
                reportSource = environmentalNoise
                    ? EnemyReportSource.EnvironmentalNoise
                    : lostTarget
                        ? EnemyReportSource.Monster
                        : EnemyReportSource.PlayerNoise;
                reportConfidence = environmentalNoise
                    ? Random.Range(0.28f, 0.62f)
                    : Random.Range(0.35f, 0.8f);
                reportUncertainty = environmentalNoise
                    ? Random.Range(5f, 11f)
                    : Mathf.Lerp(7f, 1f, reportConfidence);
                reportedPosition =
                    playerPosition +
                    Random.insideUnitCircle * reportUncertainty;
            }

            reportedPosition = ClampToArena(reportedPosition);
            reportAge = 0f;
            float reliability =
                1f / (1f + reportUncertainty * 0.05f);
            alertScore = Mathf.Clamp01(
                alertScore +
                GetAlertIncrease(reportKind) *
                reportConfidence *
                reliability);
            decisionsUntilReport = Random.Range(
                minimumReportInterval,
                maximumReportInterval + 1);
        }

        private int SelectNearestUnits(Vector2 target, int requestedCount)
        {
            int limit = Mathf.Clamp(requestedCount, 0, MaximumUnitCount);
            int selectedCount = 0;

            for (int unitIndex = 0; unitIndex < unitCount; unitIndex++)
            {
                if (!units[unitIndex].Available)
                    continue;

                float distanceSquared =
                    (units[unitIndex].Position - target).sqrMagnitude;
                int insertIndex = selectedCount;
                while (insertIndex > 0 &&
                       distanceSquared < selectedUnitDistances[insertIndex - 1])
                {
                    insertIndex--;
                }

                if (insertIndex >= limit)
                    continue;

                int newCount = Mathf.Min(selectedCount + 1, limit);
                for (int shift = newCount - 1; shift > insertIndex; shift--)
                {
                    selectedUnitIndices[shift] = selectedUnitIndices[shift - 1];
                    selectedUnitDistances[shift] =
                        selectedUnitDistances[shift - 1];
                }

                selectedUnitIndices[insertIndex] = unitIndex;
                selectedUnitDistances[insertIndex] = distanceSquared;
                selectedCount = newCount;
            }

            return selectedCount;
        }

        private void MoveSelectedUnits(
            Vector2 target,
            int selectedCount,
            float commandSpeedScale)
        {
            for (int index = 0; index < selectedCount; index++)
            {
                int unitIndex = selectedUnitIndices[index];
                SimulatedUnit unit = units[unitIndex];
                unit.Position = Vector2.MoveTowards(
                    unit.Position,
                    target,
                    unit.Speed *
                    terrainSpeedScale *
                    commandSpeedScale *
                    simulationSecondsPerDecision);
                unit.Position = ClampToArena(unit.Position);
                units[unitIndex] = unit;
            }
        }

        private void ApplyDecisionQualityRewards(
            HiveCommandKind commandKind,
            int targetSource,
            int requestedUnitCount,
            Vector2 target,
            ref float reward)
        {
            if (commandKind == HiveCommandKind.None && alertScore > 0.4f)
                reward -= 0.012f;
            if (targetSource == 2 && !extractionActive)
                reward -= 0.018f;

            int desiredUnits = GetDesiredUnitCount();
            reward -= Mathf.Abs(requestedUnitCount - desiredUnits) * 0.0025f;
            reward -= requestedUnitCount * 0.00035f;

            if (commandKind == (HiveCommandKind)GetHeuristicCommand())
                reward += 0.004f;

            if (targetSource == 1 && hasPreviousReport)
            {
                float predictedError = Vector2.Distance(target, playerPosition);
                float reportError =
                    Vector2.Distance(reportedPosition, playerPosition);
                reward += Mathf.Clamp(
                    (reportError - predictedError) * 0.004f,
                    -0.015f,
                    0.015f);
            }

            if (reportSource == EnemyReportSource.EnvironmentalNoise &&
                requestedUnitCount >= 5)
            {
                reward -= 0.012f;
            }

            if (extractionActive &&
                Vector2.Distance(playerPosition, extractionPosition) < 6f &&
                GetClosestUnitDistance(extractionPosition) < 5f)
            {
                reward += 0.02f;
            }
        }

        private Vector2 ResolveTarget(int targetSource)
        {
            switch (Mathf.Clamp(targetSource, 0, 3))
            {
                case 0:
                    return reportedPosition;
                case 1:
                    Vector2 velocity = hasPreviousReport
                        ? reportedPosition - previousReportedPosition
                        : Vector2.zero;
                    return ClampToArena(
                        reportedPosition +
                        Vector2.ClampMagnitude(velocity, arenaHalfSize * 0.5f));
                case 2:
                    return extractionPosition;
                default:
                    return Vector2.zero;
            }
        }

        private HiveUnitGroupSummary CreateUnitSummary(Vector2 target)
        {
            int available = 0;
            int hunters = 0;
            int scouts = 0;
            int heavies = 0;
            float minimumDistance = float.PositiveInfinity;
            float totalDistance = 0f;
            float normalizationDistance = Mathf.Max(1f, arenaHalfSize * 2f);

            for (int index = 0; index < unitCount; index++)
            {
                SimulatedUnit unit = units[index];
                if (!unit.Available)
                    continue;

                available++;
                switch (unit.Role)
                {
                    case HiveUnitRole.Hunter:
                        hunters++;
                        break;
                    case HiveUnitRole.Scout:
                        scouts++;
                        break;
                    case HiveUnitRole.Heavy:
                        heavies++;
                        break;
                }

                float distance = Vector2.Distance(unit.Position, target);
                minimumDistance = Mathf.Min(minimumDistance, distance);
                totalDistance += distance;
            }

            return new HiveUnitGroupSummary(
                available,
                hunters,
                scouts,
                heavies,
                available > 0
                    ? Mathf.Clamp01(minimumDistance / normalizationDistance)
                    : 1f,
                available > 0
                    ? Mathf.Clamp01(
                        totalDistance / available / normalizationDistance)
                    : 1f);
        }

        private float GetClosestUnitDistance(Vector2 target)
        {
            float closest = float.PositiveInfinity;
            for (int index = 0; index < unitCount; index++)
            {
                if (!units[index].Available)
                    continue;
                closest = Mathf.Min(
                    closest,
                    Vector2.Distance(units[index].Position, target));
            }

            return float.IsPositiveInfinity(closest)
                ? arenaHalfSize * 2f
                : closest;
        }

        private int GetDesiredUnitCount()
        {
            if (reportSource == EnemyReportSource.EnvironmentalNoise ||
                reportConfidence < 0.4f)
            {
                return 1;
            }

            if (reportKind == EnemyReportKind.VisualContact ||
                reportKind == EnemyReportKind.SpotterContact ||
                reportKind == EnemyReportKind.ExtractionActivity ||
                alertScore > 0.72f)
            {
                return 5;
            }

            return 3;
        }

        private bool PlayerCanExtract()
        {
            return extractionActive &&
                   decisionsElapsed > episodeDecisionLimit / 3 &&
                   Vector2.Distance(playerPosition, extractionPosition) < 1.25f;
        }

        private void RecordEpisodeStats(bool caughtPlayer, bool playerEscaped)
        {
            StatsRecorder stats = Academy.Instance.StatsRecorder;
            stats.Add(
                "Hive/CatchRate",
                caughtPlayer ? 1f : 0f,
                StatAggregationMethod.Average);
            stats.Add(
                "Hive/EscapeRate",
                playerEscaped ? 1f : 0f,
                StatAggregationMethod.Average);
            stats.Add(
                "Hive/EpisodeDecisions",
                decisionsElapsed,
                StatAggregationMethod.Average);
            stats.Add(
                "Hive/FinalClosestDistance",
                GetClosestUnitDistance(playerPosition),
                StatAggregationMethod.Average);
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

        private static Vector2 GetRandomDirection()
        {
            Vector2 direction = Random.insideUnitCircle;
            return direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector2.right;
        }

        private static float GetCommandSpeedScale(
            HiveCommandKind commandKind)
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
