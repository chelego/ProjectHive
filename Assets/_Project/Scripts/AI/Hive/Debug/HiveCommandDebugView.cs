using ProjectHive.Core.Events;
using UnityEngine;

namespace ProjectHive.AI.Hive.Debugging
{
    [DisallowMultipleComponent]
    public sealed class HiveCommandDebugView : MonoBehaviour
    {
        [SerializeField] private GameEventBus eventBus;
        [SerializeField] private HiveDirector hiveDirector;
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private bool drawGizmos = true;

        private EnemyReport lastReport;
        private HiveCommand lastCommand;
        private bool hasReport;
        private bool hasCommand;
        private int reportCount;
        private int commandCount;

        public int ReportCount => reportCount;
        public int CommandCount => commandCount;
        public bool HasReport => hasReport;
        public bool HasCommand => hasCommand;
        public EnemyReport LastReport => lastReport;
        public HiveCommand LastCommand => lastCommand;

        public void Configure(GameEventBus bus, HiveDirector director)
        {
            eventBus = bus;
            hiveDirector = director;
        }

        private void OnEnable()
        {
            ResolveServices();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnReport(in EnemyReport report)
        {
            lastReport = report;
            hasReport = true;
            reportCount++;
        }

        private void OnNoise(in NoiseEvent noiseEvent)
        {
            lastReport = new EnemyReport(
                EnemyReportKind.Noise,
                noiseEvent.Position,
                Mathf.Clamp01(noiseEvent.Loudness),
                noiseEvent.SourceInstanceId,
                noiseEvent.OccurredAt);
            hasReport = true;
            reportCount++;
        }

        private void OnCommand(in HiveCommand command)
        {
            lastCommand = command;
            hasCommand = true;
            commandCount++;
        }

        private void OnGUI()
        {
            if (!showOverlay)
                return;

            HiveBlackboard blackboard = hiveDirector != null
                ? hiveDirector.Blackboard
                : null;
            float alert = blackboard != null ? blackboard.AlertScore : 0f;

            GUILayout.BeginArea(new Rect(16f, 16f, 420f, 180f), GUI.skin.box);
            GUILayout.Label("HIVE SANDBOX");
            GUILayout.Label($"Reports: {reportCount}  Commands: {commandCount}");
            GUILayout.Label($"Alert: {alert:0.00}");
            GUILayout.Label(hasReport
                ? $"Last Report: {lastReport.Kind} / Confidence {lastReport.Confidence:0.00}"
                : "Last Report: none");
            GUILayout.Label(hasCommand
                ? $"Last Command: {lastCommand.Kind} / Priority {lastCommand.Priority:0.00}"
                : "Last Command: none");
            GUILayout.EndArea();
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos)
                return;

            if (hasReport)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(lastReport.Position + Vector3.up * 0.5f, 0.5f);
            }

            if (hasCommand)
            {
                Gizmos.color = Color.red;
                Vector3 center = lastCommand.TargetPosition + Vector3.up * 0.1f;
                Gizmos.DrawWireSphere(center, lastCommand.Radius);
                Gizmos.DrawLine(transform.position, center);
            }
        }

        private void ResolveServices()
        {
            if (eventBus == null)
                eventBus = GameEventBus.Instance;
            if (hiveDirector == null)
                hiveDirector = FindFirstObjectByType<HiveDirector>();
        }

        private void Subscribe()
        {
            if (eventBus == null)
                return;

            eventBus.NoiseEmitted += OnNoise;
            eventBus.EnemyReported += OnReport;
            eventBus.HiveCommandIssued += OnCommand;
        }

        private void Unsubscribe()
        {
            if (eventBus == null)
                return;

            eventBus.NoiseEmitted -= OnNoise;
            eventBus.EnemyReported -= OnReport;
            eventBus.HiveCommandIssued -= OnCommand;
        }
    }
}
