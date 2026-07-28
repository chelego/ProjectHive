using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace ProjectHive.AI.Hive.Training
{
    [RequireComponent(typeof(HiveTrainingEnvironment))]
    [DisallowMultipleComponent]
    public sealed class HiveTrainingAgent : Agent
    {
        public const int ObservationSize = 20;
        public const int CommandBranchSize = 6;
        public const int TargetBranchSize = 4;
        public const string BehaviorName = "HiveDirector";

        private HiveTrainingEnvironment environment;

        public override void Initialize()
        {
            environment = GetComponent<HiveTrainingEnvironment>();
        }

        public override void OnEpisodeBegin()
        {
            environment ??= GetComponent<HiveTrainingEnvironment>();
            environment.ResetEpisode();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            environment ??= GetComponent<HiveTrainingEnvironment>();

            sensor.AddObservation(environment.AlertScore);
            sensor.AddObservation(environment.ReportConfidence);
            sensor.AddObservation(environment.NormalizedReportAge);
            sensor.AddObservation(environment.NormalizedReportedVelocity);
            sensor.AddObservation(environment.ExtractionActive);
            sensor.AddObservation(environment.NormalizedRemainingTime);

            AddOneHot(sensor, (int)environment.ReportKind, 7);
            AddOneHot(sensor, (int)environment.LastCommandKind, 6);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            int commandIndex = actions.DiscreteActions[0];
            int targetSource = actions.DiscreteActions[1];
            HiveCommandKind command = (HiveCommandKind)Mathf.Clamp(
                commandIndex,
                0,
                CommandBranchSize - 1);

            float reward = environment.SimulateDecision(
                command,
                targetSource,
                out bool episodeEnded);
            AddReward(reward);

            if (episodeEnded)
                EndEpisode();
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            ActionSegment<int> discrete = actionsOut.DiscreteActions;
            discrete[0] = environment.GetHeuristicCommand();
            discrete[1] = environment.ExtractionActive &&
                          environment.ReportKind == EnemyReportKind.ExtractionActivity
                ? 2
                : 0;
        }

        private static void AddOneHot(VectorSensor sensor, int value, int count)
        {
            for (int index = 0; index < count; index++)
                sensor.AddObservation(index == value ? 1f : 0f);
        }
    }
}
