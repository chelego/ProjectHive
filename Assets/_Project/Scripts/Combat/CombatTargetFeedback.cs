using System.Collections;
using UnityEngine;

namespace ProjectHive.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class CombatTargetFeedback : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color healthyColor = new Color(0.16f, 0.7f, 0.35f, 1f);
        [SerializeField] private Color woundedColor = new Color(0.95f, 0.48f, 0.12f, 1f);
        [SerializeField] private Color deadColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField] private float flashDuration = 0.08f;
        [SerializeField] private Rigidbody impulseBody;
        [SerializeField] private float hitImpulse = 3.5f;

        private Health health;
        private Coroutine flashRoutine;
        private Material runtimeMaterial;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();
            if (impulseBody == null)
                impulseBody = GetComponent<Rigidbody>();

            if (targetRenderer != null)
                runtimeMaterial = targetRenderer.material;

            ApplyHealthColor();
        }

        private void OnEnable()
        {
            if (health == null)
                health = GetComponent<Health>();

            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
        }

        private void OnDisable()
        {
            if (health == null)
                return;

            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
        }

        private void HandleDamaged(Health damagedHealth, Core.Contracts.DamageData damage)
        {
            if (impulseBody != null && damage.Direction.sqrMagnitude > 0.001f)
                impulseBody.AddForce(damage.Direction * hitImpulse, ForceMode.Impulse);

            if (flashRoutine != null)
                StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashThenApplyHealthColor());
        }

        private void HandleDied(Health deadHealth)
        {
            if (runtimeMaterial != null)
                runtimeMaterial.color = deadColor;
        }

        private IEnumerator FlashThenApplyHealthColor()
        {
            if (runtimeMaterial != null)
                runtimeMaterial.color = hitFlashColor;

            yield return new WaitForSeconds(flashDuration);
            ApplyHealthColor();
            flashRoutine = null;
        }

        private void ApplyHealthColor()
        {
            if (runtimeMaterial == null || health == null)
                return;

            runtimeMaterial.color = health.IsDead ? deadColor : Color.Lerp(woundedColor, healthyColor, health.Normalized);
        }

        private void OnValidate()
        {
            flashDuration = Mathf.Max(0.01f, flashDuration);
            hitImpulse = Mathf.Max(0f, hitImpulse);
        }
    }
}
