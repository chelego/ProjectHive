using ProjectHive.Core.Events;
using ProjectHive.Core.Runtime;
using UnityEngine;

namespace ProjectHive.AI.Hive
{
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class HiveDirector : MonoBehaviour, IRuntimeTickable
    {
        [Header("Services")]
        [SerializeField] private GameEventBus eventBus;
        [SerializeField] private RuntimeCoordinator runtimeCoordinator;
        [SerializeField] private MonoBehaviour decisionPolicyComponent;

        [Header("Blackboard")]
        [SerializeField, Min(4)] private int reportCapacity = 64;
        [SerializeField, Min(0f)] private float alertDecayPerSecond = 0.025f;
        [SerializeField, Min(0.02f)] private float tickInterval = 0.1f;
        [SerializeField, Range(0f, 1f)] private float minimumHiveNoiseLoudness = 0.25f;

        private HiveBlackboard blackboard;
        private IHiveDecisionPolicy decisionPolicy;
        private int nextCommandSequence = 1;

        public HiveBlackboard Blackboard => blackboard;
        public bool RuntimeTickEnabled => isActiveAndEnabled;
        public Transform RuntimeTransform => transform;
        public float MinimumTickInterval => tickInterval;
        public bool UseDistanceScaling => false;

        public void Configure(
            GameEventBus bus,
            RuntimeCoordinator coordinator,
            MonoBehaviour policyComponent)
        {
            eventBus = bus;
            runtimeCoordinator = coordinator;
            decisionPolicyComponent = policyComponent;
            decisionPolicy = policyComponent as IHiveDecisionPolicy;
        }

        private void Awake()
        {
            blackboard = new HiveBlackboard(reportCapacity);
            decisionPolicy = decisionPolicyComponent as IHiveDecisionPolicy;

            if (decisionPolicy == null)
                decisionPolicy = GetComponent<IHiveDecisionPolicy>();
        }

        private void OnEnable()
        {
            ResolveServices();

            if (eventBus != null)
            {
                eventBus.NoiseEmitted += OnNoiseEmitted;
                eventBus.EnemyReported += OnEnemyReported;
            }

            runtimeCoordinator?.Register(this);
        }

        private void OnDisable()
        {
            if (eventBus != null)
            {
                eventBus.NoiseEmitted -= OnNoiseEmitted;
                eventBus.EnemyReported -= OnEnemyReported;
            }

            runtimeCoordinator?.Unregister(this);
        }

        public void RuntimeTick(in RuntimeTickContext context)
        {
            if (blackboard == null)
                return;

            blackboard.DecayAlert(alertDecayPerSecond * context.DeltaTime);

            if (decisionPolicy == null ||
                !decisionPolicy.TryCreateCommand(
                    blackboard,
                    context.CurrentTime,
                    nextCommandSequence,
                    out HiveCommand command))
            {
                return;
            }

            nextCommandSequence++;
            eventBus?.PublishHiveCommand(in command);
        }

        public void SubmitReport(in EnemyReport report)
        {
            blackboard?.Record(in report);
        }

        private void OnEnemyReported(in EnemyReport report)
        {
            SubmitReport(in report);
        }

        private void OnNoiseEmitted(in NoiseEvent noiseEvent)
        {
            if (noiseEvent.Loudness < minimumHiveNoiseLoudness ||
                noiseEvent.Affiliation == NoiseAffiliation.Monster)
            {
                return;
            }

            if (HiveReportFactory.TryCreateFromNoise(in noiseEvent, out EnemyReport report))
                blackboard?.Record(in report);
        }

        private void ResolveServices()
        {
            if (eventBus == null)
                eventBus = GameEventBus.Instance;
            if (runtimeCoordinator == null)
                runtimeCoordinator = RuntimeCoordinator.Instance;
            if (decisionPolicy == null)
                decisionPolicy = decisionPolicyComponent as IHiveDecisionPolicy;
        }
    }
}
