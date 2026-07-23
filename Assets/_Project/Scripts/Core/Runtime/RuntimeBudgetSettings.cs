using UnityEngine;

namespace ProjectHive.Core.Runtime
{
    [CreateAssetMenu(fileName = "RuntimeBudgetSettings", menuName = "Project Hive/Runtime/Budget Settings")]
    public sealed class RuntimeBudgetSettings : ScriptableObject
    {
        [Header("Per-frame budget")]
        [SerializeField, Min(1)] private int maximumTicksPerFrame = 16;
        [SerializeField, Min(1)] private int maximumEntriesInspectedPerFrame = 64;

        [Header("Distance bands")]
        [SerializeField, Min(0f)] private float nearDistance = 20f;
        [SerializeField, Min(0f)] private float mediumDistance = 50f;
        [SerializeField, Min(0f)] private float farDistance = 100f;

        [Header("Minimum update intervals")]
        [SerializeField, Min(0f)] private float nearInterval = 0.05f;
        [SerializeField, Min(0f)] private float mediumInterval = 0.2f;
        [SerializeField, Min(0f)] private float farInterval = 0.75f;
        [SerializeField, Min(0f)] private float dormantInterval = 2f;

        public int MaximumTicksPerFrame => maximumTicksPerFrame;
        public int MaximumEntriesInspectedPerFrame => maximumEntriesInspectedPerFrame;

        public float GetDistanceInterval(float squaredDistance)
        {
            if (squaredDistance <= nearDistance * nearDistance)
                return nearInterval;
            if (squaredDistance <= mediumDistance * mediumDistance)
                return mediumInterval;
            if (squaredDistance <= farDistance * farDistance)
                return farInterval;
            return dormantInterval;
        }

        private void OnValidate()
        {
            maximumTicksPerFrame = Mathf.Max(1, maximumTicksPerFrame);
            maximumEntriesInspectedPerFrame = Mathf.Max(maximumTicksPerFrame, maximumEntriesInspectedPerFrame);

            nearDistance = Mathf.Max(0f, nearDistance);
            mediumDistance = Mathf.Max(nearDistance, mediumDistance);
            farDistance = Mathf.Max(mediumDistance, farDistance);

            nearInterval = Mathf.Max(0f, nearInterval);
            mediumInterval = Mathf.Max(nearInterval, mediumInterval);
            farInterval = Mathf.Max(mediumInterval, farInterval);
            dormantInterval = Mathf.Max(farInterval, dormantInterval);
        }
    }
}
