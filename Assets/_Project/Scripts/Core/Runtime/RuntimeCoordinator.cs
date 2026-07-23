using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectHive.Core.Runtime
{
    [DefaultExecutionOrder(-700)]
    [DisallowMultipleComponent]
    public sealed class RuntimeCoordinator : MonoBehaviour
    {
        private sealed class Entry
        {
            public IRuntimeTickable Tickable;
            public UnityEngine.Object UnityObject;
            public float LastTickTime;
            public float NextTickTime;
        }

        public static RuntimeCoordinator Instance { get; private set; }

        [SerializeField] private RuntimeBudgetSettings budgetSettings;
        [SerializeField] private Transform observer;

        private readonly List<Entry> entries = new List<Entry>(128);
        private readonly Dictionary<IRuntimeTickable, Entry> lookup =
            new Dictionary<IRuntimeTickable, Entry>(128);
        private int cursor;
        private int ticksExecutedLastFrame;
        private int entriesInspectedLastFrame;

        public int RegisteredCount => entries.Count;
        public int TicksExecutedLastFrame => ticksExecutedLastFrame;
        public int EntriesInspectedLastFrame => entriesInspectedLastFrame;

        public void Configure(RuntimeBudgetSettings settings, Transform observerTransform)
        {
            budgetSettings = settings;
            observer = observerTransform;
        }

        public void SetObserver(Transform observerTransform)
        {
            observer = observerTransform;
        }

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

            entries.Clear();
            lookup.Clear();
        }

        private void Update()
        {
            ticksExecutedLastFrame = 0;
            entriesInspectedLastFrame = 0;

            if (entries.Count == 0)
                return;

            int tickBudget = budgetSettings != null
                ? budgetSettings.MaximumTicksPerFrame
                : 16;
            int inspectionBudget = budgetSettings != null
                ? budgetSettings.MaximumEntriesInspectedPerFrame
                : 64;
            inspectionBudget = Mathf.Min(inspectionBudget, entries.Count);

            float now = Time.time;
            Vector3 observerPosition = observer != null ? observer.position : Vector3.zero;

            while (entries.Count > 0 &&
                   ticksExecutedLastFrame < tickBudget &&
                   entriesInspectedLastFrame < inspectionBudget)
            {
                if (cursor >= entries.Count)
                    cursor = 0;

                Entry entry = entries[cursor];
                cursor++;
                entriesInspectedLastFrame++;

                if (entry.UnityObject == null)
                {
                    RemoveDeadEntry(entry);
                    continue;
                }

                IRuntimeTickable tickable = entry.Tickable;
                if (!tickable.RuntimeTickEnabled || now < entry.NextTickTime)
                    continue;

                Transform targetTransform = tickable.RuntimeTransform;
                float squaredDistance = targetTransform == null || observer == null
                    ? 0f
                    : (targetTransform.position - observerPosition).sqrMagnitude;

                float interval = Mathf.Max(0f, tickable.MinimumTickInterval);
                if (tickable.UseDistanceScaling && budgetSettings != null)
                    interval = Mathf.Max(interval, budgetSettings.GetDistanceInterval(squaredDistance));

                float delta = entry.LastTickTime <= 0f
                    ? Mathf.Max(interval, Time.deltaTime)
                    : Mathf.Max(0.0001f, now - entry.LastTickTime);

                RuntimeTickContext context = new RuntimeTickContext(
                    delta,
                    now,
                    Time.frameCount,
                    observerPosition,
                    squaredDistance);

                try
                {
                    tickable.RuntimeTick(in context);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, entry.UnityObject);
                }

                entry.LastTickTime = now;
                entry.NextTickTime = now + interval;
                ticksExecutedLastFrame++;
            }
        }

        public bool Register(IRuntimeTickable tickable)
        {
            if (tickable == null || lookup.ContainsKey(tickable))
                return false;

            UnityEngine.Object unityObject = tickable as UnityEngine.Object;
            if (unityObject == null)
                throw new ArgumentException("Runtime tickables must be Unity objects.", nameof(tickable));

            Entry entry = new Entry
            {
                Tickable = tickable,
                UnityObject = unityObject,
                LastTickTime = 0f,
                NextTickTime = Time.time
            };

            lookup.Add(tickable, entry);
            entries.Add(entry);
            return true;
        }

        public bool Unregister(IRuntimeTickable tickable)
        {
            if (tickable == null || !lookup.TryGetValue(tickable, out Entry entry))
                return false;

            lookup.Remove(tickable);
            int index = entries.IndexOf(entry);
            if (index >= 0)
            {
                entries.RemoveAt(index);
                if (index < cursor)
                    cursor--;
                cursor = Mathf.Max(0, cursor);
            }

            return true;
        }

        private void RemoveDeadEntry(Entry entry)
        {
            if (entry.Tickable != null)
                lookup.Remove(entry.Tickable);

            int index = entries.IndexOf(entry);
            if (index < 0)
                return;

            entries.RemoveAt(index);
            if (index < cursor)
                cursor--;
            cursor = Mathf.Max(0, cursor);
        }
    }
}
