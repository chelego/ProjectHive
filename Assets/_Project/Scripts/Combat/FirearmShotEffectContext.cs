using UnityEngine;

namespace ProjectHive.Combat
{
    public readonly struct FirearmShotEffectContext
    {
        public FirearmShotEffectContext(
            GameObject owner,
            Vector3 shotOrigin,
            Vector3 muzzlePosition,
            Vector3 direction,
            float maxDistance,
            bool hasHit,
            Vector3 hitPoint,
            Vector3 hitNormal,
            Collider hitCollider,
            bool hitDamageable)
        {
            Owner = owner;
            ShotOrigin = shotOrigin;
            MuzzlePosition = muzzlePosition;
            Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            MaxDistance = Mathf.Max(0f, maxDistance);
            HasHit = hasHit;
            HitPoint = hitPoint;
            HitNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : -Direction;
            HitCollider = hitCollider;
            HitDamageable = hitDamageable;
        }

        public GameObject Owner { get; }
        public Vector3 ShotOrigin { get; }
        public Vector3 MuzzlePosition { get; }
        public Vector3 Direction { get; }
        public float MaxDistance { get; }
        public bool HasHit { get; }
        public Vector3 HitPoint { get; }
        public Vector3 HitNormal { get; }
        public Collider HitCollider { get; }
        public bool HitDamageable { get; }
        public Vector3 TracerEndPoint => HasHit ? HitPoint : ShotOrigin + Direction * MaxDistance;
    }
}
