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

    public enum DamageHitZone
    {
        Body = 0,
        Head = 1,
        WeakPoint = 2
    }

    [Flags]
    public enum DamageFlags
    {
        None = 0,
        CanCauseBleeding = 1 << 0,
        BypassArmor = 1 << 1,
        Critical = 1 << 2
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
            : this(
                amount,
                kind,
                DamageHitZone.Body,
                DamageFlags.None,
                hitPoint,
                direction,
                source)
        {
        }

        public DamageData(
            float amount,
            DamageKind kind,
            DamageHitZone hitZone,
            DamageFlags flags,
            Vector3 hitPoint,
            Vector3 direction,
            GameObject source)
        {
            Amount = Mathf.Max(0f, amount);
            Kind = kind;
            HitZone = hitZone;
            Flags = flags;
            HitPoint = hitPoint;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
            Source = source;
        }

        public float Amount { get; }
        public DamageKind Kind { get; }
        public DamageHitZone HitZone { get; }
        public DamageFlags Flags { get; }
        public Vector3 HitPoint { get; }
        public Vector3 Direction { get; }
        public GameObject Source { get; }

        public bool HasFlag(DamageFlags flag)
        {
            return (Flags & flag) == flag;
        }
    }
}
