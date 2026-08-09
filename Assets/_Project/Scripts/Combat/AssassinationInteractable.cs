using ProjectHive.Core.Contracts;
using UnityEngine;

namespace ProjectHive.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class AssassinationInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Assassinate";
        [SerializeField] private float damage = 999f;
        [SerializeField] private float maxDistance = 1.8f;
        [SerializeField] private float rearAngle = 80f;
        [SerializeField] private bool requireBehindTarget = true;

        private Health health;

        public string InteractionPrompt => health != null && health.IsDead ? string.Empty : prompt;

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        public bool CanInteract(in InteractionContext context)
        {
            if (health == null || health.IsDead || context.Interactor == null)
                return false;

            Vector3 toTarget = transform.position - context.Origin;
            if (toTarget.sqrMagnitude > maxDistance * maxDistance)
                return false;

            if (!requireBehindTarget)
                return true;

            return IsInteractorBehind(context.Origin);
        }

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(in context))
                return;

            Vector3 direction = transform.position - context.Origin;
            DamageData damageData = new DamageData(
                damage,
                DamageKind.Assassination,
                transform.position + Vector3.up,
                direction,
                context.Interactor);
            health.ApplyDamage(in damageData);
        }

        private bool IsInteractorBehind(Vector3 interactorPosition)
        {
            Vector3 toInteractor = interactorPosition - transform.position;
            toInteractor.y = 0f;

            if (toInteractor.sqrMagnitude <= 0.0001f)
                return false;

            float angle = Vector3.Angle(-transform.forward, toInteractor.normalized);
            return angle <= rearAngle * 0.5f;
        }

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            maxDistance = Mathf.Max(0.1f, maxDistance);
            rearAngle = Mathf.Clamp(rearAngle, 1f, 180f);
        }
    }
}
