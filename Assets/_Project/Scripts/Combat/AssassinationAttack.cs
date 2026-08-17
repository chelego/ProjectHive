using ProjectHive.Core.Contracts;
using UnityEngine;

namespace ProjectHive.Combat
{
    [DisallowMultipleComponent]
    public sealed class AssassinationAttack : MonoBehaviour
    {
        [SerializeField] private float range = 1.6f;
        [SerializeField] private float radius = 0.35f;
        [SerializeField] private LayerMask targetMask = ~0;

        public bool TryAssassinate(GameObject owner, Vector3 origin, Vector3 forward, out GameObject target)
        {
            target = null;
            Vector3 direction = forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;

            if (!Physics.SphereCast(origin, radius, direction, out RaycastHit hit, range, targetMask, QueryTriggerInteraction.Ignore))
                return false;

            AssassinationInteractable assassinationTarget = hit.collider.GetComponentInParent<AssassinationInteractable>();
            if (assassinationTarget == null)
                return false;

            target = assassinationTarget.gameObject;

            InteractionContext context = new InteractionContext(owner, origin, direction);
            if (!assassinationTarget.TryAssassinate(in context))
                return false;

            return true;
        }

        private void OnValidate()
        {
            range = Mathf.Max(0.1f, range);
            radius = Mathf.Max(0.01f, radius);
        }
    }
}
