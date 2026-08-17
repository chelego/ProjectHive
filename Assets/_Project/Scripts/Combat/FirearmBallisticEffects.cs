using System.Collections;
using UnityEngine;

namespace ProjectHive.Combat
{
    [DisallowMultipleComponent]
    public sealed class FirearmBallisticEffects : MonoBehaviour
    {
        [Header("Optional Prefabs")]
        [SerializeField] private GameObject muzzleFlashPrefab;
        [SerializeField] private GameObject tracerPrefab;
        [SerializeField] private GameObject surfaceImpactPrefab;
        [SerializeField] private GameObject damageableImpactPrefab;

        [Header("Audio")]
        [SerializeField] private AudioClip gunshotClip;
        [SerializeField, Range(0f, 1f)] private float gunshotVolume = 0.75f;
        [SerializeField, Range(0.1f, 2f)] private float gunshotPitch = 1f;
        [SerializeField] private bool generateFallbackAudio = true;

        [Header("Fallback Visuals")]
        [SerializeField] private bool generateFallbackVisuals = true;
        [SerializeField] private Color muzzleFlashColor = new Color(1f, 0.68f, 0.18f, 1f);
        [SerializeField] private Color tracerColor = new Color(1f, 0.82f, 0.32f, 0.92f);
        [SerializeField] private Color surfaceImpactColor = new Color(1f, 0.56f, 0.14f, 1f);
        [SerializeField] private Color damageableImpactColor = new Color(1f, 0.88f, 0.72f, 1f);
        [SerializeField, Min(0.01f)] private float muzzleFlashLifetime = 0.05f;
        [SerializeField, Min(0.01f)] private float tracerLifetime = 0.06f;
        [SerializeField, Min(0.01f)] private float impactLifetime = 0.45f;
        [SerializeField, Min(0.001f)] private float tracerWidth = 0.025f;
        [SerializeField, Min(1f)] private float tracerMinimumLength = 0.35f;

        private AudioSource audioSource;
        private AudioClip fallbackGunshotClip;
        private Material tracerMaterial;

        private void Awake()
        {
            EnsureAudioSource();
        }

        public void PlayShot(in FirearmShotEffectContext context)
        {
            PlayMuzzleFlash(context.MuzzlePosition, context.Direction);
            PlayGunshot(context.MuzzlePosition);
            PlayTracer(context.MuzzlePosition, context.TracerEndPoint);

            if (context.HasHit)
                PlayImpact(context);
        }

        private void PlayMuzzleFlash(Vector3 position, Vector3 direction)
        {
            if (TrySpawnPrefab(muzzleFlashPrefab, position, Quaternion.LookRotation(direction), muzzleFlashLifetime))
                return;

            if (!generateFallbackVisuals)
                return;

            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "Runtime Muzzle Flash";
            flash.transform.SetPositionAndRotation(position + direction * 0.05f, Quaternion.identity);
            flash.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
            DestroyCollider(flash);
            ApplyMaterial(flash, muzzleFlashColor);
            StartCoroutine(FadeAndDestroy(flash, muzzleFlashLifetime, 1.6f));
        }

        private void PlayGunshot(Vector3 position)
        {
            EnsureAudioSource();
            if (audioSource == null)
                return;

            AudioClip clip = gunshotClip != null ? gunshotClip : GetOrCreateFallbackGunshot();
            if (clip == null)
                return;

            audioSource.pitch = gunshotPitch;
            audioSource.PlayOneShot(clip, gunshotVolume);
        }

        private void PlayTracer(Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            if (delta.sqrMagnitude < tracerMinimumLength * tracerMinimumLength)
                end = start + (delta.sqrMagnitude > 0.0001f ? delta.normalized : transform.forward) * tracerMinimumLength;

            if (TrySpawnTracerPrefab(start, end))
                return;

            if (!generateFallbackVisuals)
                return;

            GameObject tracer = new GameObject("Runtime Bullet Tracer");
            LineRenderer line = tracer.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = tracerWidth;
            line.endWidth = tracerWidth * 0.25f;
            line.material = GetTracerMaterial();
            line.startColor = tracerColor;
            line.endColor = new Color(tracerColor.r, tracerColor.g, tracerColor.b, 0f);
            StartCoroutine(DestroyAfter(tracer, tracerLifetime));
        }

        private void PlayImpact(in FirearmShotEffectContext context)
        {
            GameObject prefab = context.HitDamageable && damageableImpactPrefab != null
                ? damageableImpactPrefab
                : surfaceImpactPrefab;
            Quaternion rotation = Quaternion.LookRotation(context.HitNormal);
            if (TrySpawnPrefab(prefab, context.HitPoint + context.HitNormal * 0.01f, rotation, impactLifetime))
                return;

            if (!generateFallbackVisuals)
                return;

            Color color = context.HitDamageable ? damageableImpactColor : surfaceImpactColor;
            GameObject impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            impact.name = context.HitDamageable ? "Runtime Damageable Impact" : "Runtime Surface Impact";
            impact.transform.SetPositionAndRotation(context.HitPoint + context.HitNormal * 0.015f, rotation);
            impact.transform.localScale = context.HitDamageable
                ? new Vector3(0.16f, 0.16f, 0.16f)
                : new Vector3(0.11f, 0.11f, 0.11f);
            DestroyCollider(impact);
            ApplyMaterial(impact, color);
            StartCoroutine(FadeAndDestroy(impact, impactLifetime, 0.8f));

            for (int i = 0; i < 4; i++)
                SpawnFallbackFragment(context.HitPoint, context.HitNormal, color, i);
        }

        private bool TrySpawnPrefab(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime)
        {
            if (prefab == null)
                return false;

            GameObject instance = Instantiate(prefab, position, rotation);
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem system in systems)
                system.Play(true);

            StartCoroutine(DestroyAfter(instance, lifetime));
            return true;
        }

        private bool TrySpawnTracerPrefab(Vector3 start, Vector3 end)
        {
            if (tracerPrefab == null)
                return false;

            GameObject instance = Instantiate(tracerPrefab, start, Quaternion.LookRotation(end - start));
            LineRenderer line = instance.GetComponentInChildren<LineRenderer>();
            if (line != null)
            {
                line.positionCount = 2;
                line.useWorldSpace = true;
                line.SetPosition(0, start);
                line.SetPosition(1, end);
            }

            StartCoroutine(DestroyAfter(instance, tracerLifetime));
            return true;
        }

        private void SpawnFallbackFragment(Vector3 hitPoint, Vector3 hitNormal, Color color, int index)
        {
            GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fragment.name = "Runtime Impact Fragment";
            Vector3 tangent = Vector3.Cross(hitNormal, index % 2 == 0 ? Vector3.up : Vector3.right);
            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.Cross(hitNormal, Vector3.forward);
            tangent.Normalize();
            Vector3 direction = (hitNormal + Quaternion.AngleAxis(90f * index, hitNormal) * tangent * 0.65f).normalized;
            fragment.transform.position = hitPoint + hitNormal * 0.03f + direction * 0.04f;
            fragment.transform.rotation = Quaternion.LookRotation(direction);
            fragment.transform.localScale = new Vector3(0.025f, 0.025f, 0.09f);
            DestroyCollider(fragment);
            ApplyMaterial(fragment, color);
            StartCoroutine(MoveFadeAndDestroy(fragment, direction, impactLifetime));
        }

        private void EnsureAudioSource()
        {
            if (audioSource != null)
                return;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 70f;
        }

        private AudioClip GetOrCreateFallbackGunshot()
        {
            if (!generateFallbackAudio)
                return null;

            if (fallbackGunshotClip != null)
                return fallbackGunshotClip;

            const int sampleRate = 44100;
            const float duration = 0.12f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Exp(-38f * t);
                float crack = Mathf.Sin(2f * Mathf.PI * 160f * t) * 0.55f;
                float noise = Random.value * 2f - 1f;
                samples[i] = Mathf.Clamp((crack + noise * 0.75f) * envelope, -1f, 1f);
            }

            fallbackGunshotClip = AudioClip.Create("Runtime Gunshot Fallback", sampleCount, 1, sampleRate, false);
            fallbackGunshotClip.SetData(samples, 0);
            return fallbackGunshotClip;
        }

        private Material GetTracerMaterial()
        {
            if (tracerMaterial != null)
                return tracerMaterial;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Standard");
            tracerMaterial = new Material(shader)
            {
                color = tracerColor
            };
            return tracerMaterial;
        }

        private static void DestroyCollider(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }

        private static void ApplyMaterial(GameObject target, Color color)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer == null)
                return;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");
            Material material = new Material(shader)
            {
                color = color
            };
            renderer.sharedMaterial = material;
        }

        private static IEnumerator DestroyAfter(GameObject target, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (target != null)
                Destroy(target);
        }

        private static IEnumerator FadeAndDestroy(GameObject target, float duration, float scaleMultiplier)
        {
            Renderer renderer = target != null ? target.GetComponent<Renderer>() : null;
            Material material = renderer != null ? renderer.material : null;
            Vector3 startScale = target != null ? target.transform.localScale : Vector3.one;
            float startTime = Time.time;

            while (target != null && Time.time - startTime < duration)
            {
                float normalized = Mathf.Clamp01((Time.time - startTime) / duration);
                target.transform.localScale = Vector3.Lerp(startScale, startScale * scaleMultiplier, normalized);
                if (material != null)
                {
                    Color color = material.color;
                    color.a = 1f - normalized;
                    material.color = color;
                }

                yield return null;
            }

            if (target != null)
                Destroy(target);
        }

        private static IEnumerator MoveFadeAndDestroy(GameObject target, Vector3 direction, float duration)
        {
            Renderer renderer = target != null ? target.GetComponent<Renderer>() : null;
            Material material = renderer != null ? renderer.material : null;
            float startTime = Time.time;
            while (target != null && Time.time - startTime < duration)
            {
                float normalized = Mathf.Clamp01((Time.time - startTime) / duration);
                target.transform.position += direction * (2.2f * Time.deltaTime * (1f - normalized));
                if (material != null)
                {
                    Color color = material.color;
                    color.a = 1f - normalized;
                    material.color = color;
                }

                yield return null;
            }

            if (target != null)
                Destroy(target);
        }

        private void OnValidate()
        {
            muzzleFlashLifetime = Mathf.Max(0.01f, muzzleFlashLifetime);
            tracerLifetime = Mathf.Max(0.01f, tracerLifetime);
            impactLifetime = Mathf.Max(0.01f, impactLifetime);
            tracerWidth = Mathf.Max(0.001f, tracerWidth);
            tracerMinimumLength = Mathf.Max(1f, tracerMinimumLength);
        }
    }
}
