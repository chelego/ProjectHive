using ProjectHive.Core.Contracts;
using UnityEngine;

namespace ProjectHive.Combat
{
    [DisallowMultipleComponent]
    public sealed class AssassinationAttack : MonoBehaviour
    {
        [SerializeField] private float damage = 999f;
        [SerializeField] private float range = 1.6f;
        [SerializeField] private float radius = 0.35f;
        [SerializeField] private float rearAngle = 70f;
        [SerializeField] private LayerMask targetMask = ~0;

        public bool TryAssassinate(GameObject owner, Vector3 origin, Vector3 forward, out GameObject target)
        {
            target = null;
            Vector3 direction = forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;

            if (!Physics.SphereCast(origin, radius, direction, out RaycastHit hit, range, targetMask, QueryTriggerInteraction.Ignore))
                return false;

            if (!CombatHitUtility.TryGetDamageable(hit.collider, out IDamageable damageable, out target))
                return false;

            if (CombatHitUtility.IsSelfHit(owner, target) || damageable.IsDead)
                return false;

            if (!IsBehindTarget(origin, target.transform))
                return false;

            DamageData damageData = new DamageData(
                damage,
                DamageKind.Assassination,
                hit.point,
                direction,
                owner);
            damageable.ApplyDamage(in damageData);
            return true;
        }

        private bool IsBehindTarget(Vector3 attackerPosition, Transform target)
        {
            Vector3 toAttacker = attackerPosition - target.position;
            toAttacker.y = 0f;

            if (toAttacker.sqrMagnitude <= 0.0001f)
                return false;

            float angle = Vector3.Angle(-target.forward, toAttacker.normalized);
            return angle <= rearAngle * 0.5f;
        }

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            range = Mathf.Max(0.1f, range);
            radius = Mathf.Max(0.01f, radius);
            rearAngle = Mathf.Clamp(rearAngle, 1f, 180f);
        }
    }
}
