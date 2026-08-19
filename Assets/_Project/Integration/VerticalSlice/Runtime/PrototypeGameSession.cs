using System.Collections;
using ProjectHive.Core;
using ProjectHive.Core.Flow;
using ProjectHive.Data.Runs;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectHive.Integration.VerticalSlice
{
    [DefaultExecutionOrder(-700)]
    [DisallowMultipleComponent]
    public sealed class PrototypeGameSession : MonoBehaviour
    {
        public const string FrontEndSceneName = "PrototypeFrontEnd";
        public const string RaidSceneName = "VerticalSlice";
        public const string DefaultMapId = "city.prototype";

        [SerializeField, Min(0f)] private float raidResultHoldSeconds = 2f;

        private Coroutine transitionRoutine;
        private GameFlowManager gameFlow;
        private RunResult lastResult;
        private string lastResultMessage = string.Empty;
        private string selectedMapId = DefaultMapId;
        private bool resultPending;

        public static PrototypeGameSession Instance { get; private set; }

        public bool IsLoading { get; private set; }
        public float LoadingProgress { get; private set; }
        public string LoadingLabel { get; private set; } = string.Empty;
        public string SelectedMapId => selectedMapId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 90;
            gameFlow = GetComponent<GameFlowManager>();
            if (gameFlow == null)
                gameFlow = GameFlowManager.Instance;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool StartRaid(string mapId)
        {
            if (IsLoading || transitionRoutine != null)
                return false;

            selectedMapId = string.IsNullOrWhiteSpace(mapId) ? DefaultMapId : mapId.Trim();
            GameFlowManager flow = ResolveGameFlow();
            if (flow == null)
            {
                Debug.LogError("[PrototypeFlow] GameFlowManager를 찾지 못했습니다.", this);
                return false;
            }

            if (flow.CurrentState != GameState.Shelter && !flow.EnterShelter("Preparing next surface run"))
                return false;
            if (!flow.BeginRaidLoading(selectedMapId))
                return false;

            transitionRoutine = StartCoroutine(LoadSceneRoutine(RaidSceneName, true));
            return true;
        }

        public bool ResolveRaid(bool success, bool timeExpired, string message)
        {
            if (transitionRoutine != null)
                return false;

            GameFlowManager flow = ResolveGameFlow();
            float duration = flow != null ? flow.CurrentRaidDuration : 0f;
            RunOutcome outcome = success
                ? RunOutcome.Extracted
                : timeExpired ? RunOutcome.TimeExpired : RunOutcome.Dead;
            RunResult result = new RunResult(outcome, selectedMapId, duration);

            if (flow != null)
            {
                if (success)
                {
                    if (flow.CurrentState == GameState.Raid)
                        flow.BeginExtraction(message);
                    if (flow.CurrentState == GameState.Extracting)
                        flow.CompleteExtraction(result);
                }
                else
                {
                    flow.RegisterDeath(result);
                }
            }

            lastResult = result;
            lastResultMessage = message ?? string.Empty;
            resultPending = true;
            transitionRoutine = StartCoroutine(ReturnToFrontEndAfterResult());
            return true;
        }

        public bool TryConsumeResult(out RunResult result, out string message)
        {
            result = lastResult;
            message = lastResultMessage;
            if (!resultPending || result == null)
                return false;

            resultPending = false;
            return true;
        }

        public void ReturnToTitle()
        {
            if (IsLoading || transitionRoutine != null)
                return;

            transitionRoutine = StartCoroutine(LoadSceneRoutine(FrontEndSceneName, false));
        }

        private IEnumerator ReturnToFrontEndAfterResult()
        {
            yield return new WaitForSecondsRealtime(raidResultHoldSeconds);
            transitionRoutine = null;
            transitionRoutine = StartCoroutine(LoadSceneRoutine(FrontEndSceneName, false));
        }

        private IEnumerator LoadSceneRoutine(string sceneName, bool enteringRaid)
        {
            IsLoading = true;
            LoadingProgress = 0f;
            LoadingLabel = enteringRaid ? "지상으로 이동하는 중" : "은신처로 돌아가는 중";
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (!enteringRaid && GameRuntime.Instance != null)
                GameRuntime.Instance.enabled = false;

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"[PrototypeFlow] 씬을 불러오지 못했습니다: {sceneName}", this);
                ResetTransitionState();
                yield break;
            }

            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
            {
                LoadingProgress = Mathf.Clamp01(operation.progress / 0.9f);
                yield return null;
            }

            LoadingProgress = 1f;
            yield return new WaitForSecondsRealtime(0.15f);
            operation.allowSceneActivation = true;
            while (!operation.isDone)
                yield return null;

            GameFlowManager flow = ResolveGameFlow();
            if (enteringRaid)
            {
                if (GameRuntime.Instance != null)
                    GameRuntime.Instance.enabled = true;
                flow?.EnterRaid();
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                if (GameRuntime.Instance != null)
                    GameRuntime.Instance.enabled = false;
                flow?.EnterShelter("Returned from surface run");
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            ResetTransitionState();
        }

        private GameFlowManager ResolveGameFlow()
        {
            if (gameFlow == null)
                gameFlow = GameFlowManager.Instance;
            if (gameFlow == null)
                gameFlow = GetComponent<GameFlowManager>();
            return gameFlow;
        }

        private void ResetTransitionState()
        {
            IsLoading = false;
            LoadingProgress = 0f;
            LoadingLabel = string.Empty;
            transitionRoutine = null;
        }

        private void OnGUI()
        {
            if (!IsLoading)
                return;

            Color previousColor = GUI.color;
            GUI.color = new Color(0.015f, 0.02f, 0.025f, 0.98f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

            float width = Mathf.Min(620f, Screen.width * 0.62f);
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.72f;
            GUI.color = new Color(0.12f, 0.14f, 0.15f, 1f);
            GUI.DrawTexture(new Rect(x, y, width, 8f), Texture2D.whiteTexture);
            GUI.color = new Color(0.78f, 0.83f, 0.82f, 1f);
            GUI.DrawTexture(new Rect(x, y, width * LoadingProgress, 8f), Texture2D.whiteTexture);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.height / 38, 18, 32),
                normal = { textColor = new Color(0.88f, 0.9f, 0.88f, 1f) }
            };
            GUI.Label(new Rect(x, y - 58f, width, 42f), LoadingLabel, style);
            GUI.color = previousColor;
        }

        private void OnValidate()
        {
            raidResultHoldSeconds = Mathf.Max(0f, raidResultHoldSeconds);
        }
    }
}
