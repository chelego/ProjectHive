using UnityEngine;

namespace ProjectHive.AI.Hive
{
    public interface IHiveRaidContext
    {
        bool ExtractionActive { get; }
        float NormalizedRemainingTime { get; }
        bool TryGetActiveExtractionPosition(out Vector3 position);
    }
}
