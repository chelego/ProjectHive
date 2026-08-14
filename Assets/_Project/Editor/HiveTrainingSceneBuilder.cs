using System.IO;
using ProjectHive.AI.Hive.Training;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectHive.Editor
{
    public static class HiveTrainingSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/HiveTraining.unity";
        private const string BuildDirectory = "Builds/MLTraining";
        private const string BuildPath = BuildDirectory + "/ProjectHiveHiveTraining.exe";
        private const int EnvironmentCount = 16;

        [MenuItem("Project Hive/ML/Create Hive Training Scene")]
        public static void CreateTrainingScene()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            for (int index = 0; index < EnvironmentCount; index++)
                CreateEnvironment(index);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created Hive training scene with {EnvironmentCount} environments: {ScenePath}");
        }

        [MenuItem("Project Hive/ML/Build Hive Training Player")]
        public static void BuildTrainingPlayer()
        {
            CreateTrainingScene();
            Directory.CreateDirectory(BuildDirectory);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException($"Hive training build failed: {report.summary.result}");

            Debug.Log(
                $"Built Hive training player: {BuildPath} " +
                $"({report.summary.totalSize} bytes)");
        }

        public static void BuildFromCommandLine()
        {
            BuildTrainingPlayer();
        }

        private static void CreateEnvironment(int index)
        {
            GameObject root = new GameObject($"HiveTrainingEnvironment_{index:00}");
            root.transform.position = new Vector3(
                (index % 4) * 50f,
                0f,
                (index / 4) * 50f);

            BehaviorParameters behavior = root.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = HiveTrainingAgent.BehaviorName;
            behavior.BehaviorType = BehaviorType.Default;
            behavior.BrainParameters.VectorObservationSize = HiveTrainingAgent.ObservationSize;
            behavior.BrainParameters.NumStackedVectorObservations = 1;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(
                HiveTrainingAgent.CommandBranchSize,
                HiveTrainingAgent.TargetBranchSize,
                HiveTrainingAgent.UnitCountBranchSize);

            root.AddComponent<HiveTrainingEnvironment>();
            HiveTrainingAgent agent = root.AddComponent<HiveTrainingAgent>();
            agent.MaxStep = 320;

            DecisionRequester requester = root.AddComponent<DecisionRequester>();
            requester.DecisionPeriod = 1;
            requester.TakeActionsBetweenDecisions = true;
        }
    }
}
