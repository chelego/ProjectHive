using System.Collections;
using ProjectHive.AI.Hive;
using ProjectHive.AI.Mob;
using ProjectHive.Combat;
using ProjectHive.Core.Events;
using ProjectHive.Core.Runtime;
using ProjectHive.Diagnostics;
using ProjectHive.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ProjectHive.Gameplay.Raid
{
    // Scene-specific additive runtime composition: authoring map and frontend remain intact.
    public sealed class RaidEncounterBootstrap : MonoBehaviour
    {
        private const string RaidScene = "Assets/_Project/Integration/VerticalSlice/Scenes/VerticalSlice.unity";
        private const string MapScene = "Assets/_Project/Scenes/TerrainBlockout.unity";
        public int SpawnedFlocks { get; private set; }
        public int SpawnedSpotters { get; private set; }
        public bool Ready { get; private set; }
        private NavMeshDataInstance previewNavigation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSubscriptions() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Subscribe() { SceneManager.sceneLoaded += OnSceneLoaded; }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != RaidScene && scene.path != MapScene) return;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponent<RaidEncounterBootstrap>() != null) return;
            var rootObject = new GameObject("_RaidEncounters_Runtime");
            SceneManager.MoveGameObjectToScene(rootObject, scene);
            rootObject.AddComponent<RaidEncounterBootstrap>();
        }

        private IEnumerator Start()
        {
            FirstPersonMotor player = null;
            for (int i = 0; i < 120 && player == null; i++)
            { player = FindFirstObjectByType<FirstPersonMotor>(); if (player == null) yield return null; }
            if (player == null) { Debug.LogError("[RaidEncounters] No player in encounter scene."); yield break; }
            yield return null; // Raid controller first places the player at the entry bunker.
            if (player.GetComponent<Health>() == null) player.gameObject.AddComponent<Health>();
            if (GameEventBus.Instance == null) new GameObject("_EncounterEventBus").AddComponent<GameEventBus>();
            if (RuntimeCoordinator.Instance == null) new GameObject("_EncounterRuntimeCoordinator").AddComponent<RuntimeCoordinator>();
            RuntimeCoordinator.Instance.SetObserver(player.transform);
            var registry = FindFirstObjectByType<HiveUnitRegistry>();
            if (registry == null) registry = gameObject.AddComponent<HiveUnitRegistry>();
            if (FindFirstObjectByType<HiveDirector>() == null)
            {
                var policy = gameObject.AddComponent<RuleBasedHivePolicy>();
                var director = gameObject.AddComponent<HiveDirector>();
                director.Configure(GameEventBus.Instance, RuntimeCoordinator.Instance, policy);
            }
            if (FindFirstObjectByType<HiveCommandDispatcher>() == null)
                gameObject.AddComponent<HiveCommandDispatcher>().Configure(GameEventBus.Instance, registry);

            foreach (BreckenAI enemy in FindObjectsByType<BreckenAI>(FindObjectsSortMode.None)) enemy.ConfigureEncounter(player.transform);
            var diagnostics = gameObject.AddComponent<RaidDeveloperMode>();
            diagnostics.Configure(player);
            RaidEncounterLayout layout = Resources.Load<RaidEncounterLayout>("HiveEncounters/RaidEncounterLayout");
            if (layout == null) { Debug.LogError("[RaidEncounters] Missing authored encounter layout."); yield break; }
            if (gameObject.scene.path == MapScene && layout.mapNavMesh != null && NavMesh.CalculateTriangulation().indices.Length == 0)
                previewNavigation = NavMesh.AddNavMeshData(layout.mapNavMesh, layout.navMeshPosition, layout.navMeshRotation);

            for (int i = 0; i < layout.outdoorPoints.Length; i++)
            {
                Vector3 point = layout.outdoorPoints[i];
                if (Vector3.Distance(point, player.transform.position) < 18f) continue;
                if (!HasOutdoorClearance(point, 2.5f)) continue;
                var go = new GameObject("BirdFlock_" + (i + 1).ToString("00"));
                go.transform.SetParent(transform, false); go.transform.position = point;
                var flock = go.AddComponent<MonsterBirdFlock>();
                AudioClip call = layout.birdCalls.Length > 0 ? layout.birdCalls[i % layout.birdCalls.Length] : null;
                flock.Configure(player, layout.birdPrefab, call, layout.birdsPerFlock);
                SpawnedFlocks++;
                if (i % 4 == 0) yield return null;
            }
            for (int i = 0; i < layout.patrolCenters.Length; i++)
            {
                Vector3 center = layout.patrolCenters[i];
                if (!HasOutdoorClearance(center, 1f) || Vector3.Distance(center, player.transform.position) < 30f) continue;
                GameObject go = new GameObject();
                go.name = "Spotter_" + (i + 1).ToString("00");
                go.transform.SetParent(transform, false);
                go.transform.position = center + Vector3.up * 24f;
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(go.transform, false);
                body.transform.localScale = new Vector3(1.3f, 0.7f, 1.3f);
                body.GetComponent<Renderer>().sharedMaterial = layout.spotterMaterial;
                go.AddComponent<Health>();
                go.AddComponent<SpotterAI>().Configure(player.transform, center, layout.beamMaterial);
                SpawnedSpotters++;
            }
            // Existing VerticalSlice district activator owns its Brecken population.
            if (FindFirstObjectByType<PrototypeEnemyActivator>() == null && layout.breckenPrefab != null)
            {
                int spawned = 0;
                foreach (Vector3 point in layout.outdoorPoints)
                {
                    if (spawned >= 14 || Vector3.Distance(point, player.transform.position) < 22f) continue;
                    if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;
                    GameObject go = Instantiate(layout.breckenPrefab, hit.position, Quaternion.identity, transform);
                    go.name = "MapPreview_Brecken_" + (++spawned).ToString("00");
                    go.SetActive(true);
                    go.GetComponent<BreckenAI>().ConfigureEncounter(player.transform);
                    yield return null;
                }
            }
            Ready = true;
            Debug.Log($"[RaidEncounters] Ready: {SpawnedFlocks} bird flocks, {SpawnedSpotters} spotters; Hive + F5 active.");
        }

        public static bool HasOutdoorClearance(Vector3 ground, float radius)
        {
            if (Physics.CheckSphere(ground + Vector3.up * (radius + 0.2f), radius, ~0, QueryTriggerInteraction.Ignore)) return false;
            return !Physics.Raycast(ground + Vector3.up * 0.5f, Vector3.up, 90f, ~0, QueryTriggerInteraction.Ignore);
        }
        private void OnDestroy() { if (previewNavigation.valid) previewNavigation.Remove(); }
    }
}
