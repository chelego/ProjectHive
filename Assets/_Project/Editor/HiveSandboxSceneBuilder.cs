using ProjectHive.AI.Hive;
using ProjectHive.AI.Hive.Debugging;
using ProjectHive.Core.Events;
using ProjectHive.Core.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectHive.Editor
{
    public static class HiveSandboxSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/HiveSandbox.unity";

        [MenuItem("Project Hive/Hive/Create Sandbox Scene")]
        public static void CreateSandboxScene()
        {
            Scene originalActiveScene = SceneManager.GetActiveScene();
            Scene sandboxScene = default;

            try
            {
                sandboxScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive);
                SceneManager.SetActiveScene(sandboxScene);

                BuildServices(out GameEventBus eventBus, out RuntimeCoordinator coordinator);
                HiveDirector director = BuildHive(eventBus, coordinator);
                BuildReportSource(eventBus, coordinator);
                BuildDebugView(eventBus, director);
                BuildArena();
                BuildCameraAndLight();

                EditorSceneManager.SaveScene(sandboxScene, ScenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Created Hive sandbox scene: {ScenePath}");
            }
            finally
            {
                if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
                    SceneManager.SetActiveScene(originalActiveScene);

                if (sandboxScene.IsValid() && sandboxScene.isLoaded)
                    EditorSceneManager.CloseScene(sandboxScene, true);
            }
        }

        private static void BuildServices(
            out GameEventBus eventBus,
            out RuntimeCoordinator coordinator)
        {
            GameObject services = new GameObject("Services");
            eventBus = services.AddComponent<GameEventBus>();
            coordinator = services.AddComponent<RuntimeCoordinator>();
            coordinator.Configure(null, null);
        }

        private static HiveDirector BuildHive(
            GameEventBus eventBus,
            RuntimeCoordinator coordinator)
        {
            GameObject hive = new GameObject("HiveDirector");
            RuleBasedHivePolicy policy = hive.AddComponent<RuleBasedHivePolicy>();
            HiveDirector director = hive.AddComponent<HiveDirector>();
            director.Configure(eventBus, coordinator, policy);
            return director;
        }

        private static void BuildReportSource(
            GameEventBus eventBus,
            RuntimeCoordinator coordinator)
        {
            GameObject source = new GameObject("SyntheticReportSource");
            HiveSandboxReportSource reportSource =
                source.AddComponent<HiveSandboxReportSource>();
            reportSource.Configure(eventBus, coordinator);
        }

        private static void BuildDebugView(
            GameEventBus eventBus,
            HiveDirector director)
        {
            GameObject debugViewObject = new GameObject("HiveDebugView");
            HiveCommandDebugView debugView =
                debugViewObject.AddComponent<HiveCommandDebugView>();
            debugView.Configure(eventBus, director);
        }

        private static void BuildArena()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "DebugGround";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);

            Vector3[] positions =
            {
                new Vector3(12f, 0.5f, 0f),
                new Vector3(0f, 0.5f, 12f),
                new Vector3(-12f, 0.5f, 0f),
                new Vector3(0f, 0.5f, -12f)
            };

            for (int index = 0; index < positions.Length; index++)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = $"ReportAreaMarker_{index + 1:00}";
                marker.transform.position = positions[index];
                marker.transform.localScale = new Vector3(1f, 1f, 1f);
            }
        }

        private static void BuildCameraAndLight()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.03f, 0.04f);
            camera.transform.position = new Vector3(0f, 28f, -26f);
            camera.transform.LookAt(Vector3.zero);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }
}
