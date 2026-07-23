using System;
using UnityEngine;

namespace ProjectHive.Core.Runtime
{
    [Serializable]
    public readonly struct RuntimeTickContext
    {
        public RuntimeTickContext(
            float deltaTime,
            float currentTime,
            int frameIndex,
            Vector3 observerPosition,
            float squaredDistanceToObserver)
        {
            DeltaTime = deltaTime;
            CurrentTime = currentTime;
            FrameIndex = frameIndex;
            ObserverPosition = observerPosition;
            SquaredDistanceToObserver = squaredDistanceToObserver;
        }

        public float DeltaTime { get; }
        public float CurrentTime { get; }
        public int FrameIndex { get; }
        public Vector3 ObserverPosition { get; }
        public float SquaredDistanceToObserver { get; }
    }
}
