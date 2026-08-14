using System;
using UnityEngine;

namespace ProjectHive.AI.Hive
{
    [Flags]
    public enum HiveZoneTraits
    {
        None = 0,
        Outdoor = 1 << 0,
        Interior = 1 << 1,
        Extraction = 1 << 2,
        ChokePoint = 1 << 3,
        HighValueLoot = 1 << 4
    }

    [Serializable]
    public readonly struct HiveZoneSnapshot
    {
        public HiveZoneSnapshot(
            int zoneId,
            Vector3 center,
            float radius,
            HiveZoneTraits traits)
        {
            ZoneId = zoneId;
            Center = center;
            Radius = Mathf.Max(0f, radius);
            Traits = traits;
        }

        public int ZoneId { get; }
        public Vector3 Center { get; }
        public float Radius { get; }
        public HiveZoneTraits Traits { get; }

        public bool HasTraits(HiveZoneTraits required)
        {
            return (Traits & required) == required;
        }
    }

    public interface IHiveZoneProvider
    {
        int ZoneCount { get; }
        bool TryGetZone(int zoneId, out HiveZoneSnapshot zone);
        bool TryFindZone(Vector3 worldPosition, out HiveZoneSnapshot zone);
        bool TryGetTravelDistance(int fromZoneId, int toZoneId, out float distance);
    }
}
