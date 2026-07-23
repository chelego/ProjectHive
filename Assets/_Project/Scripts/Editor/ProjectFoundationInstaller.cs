using ProjectHive.AI.Hive;
using ProjectHive.Core.Events;
using ProjectHive.Core.Flow;
using ProjectHive.Core.Runtime;
using ProjectHive.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectHive.EditorTools
{
    [InitializeOnLoad]
    public static class ProjectFoundationInstaller
    {
        private const string TargetScenePath = "Assets/_Project/Scenes/Prototype_FlatFPS.unity";
        private const string MarkerName = "Project_Foundation_v1";
        private const string RuntimeSettingsPath =
            "Assets/_Project/Data/Runtime/RuntimeBudgetSettings.asset";

        static ProjectFoundationInstaller()
        {
            EditorApplication.delayCall += InstallWhenReady;
        }

        private static void InstallWhenReady()
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += InstallWhenReady;
                return;
            }

            if (SceneManager.GetActiveScene().path != TargetScenePath ||
                GameObject.Find(MarkerName) != null)
            {
                return;
            }

            InstallFoundation();
        }

        [MenuItem("Project Hive/Foundation/Install Foundation")]
        public static void InstallFoundation()
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath);
            if (sceneAsset == null)
            {
                Debug.LogError("[ProjectFoundationInstaller] Target prototype scene is missing.");
                return;
            }

            if (SceneManager.GetActiveScene().path != TargetScenePath)
                EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);

            if (GameObject.Find(MarkerName) != null)
            {
                Debug.Log("[ProjectFoundationInstaller] Foundation is already installed.");
                return;
            }

            EnsureProjectFolders();
            RuntimeBudgetSettings settings = GetOrCreateRuntimeSettings();

            GameObject sceneRoot = GameObject.Find("Prototype_FlatFPS");
            Transform parent = sceneRoot != null ? sceneRoot.transform : null;
            GameObject player = GameObject.Find("Prototype_FlatFPS/Player");

            GameObject foundationRoot = new GameObject(MarkerName);
            foundationRoot.transform.SetParent(parent, false);

            GameObject eventBusObject = new GameObject("GameEventBus");
            eventBusObject.transform.SetParent(foundationRoot.transform, false);
            GameEventBus eventBus = eventBusObject.AddComponent<GameEventBus>();

            GameObject runtimeObject = new GameObject("RuntimeCoordinator");
            runtimeObject.transform.SetParent(foundationRoot.transform, false);
            RuntimeCoordinator coordinator = runtimeObject.AddComponent<RuntimeCoordinator>();
            coordinator.Configure(settings, player != null ? player.transform : null);

            GameObject flowObject = new GameObject("GameFlow");
            flowObject.transform.SetParent(foundationRoot.transform, false);
            GameFlowManager gameFlow = flowObject.AddComponent<GameFlowManager>();
            gameFlow.Configure(eventBus);

            GameObject hiveObject = new GameObject("HiveSystem");
            hiveObject.transform.SetParent(foundationRoot.transform, false);
            RuleBasedHivePolicy policy = hiveObject.AddComponent<RuleBasedHivePolicy>();
            HiveDirector director = hiveObject.AddComponent<HiveDirector>();
            director.Configure(eventBus, coordinator, policy);

            GameObject diagnosticsObject = new GameObject("FoundationDiagnostics");
            diagnosticsObject.transform.SetParent(foundationRoot.transform, false);
            FoundationDiagnostics diagnostics =
                diagnosticsObject.AddComponent<FoundationDiagnostics>();
            diagnostics.Configure(eventBus, gameFlow, coordinator, director);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), TargetScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = foundationRoot;
            Debug.Log(
                "[ProjectFoundationInstaller] Installed GameFlow, EventBus, RuntimeCoordinator, " +
                "rule-based Hive foundation, and diagnostics.");
        }

        private static RuntimeBudgetSettings GetOrCreateRuntimeSettings()
        {
            RuntimeBudgetSettings settings =
                AssetDatabase.LoadAssetAtPath<RuntimeBudgetSettings>(RuntimeSettingsPath);
            if (settings != null)
                return settings;

            settings = ScriptableObject.CreateInstance<RuntimeBudgetSettings>();
            AssetDatabase.CreateAsset(settings, RuntimeSettingsPath);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static void EnsureProjectFolders()
        {
            string[] folders =
            {
                "Assets/_Project/Data",
                "Assets/_Project/Data/AI",
                "Assets/_Project/Data/AI/Hive",
                "Assets/_Project/Data/Items",
                "Assets/_Project/Data/Runs",
                "Assets/_Project/Data/Runtime",
                "Assets/_Project/Prefabs",
                "Assets/_Project/Prefabs/AI",
                "Assets/_Project/Prefabs/Items",
                "Assets/_Project/Prefabs/Interaction",
                "Assets/_Project/Tests"
            };

            for (int index = 0; index < folders.Length; index++)
                EnsureFolder(folders[index]);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            string child = path.Substring(separator + 1);

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
