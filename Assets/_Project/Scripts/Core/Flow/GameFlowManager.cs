using ProjectHive.Core.Events;
using ProjectHive.Data.Runs;
using UnityEngine;

namespace ProjectHive.Core.Flow
{
    [DefaultExecutionOrder(-800)]
    [DisallowMultipleComponent]
    public sealed class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [SerializeField] private GameEventBus eventBus;
        [SerializeField] private GameState currentState = GameState.Boot;
        [SerializeField] private string currentMapId = string.Empty;

        private float raidStartedAt;

        public GameState CurrentState => currentState;
        public string CurrentMapId => currentMapId;
        public float CurrentRaidDuration =>
            IsRaidState(currentState) ? Mathf.Max(0f, Time.time - raidStartedAt) : 0f;

        public void Configure(GameEventBus bus)
        {
            eventBus = bus;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (eventBus == null)
                eventBus = GameEventBus.Instance;
        }

        private void Start()
        {
            if (currentState == GameState.Boot)
                EnterShelter("Initial boot completed");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool EnterShelter(string reason = "Returned to shelter")
        {
            if (currentState == GameState.LoadingRaid ||
                currentState == GameState.Raid ||
                currentState == GameState.Extracting)
            {
                return false;
            }

            currentMapId = string.Empty;
            raidStartedAt = 0f;
            return TransitionTo(GameState.Shelter, reason);
        }

        public bool BeginRaidLoading(string mapId)
        {
            if (currentState != GameState.Shelter || string.IsNullOrWhiteSpace(mapId))
                return false;

            currentMapId = mapId.Trim();
            return TransitionTo(GameState.LoadingRaid, "Raid loading started");
        }

        public bool EnterRaid()
        {
            if (currentState != GameState.LoadingRaid)
                return false;

            raidStartedAt = Time.time;
            return TransitionTo(GameState.Raid, "Raid scene ready");
        }

        public bool BeginExtraction(string extractionId)
        {
            if (currentState != GameState.Raid)
                return false;

            string reason = string.IsNullOrWhiteSpace(extractionId)
                ? "Extraction started"
                : "Extraction started: " + extractionId.Trim();
            return TransitionTo(GameState.Extracting, reason);
        }

        public bool CancelExtraction(string reason = "Extraction interrupted")
        {
            return currentState == GameState.Extracting &&
                   TransitionTo(GameState.Raid, reason);
        }

        public bool CompleteExtraction(RunResult result)
        {
            if (currentState != GameState.Extracting || result == null)
                return false;

            bool changed = TransitionTo(GameState.Extracted, "Extraction completed");
            if (changed)
                ResolveEventBus()?.PublishRunResult(result);
            return changed;
        }

        public bool RegisterDeath(RunResult result)
        {
            if (!IsRaidState(currentState) || result == null)
                return false;

            bool changed = TransitionTo(GameState.Dead, "Player died");
            if (changed)
                ResolveEventBus()?.PublishRunResult(result);
            return changed;
        }

        private bool TransitionTo(GameState next, string reason)
        {
            if (currentState == next)
                return false;

            GameState previous = currentState;
            currentState = next;

            GameStateTransition transition = new GameStateTransition(
                previous,
                next,
                reason,
                Time.time);
            ResolveEventBus()?.PublishGameState(in transition);
            return true;
        }

        private GameEventBus ResolveEventBus()
        {
            if (eventBus == null)
                eventBus = GameEventBus.Instance;
            return eventBus;
        }

        private static bool IsRaidState(GameState state)
        {
            return state == GameState.Raid || state == GameState.Extracting;
        }
    }
}
