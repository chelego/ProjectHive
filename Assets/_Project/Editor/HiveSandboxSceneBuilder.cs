using ProjectHive.AI.Hive;
using ProjectHive.AI.Hive.Debugging;
using ProjectHive.AI.Hive.Training;
using ProjectHive.Core.Events;
using ProjectHive.Core.Runtime;
using Unity.InferenceEngine;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectHive.Editor
{
    public static class HiveSandboxSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/HiveSandbox.unity";
        private const string ModelPath =
            "Assets/_Project/ML/Models/HiveDirector.onnx";

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

                BuildServices(
                    out GameEventBus eventBus,
                    out RuntimeCoordinator coordinator,
                    out HiveUnitRegistry registry,
                    out HiveCommandDispatcher dispatcher);
                HiveDirector director =
                    BuildHive(eventBus, coordinator, registry);
                BuildReportSource(eventBus, coordinator);
                BuildDebugUnits(registry, coordinator);
                BuildDebugView(eventBus, director, registry, dispatcher);
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
            out RuntimeCoordinator coordinator,
            out HiveUnitRegistry registry,
            out HiveCommandDispatcher dispatcher)
        {
            GameObject services = new GameObject("Services");
            eventBus = services.AddComponent<GameEventBus>();
            coordinator = services.AddComponent<RuntimeCoordinator>();
            coordinator.Configure(null, null);
            registry = services.AddComponent<HiveUnitRegistry>();
            dispatcher = services.AddComponent<HiveCommandDispatcher>();
            dispatcher.Configure(eventBus, registry);
        }

        private static HiveDirector BuildHive(
            GameEventBus eventBus,
            RuntimeCoordinator coordinator,
            HiveUnitRegistry registry)
        {
            GameObject hive = new GameObject("HiveDirector");
            RuleBasedHivePolicy fallback =
                hive.AddComponent<RuleBasedHivePolicy>();
            MonoBehaviour policy = fallback;

            ModelAsset model =
                AssetDatabase.LoadAssetAtPath<ModelAsset>(ModelPath);
            if (model != null)
            {
                BehaviorParameters behavior =
                    hive.AddComponent<BehaviorParameters>();
                behavior.BehaviorName =
                    HiveTrainingAgent.BehaviorName;
                behavior.BehaviorType = BehaviorType.InferenceOnly;
                behavior.Model = model;
                behavior.BrainParameters.VectorObservationSize =
                    HiveTrainingAgent.ObservationSize;
                behavior.BrainParameters.NumStackedVectorObservations = 1;
                behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(
                    HiveTrainingAgent.CommandBranchSize,
                    HiveTrainingAgent.TargetBranchSize,
                    HiveTrainingAgent.UnitCountBranchSize);

                LearnedHivePolicy learned =
                    hive.AddComponent<LearnedHivePolicy>();
                learned.Configure(model, registry, null, fallback);
                policy = learned;
            }

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
            HiveDirector director,
            HiveUnitRegistry registry,
            HiveCommandDispatcher dispatcher)
        {
            GameObject debugViewObject = new GameObject("HiveDebugView");
            HiveCommandDebugView debugView =
                debugViewObject.AddComponent<HiveCommandDebugView>();
            debugView.Configure(eventBus, director, registry, dispatcher);
        }

        private static void BuildDebugUnits(
            HiveUnitRegistry registry,
            RuntimeCoordinator coordinator)
        {
            Vector3[] positions =
            {
                new Vector3(-17f, 0.75f, -8f),
                new Vector3(-13f, 0.75f, 8f),
                new Vector3(-6f, 0.75f, -17f),
                new Vector3(6f, 0.75f, 17f),
                new Vector3(13f, 0.75f, -8f),
                new Vector3(17f, 0.75f, 8f),
                new Vector3(-8f, 0.75f, 16f),
                new Vector3(8f, 0.75f, -16f)
            };

            for (int index = 0; index < positions.Length; index++)
            {
                HiveUnitRole role = index % 4 == 1
                    ? HiveUnitRole.Scout
                    : index % 4 == 3
                        ? HiveUnitRole.Heavy
                        : HiveUnitRole.Hunter;
                HiveUnitCapabilities capabilities =
                    HiveUnitCapabilities.GroundMovement |
                    HiveUnitCapabilities.Attack |
                    HiveUnitCapabilities.Investigate |
                    HiveUnitCapabilities.Guard;
                if (role == HiveUnitRole.Scout)
                {
                    capabilities |=
                        HiveUnitCapabilities.Flight |
                        HiveUnitCapabilities.ReportTarget;
                }

                PrimitiveType primitive = role == HiveUnitRole.Scout
                    ? PrimitiveType.Sphere
                    : role == HiveUnitRole.Heavy
                        ? PrimitiveType.Cylinder
                        : PrimitiveType.Capsule;
                GameObject unit = GameObject.CreatePrimitive(primitive);
                unit.name = $"HiveUnit_{role}_{index + 1:00}";
                unit.transform.position = positions[index];
                unit.transform.localScale = role == HiveUnitRole.Heavy
                    ? new Vector3(1.4f, 1.1f, 1.4f)
                    : Vector3.one;

                HiveSandboxUnit sandboxUnit = unit.AddComponent<HiveSandboxUnit>();
                sandboxUnit.Configure(
                    registry,
                    coordinator,
                    role,
                    capabilities,
                    role == HiveUnitRole.Heavy ? 2.6f : 4f);
            }
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
