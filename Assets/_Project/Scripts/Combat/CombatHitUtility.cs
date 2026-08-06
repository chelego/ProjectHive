using ProjectHive.Core.Contracts;
using UnityEngine;

namespace ProjectHive.Combat
{
    public static class CombatHitUtility
    {
        public static bool TryGetDamageable(Collider collider, out IDamageable damageable, out GameObject targetRoot)
        {
            damageable = null;
            targetRoot = null;

            if (collider == null)
                return false;

            damageable = collider.GetComponentInParent<IDamageable>();
            if (damageable == null)
                return false;

            Component component = damageable as Component;
            targetRoot = component != null ? component.gameObject : collider.gameObject;
            return true;
        }

        public static bool IsSelfHit(GameObject source, GameObject target)
        {
            if (source == null || target == null)
                return false;

            return source == target || target.transform.IsChildOf(source.transform);
        }
    }
}
