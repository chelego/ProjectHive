using ProjectHive.Core.Events;
using ProjectHive.Core.Runtime;
using UnityEngine;

namespace ProjectHive.AI.Hive.Debugging
{
    [DisallowMultipleComponent]
    public sealed class HiveSandboxReportSource : MonoBehaviour, IRuntimeTickable
    {
        private static readonly EnemyReportKind[] ReportSequence =
        {
            EnemyReportKind.Noise,
            EnemyReportKind.VisualContact,
            EnemyReportKind.LostTarget,
            EnemyReportKind.SpotterContact,
            EnemyReportKind.ExtractionActivity
        };

        [Header("Services")]
        [SerializeField] private GameEventBus eventBus;
        [SerializeField] private RuntimeCoordinator runtimeCoordinator;

        [Header("Sequence")]
        [SerializeField] private bool autoEmit = true;
        [SerializeField, Min(0.25f)] private float reportInterval = 2f;
        [SerializeField, Min(1f)] private float patternRadius = 12f;
        [SerializeField, Range(0f, 1f)] private float minimumConfidence = 0.45f;
        [SerializeField, Range(0f, 1f)] private float maximumConfidence = 0.95f;

        private int sequenceIndex;
        private float nextReportTime;
        private bool previousRunInBackground;

        public bool RuntimeTickEnabled => isActiveAndEnabled && autoEmit;
        public Transform RuntimeTransform => transform;
        public float MinimumTickInterval => 0.1f;
        public bool UseDistanceScaling => false;

        public void Configure(GameEventBus bus, RuntimeCoordinator coordinator)
        {
            eventBus = bus;
            runtimeCoordinator = coordinator;
        }

        private void OnEnable()
        {
            ResolveServices();
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            nextReportTime = Time.time + reportInterval;
            runtimeCoordinator?.Register(this);
        }

        private void OnDisable()
        {
            runtimeCoordinator?.Unregister(this);
            Application.runInBackground = previousRunInBackground;
        }

        public void RuntimeTick(in RuntimeTickContext context)
        {
            if (context.CurrentTime < nextReportTime)
                return;

            EmitNextReport(context.CurrentTime);
            nextReportTime = context.CurrentTime + reportInterval;
        }

        [ContextMenu("Emit Next Hive Report")]
        public void EmitNextReport()
        {
            EmitNextReport(Time.time);
            nextReportTime = Time.time + reportInterval;
        }

        private void EmitNextReport(float occurredAt)
        {
            ResolveServices();
            if (eventBus == null)
                return;

            EnemyReportKind kind = ReportSequence[sequenceIndex % ReportSequence.Length];
            float angle = sequenceIndex * Mathf.PI * 0.4f;
            Vector3 position = transform.position + new Vector3(
                Mathf.Cos(angle) * patternRadius,
                0f,
                Mathf.Sin(angle) * patternRadius);
            float confidence = Mathf.Lerp(
                minimumConfidence,
                maximumConfidence,
                (sequenceIndex % ReportSequence.Length) /
                (float)(ReportSequence.Length - 1));

            if (kind == EnemyReportKind.Noise)
            {
                NoiseEvent noiseEvent = new NoiseEvent(
                    position,
                    confidence,
                    patternRadius,
                    NoiseCategory.Gunshot,
                    NoiseAffiliation.Player,
                    gameObject.GetInstanceID(),
                    occurredAt);
                eventBus.PublishNoise(in noiseEvent);
            }
            else
            {
                EnemyReport report = new EnemyReport(
                    kind,
                    position,
                    confidence,
                    gameObject.GetInstanceID(),
                    occurredAt);
                eventBus.PublishEnemyReport(in report);
            }

            sequenceIndex++;
        }

        private void ResolveServices()
        {
            if (eventBus == null)
                eventBus = GameEventBus.Instance;
            if (runtimeCoordinator == null)
                runtimeCoordinator = RuntimeCoordinator.Instance;
        }
    }
}
