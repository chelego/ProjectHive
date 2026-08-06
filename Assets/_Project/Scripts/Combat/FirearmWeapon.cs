using ProjectHive.Core.Contracts;
using ProjectHive.Core.Events;
using UnityEngine;

namespace ProjectHive.Combat
{
    [DisallowMultipleComponent]
    public sealed class FirearmWeapon : MonoBehaviour
    {
        [SerializeField] private Transform muzzle;
        [SerializeField] private float damage = 25f;
        [SerializeField] private float fireRate = 6f;
        [SerializeField] private float maxDistance = 120f;
        [SerializeField] private float muzzleVelocity = 260f;
        [SerializeField] private float gravity = 9.81f;
        [SerializeField] private float spreadDegrees = 0.75f;
        [SerializeField] private int trajectorySteps = 24;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private float gunshotLoudness = 1f;
        [SerializeField] private float gunshotRadius = 55f;

        private float nextFireTime;

        public bool CanFire => Time.time >= nextFireTime;

        public bool TryFire(GameObject owner, Vector3 origin, Vector3 forward, out RaycastHit hit)
        {
            hit = default;
            if (!CanFire)
                return false;

            nextFireTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);

            Vector3 shotOrigin = muzzle != null ? muzzle.position : origin;
            Vector3 shotDirection = ApplySpread(forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward);

            EmitGunshot(owner, shotOrigin);

            if (!TryBallisticHit(owner, shotOrigin, shotDirection, out hit))
                return true;

            if (CombatHitUtility.TryGetDamageable(hit.collider, out IDamageable damageable, out GameObject target) &&
                !CombatHitUtility.IsSelfHit(owner, target))
            {
                DamageData damageData = new DamageData(
                    damage,
                    DamageKind.Projectile,
                    hit.point,
                    shotDirection,
                    owner);
                damageable.ApplyDamage(in damageData);
            }

            return true;
        }

        private bool TryBallisticHit(GameObject owner, Vector3 origin, Vector3 direction, out RaycastHit hit)
        {
            hit = default;
            Vector3 previous = origin;
            float stepTime = maxDistance / Mathf.Max(1f, muzzleVelocity) / Mathf.Max(1, trajectorySteps);

            for (int i = 1; i <= trajectorySteps; i++)
            {
                float t = stepTime * i;
                Vector3 current = origin +
                                  direction * (muzzleVelocity * t) +
                                  Vector3.down * (0.5f * gravity * t * t);
                Vector3 segment = current - previous;
                float segmentDistance = segment.magnitude;

                if (segmentDistance > 0.001f &&
                    Physics.Raycast(previous, segment / segmentDistance, out hit, segmentDistance, hitMask, QueryTriggerInteraction.Ignore))
                {
                    if (owner == null || !hit.transform.IsChildOf(owner.transform))
                        return true;
                }

                previous = current;
            }

            return false;
        }

        private Vector3 ApplySpread(Vector3 forward)
        {
            if (spreadDegrees <= 0f)
                return forward;

            Vector2 random = Random.insideUnitCircle * spreadDegrees;
            Quaternion rotation = Quaternion.Euler(random.y, random.x, 0f);
            return rotation * forward;
        }

        private void EmitGunshot(GameObject owner, Vector3 position)
        {
            GameEventBus bus = GameEventBus.Instance;
            if (bus == null)
                return;

            NoiseEvent noise = new NoiseEvent(
                position,
                gunshotLoudness,
                gunshotRadius,
                NoiseCategory.Gunshot,
                NoiseAffiliation.Player,
                owner != null ? owner.GetInstanceID() : gameObject.GetInstanceID(),
                Time.time);
            bus.PublishNoise(in noise);
        }

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            fireRate = Mathf.Max(0.01f, fireRate);
            maxDistance = Mathf.Max(1f, maxDistance);
            muzzleVelocity = Mathf.Max(1f, muzzleVelocity);
            gravity = Mathf.Max(0f, gravity);
            spreadDegrees = Mathf.Max(0f, spreadDegrees);
            trajectorySteps = Mathf.Clamp(trajectorySteps, 1, 128);
        }
    }
}
