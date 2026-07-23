using System;
using UnityEngine;

namespace ProjectHive.Core.Contracts
{
    [Serializable]
    public readonly struct InteractionContext
    {
        public InteractionContext(GameObject interactor, Vector3 origin, Vector3 forward)
        {
            Interactor = interactor;
            Origin = origin;
            Forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        public GameObject Interactor { get; }
        public Vector3 Origin { get; }
        public Vector3 Forward { get; }
    }
}
