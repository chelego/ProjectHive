using System;
using System.Collections.Generic;
using ProjectHive.Core.Events;
using Unity.Profiling;
using UnityEngine;

namespace ProjectHive.AI.Hive
{
    [DefaultExecutionOrder(-400)]
    [DisallowMultipleComponent]
    public sealed class HiveCommandDispatcher : MonoBehaviour
    {
        private static readonly ProfilerMarker DispatchMarker =
            new ProfilerMarker("ProjectHive.Hive.DispatchCommand");

        [SerializeField] private GameEventBus eventBus;
        [SerializeField] private HiveUnitRegistry unitRegistry;
        [SerializeField, Min(1f)] private float mobilizationRadius = 80f;
        [SerializeField, Min(1)] private int selectionBufferCapacity = 8;

        private List<IHiveControllable> selectionBuffer;
        private List<float> distanceBuffer;

        public HiveDispatchResult LastResult { get; private set; }
        public int TotalAssignmentsAccepted { get; private set; }
        public event Action<HiveDispatchResult> CommandDispatched;

        public void Configure(GameEventBus bus, HiveUnitRegistry registry)
        {
            eventBus = bus;
            unitRegistry = registry;
        }

        private void Awake()
        {
            int capacity = Mathf.Max(1, selectionBufferCapacity);
            selectionBuffer = new List<IHiveControllable>(capacity);
            distanceBuffer = new List<float>(capacity);
        }

        private void OnEnable()
        {
            ResolveServices();
            if (eventBus != null)
                eventBus.HiveCommandIssued += OnHiveCommandIssued;
        }

        private void OnDisable()
        {
            if (eventBus != null)
                eventBus.HiveCommandIssued -= OnHiveCommandIssued;
        }

        private void OnHiveCommandIssued(in HiveCommand command)
        {
            if (unitRegistry == null || command.Kind == HiveCommandKind.None)
                return;

            using (DispatchMarker.Auto())
            {
                HiveUnitCapabilities required =
                    GetRequiredCapabilities(command.Kind);
                int candidates = unitRegistry.CollectNearestAvailable(
                    in command,
                    required,
                    mobilizationRadius,
                    selectionBuffer,
                    distanceBuffer);

                int accepted = 0;
                for (int index = 0; index < selectionBuffer.Count; index++)
                {
                    if (selectionBuffer[index].TryAcceptHiveCommand(in command))
                        accepted++;
                }

                int rejected = candidates - accepted;
                LastResult = new HiveDispatchResult(
                    command.Sequence,
                    candidates,
                    accepted,
                    rejected);
                TotalAssignmentsAccepted += accepted;
                CommandDispatched?.Invoke(LastResult);
            }
        }

        private void ResolveServices()
        {
            if (eventBus == null)
                eventBus = GameEventBus.Instance;
            if (unitRegistry == null)
                unitRegistry = FindFirstObjectByType<HiveUnitRegistry>();
        }

        private static HiveUnitCapabilities GetRequiredCapabilities(
            HiveCommandKind commandKind)
        {
            switch (commandKind)
            {
                case HiveCommandKind.Investigate:
                case HiveCommandKind.SearchArea:
                    return HiveUnitCapabilities.Investigate;
                case HiveCommandKind.GuardExtraction:
                    return HiveUnitCapabilities.Guard;
                case HiveCommandKind.Converge:
                case HiveCommandKind.ResumeHunt:
                    return HiveUnitCapabilities.Attack;
                default:
                    return HiveUnitCapabilities.None;
            }
        }
    }
}
