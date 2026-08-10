using ProjectHive.AI.Hive.Training;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace ProjectHive.AI.Hive
{
    [RequireComponent(typeof(BehaviorParameters))]
    [DisallowMultipleComponent]
    public sealed class LearnedHivePolicy : Agent, IHiveDecisionPolicy
    {
        [Header("Inference")]
        [SerializeField] private ModelAsset model;
        [SerializeField] private InferenceDevice inferenceDevice =
            InferenceDevice.Burst;
        [SerializeField] private HiveUnitRegistry unitRegistry;
        [SerializeField] private MonoBehaviour raidContextComponent;
        [SerializeField] private MonoBehaviour fallbackPolicyComponent;

        [Header("Decision")]
        [SerializeField, Min(0.05f)] private float decisionCooldown = 0.25f;
        [SerializeField, Min(1f)] private float observationDistance = 80f;
        [SerializeField, Min(0.1f)] private float predictionSeconds = 2f;
        [SerializeField, Min(0.1f)] private float commandLifetime = 12f;
        [SerializeField, Min(1f)] private float investigateRadius = 12f;
        [SerializeField, Min(1f)] private float searchRadius = 20f;
        [SerializeField, Min(1f)] private float convergeRadius = 8f;

        private readonly Vector3[] targetCandidates = new Vector3[4];
        private BehaviorParameters behaviorParameters;
        private IHiveRaidContext raidContext;
        private IHiveDecisionPolicy fallbackPolicy;
        private HiveDecisionObservation currentObservation;
        private bool awaitingDecision;
        private bool hasPendingCommand;
        private HiveCommand pendingCommand;
        private int pendingSequence;
        private float pendingIssuedAt;
        private float pendingPriority;
        private float nextDecisionTime;
        private int lastProcessedBlackboardVersion = -1;

        public bool HasInferenceModel =>
            model != null ||
            (behaviorParameters != null &&
             behaviorParameters.Model != null);

        public void Configure(
            ModelAsset inferenceModel,
            HiveUnitRegistry registry,
            MonoBehaviour raidContext,
            MonoBehaviour fallback)
        {
            model = inferenceModel;
            unitRegistry = registry;
            raidContextComponent = raidContext;
            fallbackPolicyComponent = fallback;
            ResolveDependencies();

            behaviorParameters = GetComponent<BehaviorParameters>();
            behaviorParameters.BehaviorName = HiveTrainingAgent.BehaviorName;
            behaviorParameters.Model = inferenceModel;
            behaviorParameters.InferenceDevice = inferenceDevice;
            behaviorParameters.BehaviorType = inferenceModel != null
                ? BehaviorType.InferenceOnly
                : BehaviorType.HeuristicOnly;
        }

        public override void Initialize()
        {
            ResolveDependencies();
            MaxStep = 0;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            currentObservation.WriteTo(sensor);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (!awaitingDecision)
                return;

            int commandChoice = Mathf.Clamp(
                actions.DiscreteActions[0],
                0,
                HiveTrainingAgent.CommandBranchSize - 1);
            int targetChoice = Mathf.Clamp(
                actions.DiscreteActions[1],
                0,
                HiveTrainingAgent.TargetBranchSize - 1);
            int unitCountChoice = Mathf.Clamp(
                actions.DiscreteActions[2],
                0,
                HiveTrainingAgent.UnitCountBranchSize - 1);

            HiveCommandKind kind = (HiveCommandKind)commandChoice;
            float radius = GetCommandRadius(kind);
            radius = Mathf.Max(
                radius,
                currentObservation.NormalizedUncertainty *
                observationDistance *
                2f);

            pendingCommand = new HiveCommand(
                pendingSequence,
                kind,
                targetCandidates[targetChoice],
                radius,
                pendingPriority,
                HiveTrainingAgent.ResolveRequestedUnitCount(unitCountChoice),
                pendingIssuedAt,
                pendingIssuedAt + commandLifetime);
            awaitingDecision = false;
            hasPendingCommand = kind != HiveCommandKind.None;
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            ActionSegment<int> discrete = actionsOut.DiscreteActions;
            discrete[0] = GetHeuristicCommand(currentObservation.ReportKind);
            discrete[1] =
                currentObservation.ExtractionActive &&
                currentObservation.ReportKind ==
                EnemyReportKind.ExtractionActivity
                    ? 2
                    : 0;
            discrete[2] = currentObservation.AlertScore > 0.7f
                ? 2
                : currentObservation.ReportConfidence < 0.4f
                    ? 0
                    : 1;
        }

        public bool TryCreateCommand(
            HiveBlackboard blackboard,
            float currentTime,
            int commandSequence,
            out HiveCommand command)
        {
            ResolveDependencies();

            if (!HasInferenceModel)
            {
                if (fallbackPolicy != null)
                {
                    return fallbackPolicy.TryCreateCommand(
                        blackboard,
                        currentTime,
                        commandSequence,
                        out command);
                }

                command = default;
                return false;
            }

            if (hasPendingCommand)
            {
                command = pendingCommand;
                hasPendingCommand = false;
                return true;
            }

            if (blackboard == null ||
                !blackboard.HasLastReport ||
                blackboard.Version == lastProcessedBlackboardVersion ||
                currentTime < nextDecisionTime ||
                awaitingDecision)
            {
                command = default;
                return false;
            }

            CaptureDecisionState(
                blackboard,
                currentTime,
                commandSequence);
            lastProcessedBlackboardVersion = blackboard.Version;
            nextDecisionTime = currentTime + decisionCooldown;
            awaitingDecision = true;
            RequestDecision();
            command = default;
            return false;
        }

        private void CaptureDecisionState(
            HiveBlackboard blackboard,
            float currentTime,
            int commandSequence)
        {
            EnemyReport latest = blackboard.LastReport;
            Vector3 reportedVelocity = Vector3.zero;
            if (blackboard.TryGetRecent(1, out EnemyReport previous))
            {
                float elapsed = Mathf.Max(
                    0.1f,
                    latest.OccurredAt - previous.OccurredAt);
                reportedVelocity =
                    (latest.Position - previous.Position) / elapsed;
            }

            bool extractionActive =
                raidContext?.ExtractionActive ??
                latest.Kind == EnemyReportKind.ExtractionActivity;
            float remainingTime =
                raidContext?.NormalizedRemainingTime ?? 1f;
            Vector3 extractionPosition = latest.Position;
            if (raidContext == null ||
                !raidContext.TryGetActiveExtractionPosition(
                    out extractionPosition))
            {
                extractionPosition =
                    latest.Kind == EnemyReportKind.ExtractionActivity
                        ? latest.Position
                        : transform.position;
            }

            HiveUnitGroupSummary unitSummary =
                unitRegistry != null
                    ? unitRegistry.GetSummary(
                        latest.Position,
                        observationDistance)
                    : default;
            Vector3 extractionOffset =
                (extractionPosition - latest.Position) /
                Mathf.Max(1f, observationDistance);

            currentObservation = new HiveDecisionObservation(
                blackboard.AlertScore,
                latest.Confidence,
                Mathf.Clamp01(
                    (currentTime - latest.OccurredAt) / 5f),
                new Vector2(
                    reportedVelocity.x / observationDistance,
                    reportedVelocity.z / observationDistance),
                latest.UncertaintyRadius / observationDistance,
                extractionActive,
                remainingTime,
                unitSummary,
                new Vector2(extractionOffset.x, extractionOffset.z),
                latest.Kind,
                latest.Source,
                pendingCommand.Kind);

            targetCandidates[0] = latest.Position;
            targetCandidates[1] =
                latest.Position +
                Vector3.ClampMagnitude(
                    reportedVelocity * predictionSeconds,
                    observationDistance * 0.5f);
            targetCandidates[2] = extractionPosition;
            targetCandidates[3] = transform.position;

            pendingSequence = commandSequence;
            pendingIssuedAt = currentTime;
            pendingPriority = Mathf.Clamp01(
                blackboard.AlertScore * 0.65f +
                latest.Confidence * 0.35f);
        }

        private void ResolveDependencies()
        {
            if (behaviorParameters == null)
                behaviorParameters = GetComponent<BehaviorParameters>();
            raidContext = raidContextComponent as IHiveRaidContext;
            fallbackPolicy = fallbackPolicyComponent as IHiveDecisionPolicy;
            if (unitRegistry == null)
                unitRegistry = FindFirstObjectByType<HiveUnitRegistry>();
        }

        private float GetCommandRadius(HiveCommandKind kind)
        {
            switch (kind)
            {
                case HiveCommandKind.Converge:
                    return convergeRadius;
                case HiveCommandKind.Investigate:
                    return investigateRadius;
                default:
                    return searchRadius;
            }
        }

        private static int GetHeuristicCommand(EnemyReportKind reportKind)
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
    }
}
