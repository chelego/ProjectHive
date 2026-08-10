using UnityEngine;
using ProjectHive.Core.Contracts;
using System;

namespace ProjectHive.Core.Runtime
{
    public class RaidClock : MonoBehaviour, IRaidClock
    {
        private float duration = 10f; // Updated to 10s
        private float startTime;
        private bool isExpired = false;

        public RaidClockSnapshot ClockState { get; private set; }
        public float ElapsedSeconds => Mathf.Min(Time.time - startTime, duration);
        public TimeSpan RemainingTime => TimeSpan.FromSeconds(Mathf.Max(0, duration - (Time.time - startTime)));
        public bool IsExpired => isExpired;

        public event Action<RaidClockSnapshot> ClockChanged;

        private void Start()
        {
            startTime = Time.time;
            UpdateClockState(0f);
        }

        private void Update()
        {
            if (isExpired) return;

            float elapsed = Time.time - startTime;
            float cappedElapsed = Mathf.Min(elapsed, duration);
            UpdateClockState(cappedElapsed);

            if (elapsed >= duration)
            {
                isExpired = true;
                UpdateClockState(duration);
            }
        }

        private void UpdateClockState(float elapsedSeconds)
        {
            float remainingSeconds = Mathf.Max(0f, duration - elapsedSeconds);
            float totalMinutes = (elapsedSeconds / duration) * 7f * 60f;
            int hours = (23 + (int)(totalMinutes / 60f)) % 24;
            int minutes = (int)(totalMinutes % 60f);

            ClockState = new RaidClockSnapshot(
                elapsedSeconds,
                remainingSeconds,
                hours,
                minutes);
            ClockChanged?.Invoke(ClockState);
        }
    }
}
