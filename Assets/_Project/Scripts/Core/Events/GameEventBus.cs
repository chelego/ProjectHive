using System;
using ProjectHive.AI.Hive;
using ProjectHive.Core.Flow;
using ProjectHive.Data.Runs;
using UnityEngine;

namespace ProjectHive.Core.Events
{
    public delegate void NoiseEventHandler(in NoiseEvent noiseEvent);
    public delegate void EnemyReportHandler(in EnemyReport report);
    public delegate void HiveCommandHandler(in HiveCommand command);
    public delegate void GameStateTransitionHandler(in GameStateTransition transition);
    public delegate void RunResultHandler(RunResult result);

    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class GameEventBus : MonoBehaviour
    {
        public static GameEventBus Instance { get; private set; }

        public event NoiseEventHandler NoiseEmitted;
        public event EnemyReportHandler EnemyReported;
        public event HiveCommandHandler HiveCommandIssued;
        public event GameStateTransitionHandler GameStateChanged;
        public event RunResultHandler RunFinished;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void PublishNoise(in NoiseEvent noiseEvent)
        {
            NoiseEmitted?.Invoke(in noiseEvent);
        }

        public void PublishEnemyReport(in EnemyReport report)
        {
            EnemyReported?.Invoke(in report);
        }

        public void PublishHiveCommand(in HiveCommand command)
        {
            HiveCommandIssued?.Invoke(in command);
        }

        public void PublishGameState(in GameStateTransition transition)
        {
            GameStateChanged?.Invoke(in transition);
        }

        public void PublishRunResult(RunResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            RunFinished?.Invoke(result);
        }
    }
}
