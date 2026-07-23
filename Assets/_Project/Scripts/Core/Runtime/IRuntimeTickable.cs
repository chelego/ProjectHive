using UnityEngine;

namespace ProjectHive.Core.Runtime
{
    public interface IRuntimeTickable
    {
        bool RuntimeTickEnabled { get; }
        Transform RuntimeTransform { get; }
        float MinimumTickInterval { get; }
        bool UseDistanceScaling { get; }
        void RuntimeTick(in RuntimeTickContext context);
    }
}
