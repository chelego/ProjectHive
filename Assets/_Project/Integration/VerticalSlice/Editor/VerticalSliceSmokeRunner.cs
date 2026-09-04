#if UNITY_EDITOR
using System;
using System.Linq;
using ProjectHive.AI.Mob;
using ProjectHive.Core.Contracts;
using ProjectHive.Core.Runtime;
using ProjectHive.Gameplay.Raid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ProjectHive.Editor.Integration
{
    [InitializeOnLoad]
    public static class VerticalSliceSmokeRunner
    {
        private const string ScenePath = "Assets/_Project/Integration/VerticalSlice/Scenes/VerticalSlice.unity";
        private const string SessionKey = "ProjectHive.VerticalSliceSmoke.Active";

        private static float enteredPlayModeAt;
        private static PrototypeExtractionGate gateUnderTest;
        private static VerticalSliceRaidController controller;
        private static bool openingStarted;

        static VerticalSliceSmokeRunner()
        {
            if (SessionState.GetBool(SessionKey, false))
                Subscribe();
        }

        public static void RunFromCommandLine()
        {
            SessionState.SetBool(SessionKey, true);
            Subscribe();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void Subscribe()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(SessionKey, false))
                return;

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                enteredPlayModeAt = Time.realtimeSinceStartup;
                openingStarted = false;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
        }

        private static void Tick()
        {
            try
            {
                float elapsed = Time.realtimeSinceStartup - enteredPlayModeAt;
                if (!openingStarted && elapsed >= 1f)
                {
                    PrototypeEnemyActivator spawner = Object.FindFirstObjectByType<PrototypeEnemyActivator>();
                    if (spawner != null && !spawner.SpawnComplete && elapsed < 30f) return;
                    ValidateInitialState();
                    StartGateOpening();
                    openingStarted = true;
                    return;
                }

                if (openingStarted && gateUnderTest != null && elapsed >= gateUnderTest.OpeningDurationSeconds + 2f)
                {
                    if (gateUnderTest.State != ExtractionGateState.Open)
                        throw new InvalidOperationException($"Gate did not finish opening. Current state={gateUnderTest.State}");

                    controller.CompleteExtraction(gateUnderTest, controller.Player.gameObject);
                    if (!controller.HasEnded || !controller.WasSuccessful)
                        throw new InvalidOperationException("Extraction completion did not end the raid successfully.");

                    Debug.Log(
                        $"[VerticalSliceSmoke] PASS player=1, breckens={Object.FindFirstObjectByType<PrototypeEnemyActivator>().ActivatedEnemyCount}, bunkers=6, " +
                        $"available={controller.AvailableGates.Count}, runtimeTicks={RuntimeCoordinator.Instance.RegisteredCount}, " +
                        $"result='{controller.ResultMessage}'");
                    Finish(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void ValidateInitialState()
        {
            controller = Object.FindFirstObjectByType<VerticalSliceRaidController>();
            if (controller == null)
                throw new InvalidOperationException("VerticalSliceRaidController is missing.");
            if (controller.Player == null || !controller.Player.isActiveAndEnabled)
                throw new InvalidOperationException("Playable first-person player is missing or disabled.");

            PrototypeExtractionGate[] gates = Object.FindObjectsByType<PrototypeExtractionGate>(FindObjectsSortMode.None);
            if (gates.Length != 6)
                throw new InvalidOperationException($"Expected 6 bunkers, found {gates.Length}.");
            if (controller.AvailableGates.Count < 2 || controller.AvailableGates.Count > 3)
                throw new InvalidOperationException($"Expected 2-3 available exits, found {controller.AvailableGates.Count}.");

            BreckenAI[] breckens = Object.FindObjectsByType<BreckenAI>(FindObjectsSortMode.None);
            PrototypeEnemyActivator activator = Object.FindFirstObjectByType<PrototypeEnemyActivator>();
            if (activator == null || !activator.SpawnComplete || activator.ActivatedEnemyCount != activator.RequestedEnemyCount)
                throw new InvalidOperationException("District spawning is incomplete.");
            if (breckens.Length != activator.RequestedEnemyCount)
                throw new InvalidOperationException($"Expected {activator.RequestedEnemyCount} Breckens, found {breckens.Length}.");
            foreach (PrototypeSpawnDistrict district in activator.Districts)
                if (!activator.DistrictSpawnCounts.TryGetValue(district.DistrictName, out int count) || count != district.Population)
                    throw new InvalidOperationException($"{district.DistrictName}: district population is incomplete.");
            for (int i = 0; i < breckens.Length; i++)
            {
                NavMeshAgent agent = breckens[i].GetComponent<NavMeshAgent>();
                if (!breckens[i].isActiveAndEnabled || agent == null || !agent.enabled || !agent.isOnNavMesh)
                    throw new InvalidOperationException($"{breckens[i].name} is not active on the NavMesh.");
                Vector3 playerDelta = breckens[i].transform.position - controller.Player.transform.position;
                playerDelta.y = 0f;
                if (playerDelta.sqrMagnitude < 10f * 10f)
                    throw new InvalidOperationException($"{breckens[i].name} spawned too close to the player.");
            }
            if (RuntimeCoordinator.Instance == null || RuntimeCoordinator.Instance.RegisteredCount < activator.RequestedEnemyCount)
                throw new InvalidOperationException("Brecken runtime tick registration is incomplete.");
            if (controller.RaidClock == null || controller.RaidClock.ClockState.ElapsedSeconds <= 0f)
                throw new InvalidOperationException("Raid clock is not progressing.");
        }

        private static void StartGateOpening()
        {
            gateUnderTest = controller.AvailableGates.First();
            InteractionContext context = new InteractionContext(
                controller.Player.gameObject,
                controller.Player.transform.position,
                controller.Player.transform.forward);
            if (!gateUnderTest.TryBeginOpening(in context))
                throw new InvalidOperationException("Available extraction gate rejected opening.");
        }

        private static void Finish(int exitCode)
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SessionState.SetBool(SessionKey, false);
            EditorApplication.Exit(exitCode);
        }
    }
}
#endif
