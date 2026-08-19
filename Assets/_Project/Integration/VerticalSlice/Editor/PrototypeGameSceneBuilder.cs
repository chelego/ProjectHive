#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using ProjectHive.Core.Events;
using ProjectHive.Core.Flow;
using ProjectHive.Core.Runtime;
using ProjectHive.Integration.VerticalSlice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectHive.Editor.Integration
{
    public static class PrototypeGameSceneBuilder
    {
        public const string FrontEndScenePath =
            "Assets/_Project/Integration/VerticalSlice/Scenes/PrototypeFrontEnd.unity";
        public const string RaidScenePath =
            "Assets/_Project/Integration/VerticalSlice/Scenes/VerticalSlice.unity";
        private const string RuntimeBudgetPath =
            "Assets/_Project/Data/Runtime/RuntimeBudgetSettings.asset";

        [MenuItem("Project Hive/Integration/Build Playable Prototype")]
        public static void BuildPlayablePrototype()
        {
            VerticalSliceSceneBuilder.Build();
            BuildFrontEndOnly();
            Debug.Log("[PrototypeBuilder] 메인 화면부터 지상 진입까지 플레이 가능한 프로토타입을 생성했습니다.");
        }

        [MenuItem("Project Hive/Integration/Rebuild Front End Only")]
        public static void BuildFrontEndOnly()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.012f, 0.014f, 1f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.tag = "MainCamera";

            GameObject sessionRoot = new GameObject("_PrototypeSession");
            GameEventBus eventBus = sessionRoot.AddComponent<GameEventBus>();
            GameFlowManager flow = sessionRoot.AddComponent<GameFlowManager>();
            flow.Configure(eventBus);
            RuntimeCoordinator coordinator = sessionRoot.AddComponent<RuntimeCoordinator>();
            coordinator.Configure(
                AssetDatabase.LoadAssetAtPath<RuntimeBudgetSettings>(RuntimeBudgetPath),
                null);
            sessionRoot.AddComponent<PrototypeGameSession>();

            GameObject screenRoot = new GameObject("_PrototypeFrontEnd");
            screenRoot.AddComponent<PrototypeFrontEndController>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, FrontEndScenePath);
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(FrontEndScenePath, OpenSceneMode.Single);
            Selection.activeGameObject = screenRoot;
        }

        private static void ConfigureBuildSettings()
        {
            List<EditorBuildSettingsScene> remaining = EditorBuildSettings.scenes
                .Where(scene => scene.path != FrontEndScenePath && scene.path != RaidScenePath)
                .Select(scene => new EditorBuildSettingsScene(scene.path, false))
                .ToList();

            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(FrontEndScenePath, true),
                new EditorBuildSettingsScene(RaidScenePath, true)
            };
            scenes.AddRange(remaining);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
