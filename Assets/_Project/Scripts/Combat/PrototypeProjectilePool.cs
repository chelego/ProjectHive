using System.Collections.Generic;
using UnityEngine;

namespace ProjectHive.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeProjectilePool : MonoBehaviour
    {
        [SerializeField, Min(1)] private int initialCapacity = 24;
        [SerializeField] private Material projectileMaterial;

        private readonly Queue<PrototypeProjectile> available = new Queue<PrototypeProjectile>(32);
        private bool initialized;

        public int AvailableCount => available.Count;

        public void Configure(Material material, int capacity)
        {
            projectileMaterial = material;
            initialCapacity = Mathf.Max(1, capacity);
        }

        private void Awake()
        {
            Initialize();
        }

        public PrototypeProjectile Spawn(
            Vector3 position,
            Vector3 direction,
            float speed,
            float damage,
            float lifetime)
        {
            Initialize();
            PrototypeProjectile projectile = available.Count > 0 ? available.Dequeue() : CreateProjectile();
            projectile.Launch(this, position, direction, speed, damage, lifetime);
            return projectile;
        }

        public void Return(PrototypeProjectile projectile)
        {
            if (projectile != null && !available.Contains(projectile))
                available.Enqueue(projectile);
        }

        private void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            for (int index = 0; index < initialCapacity; index++)
            {
                PrototypeProjectile projectile = CreateProjectile();
                projectile.gameObject.SetActive(false);
                available.Enqueue(projectile);
            }
        }

        private PrototypeProjectile CreateProjectile()
        {
            GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = "Pooled_Projectile";
            projectileObject.transform.SetParent(transform, false);
            projectileObject.transform.localScale = Vector3.one * 0.09f;

            Renderer renderer = projectileObject.GetComponent<Renderer>();
            if (projectileMaterial != null)
                renderer.sharedMaterial = projectileMaterial;

            SphereCollider sphereCollider = projectileObject.GetComponent<SphereCollider>();
            sphereCollider.radius = 0.5f;

            Rigidbody rigidbody = projectileObject.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.mass = 0.08f;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            return projectileObject.AddComponent<PrototypeProjectile>();
        }
    }
}
