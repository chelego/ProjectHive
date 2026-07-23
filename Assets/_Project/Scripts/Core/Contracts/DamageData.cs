using System;
using UnityEngine;

namespace ProjectHive.Core.Contracts
{
    public enum DamageKind
    {
        Unknown = 0,
        Melee = 1,
        Projectile = 2,
        Explosion = 3,
        Environment = 4,
        Monster = 5,
        Assassination = 6
    }

    [Serializable]
    public readonly struct DamageData
    {
        public DamageData(
            float amount,
            DamageKind kind,
            Vector3 hitPoint,
            Vector3 direction,
            GameObject source)
        {
            Amount = Mathf.Max(0f, amount);
            Kind = kind;
            HitPoint = hitPoint;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
            Source = source;
        }

        public float Amount { get; }
        public DamageKind Kind { get; }
        public Vector3 HitPoint { get; }
        public Vector3 Direction { get; }
        public GameObject Source { get; }
    }
}
