using ProjectHive.Core.Contracts;
using ProjectHive.Core.Events;
using UnityEngine;

namespace ProjectHive.Combat
{
    [DisallowMultipleComponent]
    public sealed class MeleeWeapon : MonoBehaviour
    {
        [SerializeField] private float damage = 35f;
        [SerializeField] private float range = 1.9f;
        [SerializeField] private float radius = 0.45f;
        [SerializeField] private float cooldown = 0.55f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private float impactNoiseRadius = 12f;

        private float nextAttackTime;

        public bool CanAttack => Time.time >= nextAttackTime;

        public bool TryAttack(GameObject owner, Vector3 origin, Vector3 forward, out RaycastHit hit)
        {
            hit = default;
            if (!CanAttack)
                return false;

            nextAttackTime = Time.time + cooldown;

            Vector3 direction = forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
            if (!Physics.SphereCast(origin, radius, direction, out hit, range, hitMask, QueryTriggerInteraction.Ignore))
                return true;

            if (owner != null && hit.transform.IsChildOf(owner.transform))
                return true;

            if (CombatHitUtility.TryGetDamageable(hit.collider, out IDamageable damageable, out GameObject target) &&
                !CombatHitUtility.IsSelfHit(owner, target))
            {
                DamageData damageData = new DamageData(
                    damage,
                    DamageKind.Melee,
                    hit.point,
                    direction,
                    owner);
                damageable.ApplyDamage(in damageData);
            }

            EmitImpact(owner, hit.point);
            return true;
        }

        private void EmitImpact(GameObject owner, Vector3 position)
        {
            GameEventBus bus = GameEventBus.Instance;
            if (bus == null)
                return;

            NoiseEvent noise = new NoiseEvent(
                position,
                0.45f,
                impactNoiseRadius,
                NoiseCategory.MeleeImpact,
                NoiseAffiliation.Player,
                owner != null ? owner.GetInstanceID() : gameObject.GetInstanceID(),
                Time.time);
            bus.PublishNoise(in noise);
        }

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            range = Mathf.Max(0.1f, range);
            radius = Mathf.Max(0.01f, radius);
            cooldown = Mathf.Max(0.01f, cooldown);
            impactNoiseRadius = Mathf.Max(0f, impactNoiseRadius);
        }
    }
}
