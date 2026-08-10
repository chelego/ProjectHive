using UnityEngine;

namespace ProjectHive.Combat
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class HumanoidTargetVisual : MonoBehaviour
    {
        [SerializeField] private bool rebuildOnAwake = true;
        [SerializeField] private bool rebuildInEditMode = true;
        [SerializeField] private Material bodyMaterial;
        [SerializeField] private Material headMaterial;
        [SerializeField] private Color bodyColor = new Color(0.18f, 0.32f, 0.72f, 1f);
        [SerializeField] private Color headColor = new Color(0.86f, 0.74f, 0.56f, 1f);
        [SerializeField, Min(1f)] private float headDamageMultiplier = 2f;

        private const string GeneratedRootName = "Generated Humanoid Target";

        private void Awake()
        {
            if (Application.isPlaying && rebuildOnAwake)
                Rebuild();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying && rebuildInEditMode)
                Rebuild();
        }

        [ContextMenu("Rebuild Target Visual")]
        public void Rebuild()
        {
            ClearGenerated();
            DisableLegacyRenderersAndColliders();

            GameObject root = new GameObject(GeneratedRootName);
            root.transform.SetParent(transform, false);

            Material body = CreateRuntimeMaterial(bodyMaterial, bodyColor);
            Material head = CreateRuntimeMaterial(headMaterial, headColor);

            GameObject bodyObject = AddPart(
                root.transform,
                "Body Hit Zone",
                PrimitiveType.Capsule,
                new Vector3(0f, 0f, 0f),
                new Vector3(0.62f, 0.78f, 0.62f),
                body);
            ConfigureHitZone(bodyObject, Core.Contracts.DamageHitZone.Body, 1f);

            GameObject headObject = AddPart(
                root.transform,
                "Head Hit Zone",
                PrimitiveType.Sphere,
                new Vector3(0f, 0.92f, 0f),
                new Vector3(0.46f, 0.46f, 0.46f),
                head);
            ConfigureHitZone(
                headObject,
                Core.Contracts.DamageHitZone.Head,
                headDamageMultiplier);
        }

        private void ClearGenerated()
        {
            Transform existing = transform.Find(GeneratedRootName);
            if (existing != null)
                DestroyGeneratedObject(existing.gameObject);
        }

        private static void DestroyGeneratedObject(GameObject generated)
        {
            if (Application.isPlaying)
                Destroy(generated);
            else
                DestroyImmediate(generated);
        }

        private void DisableLegacyRenderersAndColliders()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (renderer.GetComponentInParent<DamageHitZoneMarker>() != null)
                    continue;

                renderer.enabled = false;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider collider in colliders)
            {
                if (collider.GetComponent<DamageHitZoneMarker>() != null)
                    continue;

                collider.enabled = false;
            }
        }

        private GameObject AddPart(
            Transform parent,
            string partName,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;

            return part;
        }

        private static void ConfigureHitZone(
            GameObject part,
            Core.Contracts.DamageHitZone hitZone,
            float multiplier)
        {
            DamageHitZoneMarker marker = part.AddComponent<DamageHitZoneMarker>();
            marker.Configure(hitZone, multiplier);
        }

        private static Material CreateRuntimeMaterial(Material source, Color color)
        {
            Shader shader =
                source != null
                    ? source.shader
                    : Shader.Find("Universal Render Pipeline/Lit") ??
                      Shader.Find("Standard");
            Material material = source != null ? new Material(source) : new Material(shader);
            material.color = color;
            return material;
        }

        private void OnValidate()
        {
            headDamageMultiplier = Mathf.Max(1f, headDamageMultiplier);
        }

    }
}
