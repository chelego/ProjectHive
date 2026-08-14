using System.Collections.Generic;
using UnityEngine;

namespace ProjectHive.AI.Hive
{
    [DefaultExecutionOrder(-450)]
    [DisallowMultipleComponent]
    public sealed class HiveUnitRegistry : MonoBehaviour
    {
        private readonly List<IHiveControllable> units = new List<IHiveControllable>(32);
        private readonly HashSet<IHiveControllable> lookup = new HashSet<IHiveControllable>();

        public int RegisteredCount => units.Count;

        public bool Register(IHiveControllable unit)
        {
            if (unit == null || !lookup.Add(unit))
                return false;

            units.Add(unit);
            return true;
        }

        public bool Unregister(IHiveControllable unit)
        {
            if (unit == null || !lookup.Remove(unit))
                return false;

            units.Remove(unit);
            return true;
        }

        public int CollectNearestAvailable(
            in HiveCommand command,
            HiveUnitCapabilities requiredCapabilities,
            float mobilizationRadius,
            List<IHiveControllable> output,
            List<float> distanceBuffer)
        {
            if (output == null || distanceBuffer == null)
                return 0;

            output.Clear();
            distanceBuffer.Clear();

            int limit = command.RequestedUnitCount;
            if (limit <= 0)
                return 0;

            float maximumDistanceSquared =
                mobilizationRadius > 0f
                    ? mobilizationRadius * mobilizationRadius
                    : float.PositiveInfinity;

            for (int index = units.Count - 1; index >= 0; index--)
            {
                IHiveControllable unit = units[index];
                if (!TryGetLiveSnapshot(unit, out HiveUnitSnapshot snapshot))
                {
                    RemoveAt(index, unit);
                    continue;
                }

                if (!snapshot.IsAvailable ||
                    !snapshot.HasCapabilities(requiredCapabilities))
                {
                    continue;
                }

                float distanceSquared =
                    (snapshot.Position - command.TargetPosition).sqrMagnitude;
                if (distanceSquared > maximumDistanceSquared)
                    continue;

                InsertNearest(
                    unit,
                    distanceSquared,
                    limit,
                    output,
                    distanceBuffer);
            }

            return output.Count;
        }

        public HiveUnitGroupSummary GetSummary(
            Vector3 targetPosition,
            float normalizationDistance)
        {
            int available = 0;
            int hunters = 0;
            int scouts = 0;
            int heavies = 0;
            float minimumDistance = float.PositiveInfinity;
            float distanceSum = 0f;

            for (int index = units.Count - 1; index >= 0; index--)
            {
                IHiveControllable unit = units[index];
                if (!TryGetLiveSnapshot(unit, out HiveUnitSnapshot snapshot))
                {
                    RemoveAt(index, unit);
                    continue;
                }

                if (!snapshot.IsAvailable)
                    continue;

                available++;
                switch (snapshot.Role)
                {
                    case HiveUnitRole.Hunter:
                        hunters++;
                        break;
                    case HiveUnitRole.Scout:
                        scouts++;
                        break;
                    case HiveUnitRole.Heavy:
                        heavies++;
                        break;
                }

                float distance = Vector3.Distance(snapshot.Position, targetPosition);
                minimumDistance = Mathf.Min(minimumDistance, distance);
                distanceSum += distance;
            }

            float scale = Mathf.Max(1f, normalizationDistance);
            return new HiveUnitGroupSummary(
                available,
                hunters,
                scouts,
                heavies,
                available > 0 ? Mathf.Clamp01(minimumDistance / scale) : 1f,
                available > 0 ? Mathf.Clamp01(distanceSum / available / scale) : 1f);
        }

        private static bool TryGetLiveSnapshot(
            IHiveControllable unit,
            out HiveUnitSnapshot snapshot)
        {
            if (unit is Object unityObject && unityObject == null)
            {
                snapshot = default;
                return false;
            }

            snapshot = unit.GetHiveUnitSnapshot();
            return true;
        }

        private void RemoveAt(int index, IHiveControllable unit)
        {
            units.RemoveAt(index);
            if (unit != null)
                lookup.Remove(unit);
        }

        private static void InsertNearest(
            IHiveControllable unit,
            float distanceSquared,
            int limit,
            List<IHiveControllable> output,
            List<float> distanceBuffer)
        {
            int insertIndex = distanceBuffer.Count;
            while (insertIndex > 0 &&
                   distanceSquared < distanceBuffer[insertIndex - 1])
            {
                insertIndex--;
            }

            if (insertIndex >= limit)
                return;

            output.Insert(insertIndex, unit);
            distanceBuffer.Insert(insertIndex, distanceSquared);
            if (output.Count <= limit)
                return;

            int lastIndex = output.Count - 1;
            output.RemoveAt(lastIndex);
            distanceBuffer.RemoveAt(lastIndex);
        }
    }

    public readonly struct HiveUnitGroupSummary
    {
        public HiveUnitGroupSummary(
            int availableCount,
            int hunterCount,
            int scoutCount,
            int heavyCount,
            float normalizedMinimumDistance,
            float normalizedAverageDistance)
        {
            AvailableCount = availableCount;
            HunterCount = hunterCount;
            ScoutCount = scoutCount;
            HeavyCount = heavyCount;
            NormalizedMinimumDistance = normalizedMinimumDistance;
            NormalizedAverageDistance = normalizedAverageDistance;
        }

        public int AvailableCount { get; }
        public int HunterCount { get; }
        public int ScoutCount { get; }
        public int HeavyCount { get; }
        public float NormalizedMinimumDistance { get; }
        public float NormalizedAverageDistance { get; }
    }
}
