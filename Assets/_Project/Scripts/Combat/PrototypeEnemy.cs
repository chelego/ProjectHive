using System.Collections;
using UnityEngine;

namespace ProjectHive.Prototype
{
    [RequireComponent(typeof(PrototypeHealth))]
    [DisallowMultipleComponent]
    public sealed class PrototypeEnemy : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;
        [SerializeField, Min(0.1f)] private float deathDuration = 0.9f;
        [SerializeField, Min(0f)] private float hitFlashDuration = 0.09f;

        private MaterialPropertyBlock propertyBlock;
        private PrototypeHealth health;
        private Collider[] colliders;
        private Color baseColor = new Color(0.34f, 0.08f, 0.07f);
        private Coroutine flashRoutine;

        public void Configure(Renderer[] enemyRenderers)
        {
            renderers = enemyRenderers;
        }

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            health = GetComponent<PrototypeHealth>();
            colliders = GetComponentsInChildren<Collider>(true);

            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health == null)
                return;

            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void OnDamaged(Vector3 hitPoint, Vector3 direction)
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);

            flashRoutine = StartCoroutine(FlashHit());
        }

        private void OnDied(Vector3 hitPoint, Vector3 direction)
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);

            for (int index = 0; index < colliders.Length; index++)
                colliders[index].enabled = false;

            StartCoroutine(PlayDeath(direction));
        }

        private IEnumerator FlashHit()
        {
            SetColor(new Color(1f, 0.65f, 0.55f), 1.5f);
            yield return new WaitForSeconds(hitFlashDuration);
            SetColor(baseColor, 0f);
            flashRoutine = null;
        }

        private IEnumerator PlayDeath(Vector3 direction)
        {
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            Vector3 startScale = transform.localScale;
            Vector3 fallAxis = Vector3.Cross(Vector3.up, direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward);
            if (fallAxis.sqrMagnitude < 0.01f)
                fallAxis = Vector3.right;

            Quaternion endRotation = Quaternion.AngleAxis(82f, fallAxis.normalized) * startRotation;
            float elapsed = 0f;

            while (elapsed < deathDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / deathDuration);
                float eased = 1f - Mathf.Pow(1f - normalized, 3f);

                transform.rotation = Quaternion.Slerp(startRotation, endRotation, eased);
                transform.position = Vector3.Lerp(startPosition, startPosition + Vector3.down * 0.58f, eased);
                transform.localScale = Vector3.Lerp(startScale, startScale * 0.82f, normalized);
                SetColor(Color.Lerp(baseColor, new Color(0.025f, 0.02f, 0.02f), normalized), 0f);
                yield return null;
            }

            gameObject.SetActive(false);
        }

        private void SetColor(Color color, float emissionStrength)
        {
            if (renderers == null)
                return;

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer target = renderers[index];
                if (target == null)
                    continue;

                target.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_EmissionColor", color * emissionStrength);
                target.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
