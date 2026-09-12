using System;
using System.Collections.Generic;
using ProjectHive.Combat;
using ProjectHive.Core.Contracts;
using ProjectHive.Core.Runtime;
using ProjectHive.Integration.VerticalSlice;
using ProjectHive.Interaction;
using ProjectHive.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ProjectHive.Gameplay.Raid
{
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class VerticalSliceRaidController : MonoBehaviour
    {
        [Header("Prototype raid")]
        [SerializeField, Min(10f)] private float raidDurationSeconds = 1800f;
        [SerializeField, Min(0.1f)] private float bunkerOpeningSeconds = 8f;
        [SerializeField, Range(1, 5)] private int minimumAvailableExits = 2;
        [SerializeField, Range(1, 5)] private int maximumAvailableExits = 3;

        [Header("Scene references")]
        [SerializeField] private RaidClock raidClock;
        [SerializeField] private FirstPersonMotor player;
        [SerializeField] private RuntimeCoordinator runtimeCoordinator;
        [SerializeField] private Light sunriseLight;
        [SerializeField] private PrototypeExtractionGate[] gates = Array.Empty<PrototypeExtractionGate>();

        private readonly List<PrototypeExtractionGate> availableGates = new List<PrototypeExtractionGate>(5);
        private PrototypeExtractionGate entryGate;
        private bool ended;
        private bool sunriseActive;
        private float sunriseElapsed;

        public RaidClock RaidClock => raidClock;
        public FirstPersonMotor Player => player;
        public PrototypeExtractionGate EntryGate => entryGate;
        public IReadOnlyList<PrototypeExtractionGate> AvailableGates => availableGates;
        public bool HasEnded => ended;
        public bool WasSuccessful { get; private set; }
        public string ResultMessage { get; private set; } = string.Empty;

        public void Configure(
            RaidClock clock,
            FirstPersonMotor playerMotor,
            RuntimeCoordinator coordinator,
            Light sceneSunriseLight,
            PrototypeExtractionGate[] extractionGates)
        {
            raidClock = clock;
            player = playerMotor;
            runtimeCoordinator = coordinator;
            sunriseLight = sceneSunriseLight;
            gates = extractionGates ?? Array.Empty<PrototypeExtractionGate>();
        }

        private void Start()
        {
            if (raidClock == null)
                raidClock = FindFirstObjectByType<RaidClock>();
            if (player == null)
                player = FindFirstObjectByType<FirstPersonMotor>();
            if (runtimeCoordinator == null)
                runtimeCoordinator = RuntimeCoordinator.Instance;
            if (gates == null || gates.Length == 0)
                gates = FindObjectsByType<PrototypeExtractionGate>(FindObjectsSortMode.None);

            if (raidClock != null)
                raidClock.Configure(raidDurationSeconds, 23, 5);

            runtimeCoordinator?.SetObserver(player != null ? player.transform : null);
            InitializeGates();
            SpawnPlayerAtEntryGate();

            if (sunriseLight != null)
            {
                sunriseLight.intensity = 0f;
                sunriseLight.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!ended && raidClock != null && raidClock.IsExpired)
                EndRaid(false, "05:00 — 햇빛에 노출되어 사망");

            if (!ended && player != null)
            {
                Health health = player.GetComponent<Health>();
                if (health != null && health.IsDead)
                    EndRaid(false, "사망 — 소지품 손실 처리 대상");
            }

            if (sunriseActive && sunriseLight != null)
            {
                sunriseElapsed += Time.deltaTime;
                sunriseLight.intensity = Mathf.Lerp(0f, 12f, Mathf.Clamp01(sunriseElapsed / 3f));
            }

            if (ended && PrototypeGameSession.Instance == null &&
                Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void InitializeGates()
        {
            availableGates.Clear();
            if (gates == null || gates.Length == 0)
            {
                Debug.LogError("[VerticalSlice] 벙커 마커를 찾지 못했습니다.", this);
                return;
            }

            entryGate = gates[UnityEngine.Random.Range(0, gates.Length)];
            List<PrototypeExtractionGate> candidates = new List<PrototypeExtractionGate>(gates.Length - 1);
            for (int i = 0; i < gates.Length; i++)
            {
                PrototypeExtractionGate gate = gates[i];
                gate.Configure(this, $"Bunker-{i + 1:00}", bunkerOpeningSeconds);
                if (gate != entryGate)
                    candidates.Add(gate);
            }

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                (candidates[i], candidates[swapIndex]) = (candidates[swapIndex], candidates[i]);
            }

            int max = Mathf.Clamp(maximumAvailableExits, 1, candidates.Count);
            int min = Mathf.Clamp(minimumAvailableExits, 1, max);
            int availableCount = UnityEngine.Random.Range(min, max + 1);

            for (int i = 0; i < gates.Length; i++)
            {
                PrototypeExtractionGate gate = gates[i];
                bool available = candidates.IndexOf(gate) >= 0 && candidates.IndexOf(gate) < availableCount;
                gate.ResetForRaid(gate == entryGate, available);
                if (available)
                    availableGates.Add(gate);
            }

            Debug.Log($"[VerticalSlice] 시작 벙커={entryGate.GateId}, 탈출 가능 벙커={availableGates.Count}", this);
        }

        private void SpawnPlayerAtEntryGate()
        {
            if (player == null || entryGate == null)
                return;

            Collider gateCollider = entryGate.GetComponent<Collider>();
            float distance = 4f;
            if (gateCollider != null)
                distance = Mathf.Max(distance, Mathf.Max(gateCollider.bounds.extents.x, gateCollider.bounds.extents.z) + 2f);

            Vector3 desired = entryGate.transform.position + entryGate.transform.forward * distance + Vector3.up * 1.5f;
            if (Physics.Raycast(desired + Vector3.up * 100f, Vector3.down, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore))
                desired.y = hit.point.y + 0.1f;

            CharacterController characterController = player.GetComponent<CharacterController>();
            if (characterController != null)
                characterController.enabled = false;
            player.transform.SetPositionAndRotation(desired, entryGate.transform.rotation);
            if (characterController != null)
                characterController.enabled = true;
        }

        public void CompleteExtraction(PrototypeExtractionGate gate, GameObject extractingPlayer)
        {
            if (ended || gate == null || gate.IsEntryOnly || gate.State != ExtractionGateState.Open)
                return;
            if (player == null || extractingPlayer != player.gameObject)
                return;

            EndRaid(true, $"생환 성공 — {gate.GateId}");
        }

        public void NotifyGateChanged(PrototypeExtractionGate gate)
        {
            if (gate != null)
                Debug.Log($"[VerticalSlice] {gate.GateId}: {gate.State} ({gate.OpeningProgress:P0})", gate);
        }

        private void EndRaid(bool success, string message)
        {
            if (ended)
                return;

            ended = true;
            WasSuccessful = success;
            ResultMessage = message;

            if (!success && raidClock != null && raidClock.IsExpired && sunriseLight != null)
            {
                sunriseLight.gameObject.SetActive(true);
                sunriseActive = true;
                sunriseElapsed = 0f;
            }

            if (player != null)
            {
                player.enabled = false;
                PlayerCombatController combat = player.GetComponent<PlayerCombatController>();
                if (combat != null)
                    combat.enabled = false;
                PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
                if (interactor != null)
                    interactor.enabled = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log($"[VerticalSlice] Raid ended: success={success}, result={message}", this);

            PrototypeGameSession session = PrototypeGameSession.Instance;
            if (session != null)
            {
                bool timeExpired = !success && raidClock != null && raidClock.IsExpired;
                session.ResolveRaid(success, timeExpired, message);
            }
        }

        private void OnValidate()
        {
            raidDurationSeconds = Mathf.Max(10f, raidDurationSeconds);
            bunkerOpeningSeconds = Mathf.Max(0.1f, bunkerOpeningSeconds);
            maximumAvailableExits = Mathf.Max(minimumAvailableExits, maximumAvailableExits);
        }
    }
}
