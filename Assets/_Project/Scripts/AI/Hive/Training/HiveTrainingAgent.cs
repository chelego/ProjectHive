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
        public const int ObservationSize = HiveDecisionObservation.ObservationSize;
        public const int CommandBranchSize = 6;
        public const int TargetBranchSize = 4;
        public const int UnitCountBranchSize = 3;
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
            HiveDecisionObservation observation =
                environment.CreateObservation();
            observation.WriteTo(sensor);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            int commandIndex = actions.DiscreteActions[0];
            int targetSource = actions.DiscreteActions[1];
            int unitCountChoice = actions.DiscreteActions[2];
            HiveCommandKind command = (HiveCommandKind)Mathf.Clamp(
                commandIndex,
                0,
                CommandBranchSize - 1);

            float reward = environment.SimulateDecision(
                command,
                targetSource,
                ResolveRequestedUnitCount(unitCountChoice),
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
            discrete[2] = environment.GetHeuristicUnitCountChoice();
        }

        public static int ResolveRequestedUnitCount(int choice)
        {
            switch (Mathf.Clamp(choice, 0, UnitCountBranchSize - 1))
            {
                case 0:
                    return 1;
                case 1:
                    return 3;
                default:
                    return 5;
            }
        }
    }
}
