#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using ProjectHive.AI.Mob;
using ProjectHive.Combat;
using ProjectHive.Core;
using ProjectHive.Core.Runtime;
using ProjectHive.Gameplay.Raid;
using ProjectHive.Interaction;
using ProjectHive.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectHive.Editor.Integration
{
    public static class VerticalSliceSceneBuilder
    {
        private const string MapScenePath = "Assets/_Project/Scenes/TerrainBlockout.unity";
        private const string PlayerScenePath = "Assets/Scenes/SampleScene.unity";
        private const string MobScenePath = "Assets/_Project/Scenes/Mob_AI_JH.unity";
        private const string OutputScenePath = "Assets/_Project/Integration/VerticalSlice/Scenes/VerticalSlice.unity";
        private const string NavMeshFolderPath = "Assets/_Project/Integration/VerticalSlice/Scenes/VerticalSlice";
        private const string NavMeshDataPath = NavMeshFolderPath + "/NavMesh-Raid.asset";
        private const string RuntimeBudgetPath = "Assets/_Project/Data/Runtime/RuntimeBudgetSettings.asset";

        [MenuItem("Project Hive/Integration/Build Vertical Slice")]
        public static void Build()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            Scene mapScene = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
            if (!mapScene.IsValid())
                throw new InvalidOperationException($"Map scene could not be opened: {MapScenePath}");

            if (!EditorSceneManager.SaveScene(mapScene, OutputScenePath, true))
                throw new InvalidOperationException($"Vertical slice scene could not be created: {OutputScenePath}");

            Scene targetScene = EditorSceneManager.OpenScene(OutputScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(targetScene);
            DisableMapOverviewCamera(targetScene);

            FirstPersonMotor player = ClonePlayer(targetScene);
            List<PrototypeExtractionGate> gates = ConfigureBunkers(targetScene);

            GameObject runtimeRoot = new GameObject("_VerticalSliceRuntime");
            SceneManager.MoveGameObjectToScene(runtimeRoot, targetScene);

            GameRuntime gameRuntime = runtimeRoot.AddComponent<GameRuntime>();
            SerializedObject gameRuntimeSerialized = new SerializedObject(gameRuntime);
            gameRuntimeSerialized.FindProperty("targetFrameRate").intValue = 90;
            gameRuntimeSerialized.ApplyModifiedPropertiesWithoutUndo();

            RuntimeCoordinator coordinator = runtimeRoot.AddComponent<RuntimeCoordinator>();
            RuntimeBudgetSettings budgetSettings = AssetDatabase.LoadAssetAtPath<RuntimeBudgetSettings>(RuntimeBudgetPath);
            coordinator.Configure(budgetSettings, player.transform);

            RaidClock raidClock = runtimeRoot.AddComponent<RaidClock>();
            Light sunriseLight = CreateSunriseLight(targetScene);
            VerticalSliceRaidController controller = runtimeRoot.AddComponent<VerticalSliceRaidController>();
            controller.Configure(raidClock, player, coordinator, sunriseLight, gates.ToArray());

            VerticalSliceHud hud = runtimeRoot.AddComponent<VerticalSliceHud>();
            hud.Configure(controller);

            int breckenAgentTypeId = GetBreckenAgentTypeId();
            NavMeshSurface surface = CreateAndBakeNavigation(targetScene, breckenAgentTypeId);
            BreckenAI[] breckens = CloneBreckens(targetScene, player.transform, coordinator);
            runtimeRoot.AddComponent<PrototypeEnemyActivator>().Configure(breckens, player.transform);

            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorSceneManager.SaveScene(targetScene);
            AssetDatabase.ForceReserializeAssets(
                new[] { OutputScenePath },
                ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[VerticalSliceBuilder] Built {OutputScenePath}: player=1, breckens=3, bunkers={gates.Count}");
        }

        public static void BuildFromCommandLine()
        {
            Build();
        }

        private static void DisableMapOverviewCamera(Scene targetScene)
        {
            foreach (GameObject root in targetScene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    camera.gameObject.SetActive(false);
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;
            }
        }

        private static FirstPersonMotor ClonePlayer(Scene targetScene)
        {
            Scene playerScene = EditorSceneManager.OpenScene(PlayerScenePath, OpenSceneMode.Additive);
            FirstPersonMotor source = Object.FindObjectsByType<FirstPersonMotor>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.gameObject.scene == playerScene);

            if (source == null)
                throw new InvalidOperationException($"FirstPersonMotor was not found in {PlayerScenePath}");

            GameObject clone = Object.Instantiate(source.gameObject);
            clone.name = "PrototypePlayer";
            clone.SetActive(true);
            SceneManager.MoveGameObjectToScene(clone, targetScene);
            EditorSceneManager.CloseScene(playerScene, true);

            FirstPersonMotor motor = clone.GetComponent<FirstPersonMotor>();
            if (clone.GetComponent<PlayerInteractor>() == null)
                clone.AddComponent<PlayerInteractor>();
            if (clone.GetComponent<Health>() == null)
                clone.AddComponent<Health>();
            return motor;
        }

        private static List<PrototypeExtractionGate> ConfigureBunkers(Scene targetScene)
        {
            Transform bunkerRoot = FindTransform(targetScene, "BunkerMarkers");
            if (bunkerRoot == null)
                throw new InvalidOperationException("BunkerMarkers root was not found in the terrain blockout.");

            List<Transform> markers = bunkerRoot.GetComponentsInChildren<Transform>(true)
                .Where(candidate => candidate != bunkerRoot && candidate.name.IndexOf("bunker", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(candidate => candidate.name)
                .Take(6)
                .ToList();

            if (markers.Count < 6)
                throw new InvalidOperationException($"Six bunker markers are required, but only {markers.Count} were found.");

            GameObject zonesRoot = new GameObject("_ExtractionVolumes");
            SceneManager.MoveGameObjectToScene(zonesRoot, targetScene);
            List<PrototypeExtractionGate> gates = new List<PrototypeExtractionGate>(markers.Count);

            for (int i = 0; i < markers.Count; i++)
            {
                Transform marker = markers[i];
                Collider markerCollider = marker.GetComponent<Collider>();
                if (markerCollider == null)
                    markerCollider = marker.gameObject.AddComponent<BoxCollider>();
                markerCollider.isTrigger = false;

                PrototypeExtractionGate gate = marker.GetComponent<PrototypeExtractionGate>();
                if (gate == null)
                    gate = marker.gameObject.AddComponent<PrototypeExtractionGate>();
                gates.Add(gate);

                GameObject volume = new GameObject($"ExtractionVolume_{i + 1:00}");
                volume.transform.SetPositionAndRotation(marker.position, marker.rotation);
                volume.transform.SetParent(zonesRoot.transform, true);
                BoxCollider trigger = volume.AddComponent<BoxCollider>();
                Vector3 markerSize = markerCollider.bounds.size;
                trigger.size = new Vector3(
                    Mathf.Max(3f, markerSize.x),
                    Mathf.Max(2.5f, markerSize.y + 1f),
                    Mathf.Max(3f, markerSize.z));
                trigger.isTrigger = true;
                volume.AddComponent<PrototypeExtractionZone>().Configure(gate);
            }

            return gates;
        }

        private static int GetBreckenAgentTypeId()
        {
            Scene mobScene = EditorSceneManager.OpenScene(MobScenePath, OpenSceneMode.Additive);
            NavMeshAgent sourceAgent = Object.FindObjectsByType<NavMeshAgent>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.gameObject.scene == mobScene);
            if (sourceAgent == null)
                throw new InvalidOperationException($"NavMeshAgent was not found in {MobScenePath}");

            int agentTypeId = sourceAgent.agentTypeID;
            EditorSceneManager.CloseScene(mobScene, true);
            return agentTypeId;
        }

        private static NavMeshSurface CreateAndBakeNavigation(Scene targetScene, int agentTypeId)
        {
            GameObject navigation = new GameObject("_Navigation");
            SceneManager.MoveGameObjectToScene(navigation, targetScene);
            NavMeshSurface surface = navigation.AddComponent<NavMeshSurface>();
            surface.agentTypeID = agentTypeId;
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.layerMask = ~0;

            if (!AssetDatabase.IsValidFolder(NavMeshFolderPath))
                AssetDatabase.CreateFolder("Assets/_Project/Scenes", "VerticalSlice");
            if (AssetDatabase.LoadMainAssetAtPath(NavMeshDataPath) != null)
                AssetDatabase.DeleteAsset(NavMeshDataPath);

            surface.BuildNavMesh();
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshDataPath);
            EditorUtility.SetDirty(surface);
            AssetDatabase.SaveAssets();

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices == null || triangulation.vertices.Length < 3)
                throw new InvalidOperationException("The terrain blockout produced an empty NavMesh.");
            Debug.Log($"[VerticalSliceBuilder] NavMesh vertices={triangulation.vertices.Length}, indices={triangulation.indices.Length}");
            return surface;
        }

        private static BreckenAI[] CloneBreckens(
            Scene targetScene,
            Transform player,
            RuntimeCoordinator coordinator)
        {
            Scene mobScene = EditorSceneManager.OpenScene(MobScenePath, OpenSceneMode.Additive);
            BreckenAI source = Object.FindObjectsByType<BreckenAI>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.gameObject.scene == mobScene);

            if (source == null)
                throw new InvalidOperationException($"BreckenAI was not found in {MobScenePath}");

            Terrain terrain = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.gameObject.scene == targetScene);
            Bounds terrainBounds = terrain != null
                ? new Bounds(terrain.transform.position + terrain.terrainData.size * 0.5f, terrain.terrainData.size)
                : new Bounds(Vector3.zero, new Vector3(400f, 50f, 400f));

            Vector3[] normalizedPositions =
            {
                new Vector3(-0.23f, 0f, -0.18f),
                new Vector3(0.18f, 0f, -0.08f),
                new Vector3(0.04f, 0f, 0.24f)
            };
            List<BreckenAI> clones = new List<BreckenAI>(normalizedPositions.Length);

            for (int i = 0; i < normalizedPositions.Length; i++)
            {
                GameObject clone = Object.Instantiate(source.gameObject);
                clone.name = $"PrototypeBrecken_{i + 1:00}";
                clone.SetActive(true);
                SceneManager.MoveGameObjectToScene(clone, targetScene);

                Vector3 offset = normalizedPositions[i];
                Vector3 candidate = terrainBounds.center + new Vector3(
                    terrainBounds.extents.x * offset.x * 2f,
                    0f,
                    terrainBounds.extents.z * offset.z * 2f);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 100f, NavMesh.AllAreas))
                    throw new InvalidOperationException($"No NavMesh position was found for Brecken {i + 1} near {candidate}.");
                candidate = hit.position;

                clone.transform.position = candidate;
                BreckenAI ai = clone.GetComponent<BreckenAI>();
                NavMeshAgent agent = clone.GetComponent<NavMeshAgent>();
                agent.enabled = false;
                ai.enabled = false;
                SerializedObject serialized = new SerializedObject(ai);
                serialized.FindProperty("playerTransform").objectReferenceValue = player;
                serialized.FindProperty("runtimeCoordinator").objectReferenceValue = coordinator;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                if (clone.GetComponent<Health>() == null)
                    clone.AddComponent<Health>();
                if (clone.GetComponent<AssassinationInteractable>() == null)
                    clone.AddComponent<AssassinationInteractable>();
                clones.Add(ai);
            }

            EditorSceneManager.CloseScene(mobScene, true);
            return clones.ToArray();
        }

        private static Light CreateSunriseLight(Scene targetScene)
        {
            GameObject lightObject = new GameObject("_SunriseLight");
            SceneManager.MoveGameObjectToScene(lightObject, targetScene);
            lightObject.transform.rotation = Quaternion.Euler(32f, -28f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.82f);
            light.intensity = 0f;
            lightObject.SetActive(false);
            return light;
        }

        private static Transform FindTransform(Scene scene, string exactName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name == exactName)
                        return transforms[i];
                }
            }
            return null;
        }

        private static void ConfigureBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(scene => scene.path == OutputScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(OutputScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
