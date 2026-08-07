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

        public float ElapsedSeconds => Mathf.Min(Time.time - startTime, duration);
        public TimeSpan RemainingTime => TimeSpan.FromSeconds(Mathf.Max(0, duration - (Time.time - startTime)));
        public bool IsExpired => isExpired;

        public event Action OnTimeExpired;
        public event Action<float> OnTimeUpdated;

        private void Start()
        {
            startTime = Time.time;
        }

        private void Update()
        {
            if (isExpired) return;

            float elapsed = Time.time - startTime;
            float cappedElapsed = Mathf.Min(elapsed, duration);
            OnTimeUpdated?.Invoke(cappedElapsed);

            if (elapsed >= duration)
            {
                isExpired = true;
                OnTimeExpired?.Invoke();
            }
        }
    }
}
