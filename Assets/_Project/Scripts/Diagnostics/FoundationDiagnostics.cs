using System.Collections;
using ProjectHive.AI.Hive;
using ProjectHive.Core.Events;
using ProjectHive.Core.Flow;
using ProjectHive.Core.Runtime;
using UnityEngine;

namespace ProjectHive.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class FoundationDiagnostics : MonoBehaviour
    {
        [SerializeField] private GameEventBus eventBus;
        [SerializeField] private GameFlowManager gameFlow;
        [SerializeField] private RuntimeCoordinator runtimeCoordinator;
        [SerializeField] private HiveDirector hiveDirector;
        [SerializeField] private bool runSmokeTestOnStart = true;

        private int hiveCommandsReceived;
        private HiveCommand lastCommand;

        public string LastSmokeTestResult { get; private set; } = "Not run";
        public int HiveCommandsReceived => hiveCommandsReceived;
        public HiveCommand LastCommand => lastCommand;

        public void Configure(
            GameEventBus bus,
            GameFlowManager flow,
            RuntimeCoordinator coordinator,
            HiveDirector director)
        {
            eventBus = bus;
            gameFlow = flow;
            runtimeCoordinator = coordinator;
            hiveDirector = director;
        }

        private void Awake()
        {
            ResolveServices();
        }

        private void OnEnable()
        {
            ResolveServices();
            if (eventBus != null)
                eventBus.HiveCommandIssued += OnHiveCommandIssued;
        }

        private void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (runSmokeTestOnStart)
                StartCoroutine(RunSmokeTest());
#endif
        }

        private void OnDisable()
        {
            if (eventBus != null)
                eventBus.HiveCommandIssued -= OnHiveCommandIssued;
        }

        public IEnumerator RunSmokeTest()
        {
            ResolveServices();
            hiveCommandsReceived = 0;

            if (eventBus == null ||
                gameFlow == null ||
                runtimeCoordinator == null ||
                hiveDirector == null)
            {
                LastSmokeTestResult = "FAILED: foundation service missing";
                Debug.LogError("[FoundationDiagnostics] " + LastSmokeTestResult, this);
                yield break;
            }

            if (gameFlow.CurrentState != GameState.Shelter)
                gameFlow.EnterShelter("Foundation smoke test reset");

            bool loadingStarted = gameFlow.BeginRaidLoading("prototype.city");
            bool raidEntered = gameFlow.EnterRaid();

            NoiseEvent noiseEvent = new NoiseEvent(
                new Vector3(4f, 0f, 6f),
                0.8f,
                35f,
                NoiseCategory.Gunshot,
                NoiseAffiliation.Player,
                gameObject.GetInstanceID(),
                Time.time);
            eventBus.PublishNoise(in noiseEvent);

            EnemyReport visualReport = new EnemyReport(
                EnemyReportKind.VisualContact,
                new Vector3(8f, 0f, 12f),
                1f,
                gameObject.GetInstanceID(),
                Time.time);
            eventBus.PublishEnemyReport(in visualReport);

            yield return new WaitForSeconds(0.55f);

            bool stateValid = loadingStarted &&
                              raidEntered &&
                              gameFlow.CurrentState == GameState.Raid;
            bool commandValid = hiveCommandsReceived > 0 &&
                                lastCommand.Kind == HiveCommandKind.Converge;
            bool schedulerValid = runtimeCoordinator.RegisteredCount > 0;
            bool blackboardValid = hiveDirector.Blackboard != null &&
                                   hiveDirector.Blackboard.ReportCount >= 2;

            bool success = stateValid && commandValid && schedulerValid && blackboardValid;
            LastSmokeTestResult = success
                ? "PASSED: flow, events, scheduler, blackboard, and Hive command"
                : "FAILED: state=" + stateValid +
                  ", command=" + commandValid +
                  ", scheduler=" + schedulerValid +
                  ", blackboard=" + blackboardValid;

            if (success)
                Debug.Log("[FoundationDiagnostics] " + LastSmokeTestResult, this);
            else
                Debug.LogError("[FoundationDiagnostics] " + LastSmokeTestResult, this);
        }

        private void OnHiveCommandIssued(in HiveCommand command)
        {
            hiveCommandsReceived++;
            lastCommand = command;
        }

        private void ResolveServices()
        {
            if (eventBus == null)
                eventBus = GameEventBus.Instance;
            if (gameFlow == null)
                gameFlow = GameFlowManager.Instance;
            if (runtimeCoordinator == null)
                runtimeCoordinator = RuntimeCoordinator.Instance;
            if (hiveDirector == null)
                hiveDirector = FindFirstObjectByType<HiveDirector>();
        }
    }
}
