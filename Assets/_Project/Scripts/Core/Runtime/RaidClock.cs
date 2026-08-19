using UnityEngine;
using ProjectHive.Core.Contracts;
using System;

namespace ProjectHive.Core.Runtime
{
    [DisallowMultipleComponent]
    public class RaidClock : MonoBehaviour, IRaidClock
    {
        [SerializeField, Min(1f)] private float durationSeconds = 180f;
        [SerializeField, Range(0, 23)] private int startWorldHour = 23;
        [SerializeField, Range(0, 23)] private int endWorldHour = 5;

        private float startTime;
        private bool isExpired;
        private bool hasStarted;

        public RaidClockSnapshot ClockState { get; private set; }
        public float DurationSeconds => durationSeconds;
        public float ElapsedSeconds => hasStarted ? Mathf.Min(Time.time - startTime, durationSeconds) : 0f;
        public TimeSpan RemainingTime => TimeSpan.FromSeconds(Mathf.Max(0f, durationSeconds - ElapsedSeconds));
        public bool IsExpired => isExpired;

        public event Action<RaidClockSnapshot> ClockChanged;

        private void Start()
        {
            Begin();
        }

        public void Configure(float realDurationSeconds, int worldStartHour, int worldEndHour)
        {
            durationSeconds = Mathf.Max(1f, realDurationSeconds);
            startWorldHour = Mathf.Abs(worldStartHour) % 24;
            endWorldHour = Mathf.Abs(worldEndHour) % 24;

            if (hasStarted)
                Begin();
        }

        public void Begin()
        {
            startTime = Time.time;
            isExpired = false;
            hasStarted = true;
            UpdateClockState(0f);
        }

        private void Update()
        {
            if (isExpired) return;

            float elapsed = Time.time - startTime;
            float cappedElapsed = Mathf.Min(elapsed, durationSeconds);
            UpdateClockState(cappedElapsed);

            if (elapsed >= durationSeconds)
            {
                isExpired = true;
                UpdateClockState(durationSeconds);
            }
        }

        private void UpdateClockState(float elapsedSeconds)
        {
            float remainingSeconds = Mathf.Max(0f, durationSeconds - elapsedSeconds);
            int worldHours = (endWorldHour - startWorldHour + 24) % 24;
            if (worldHours == 0)
                worldHours = 24;
            float progress = Mathf.Clamp01(elapsedSeconds / Mathf.Max(1f, durationSeconds));
            float totalMinutes = progress * worldHours * 60f;
            int hours = (startWorldHour + (int)(totalMinutes / 60f)) % 24;
            int minutes = (int)(totalMinutes % 60f);

            ClockState = new RaidClockSnapshot(
                elapsedSeconds,
                remainingSeconds,
                hours,
                minutes);
            ClockChanged?.Invoke(ClockState);
        }

        private void OnValidate()
        {
            durationSeconds = Mathf.Max(1f, durationSeconds);
            startWorldHour = Mathf.Abs(startWorldHour) % 24;
            endWorldHour = Mathf.Abs(endWorldHour) % 24;
        }
    }
}
