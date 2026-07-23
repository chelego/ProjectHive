using ProjectHive.Core.Contracts;
using UnityEngine;

namespace ProjectHive.Prototype
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class PrototypeProjectile : MonoBehaviour
    {
        private PrototypeProjectilePool owner;
        private Rigidbody body;
        private float damage;
        private float remainingLifetime;
        private bool activeProjectile;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public void Launch(
            PrototypeProjectilePool projectileOwner,
            Vector3 position,
            Vector3 direction,
            float speed,
            float projectileDamage,
            float lifetime)
        {
            owner = projectileOwner;
            damage = projectileDamage;
            remainingLifetime = lifetime;
            activeProjectile = true;

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            gameObject.SetActive(true);
            body.linearVelocity = direction.normalized * speed;
            body.angularVelocity = Vector3.zero;
        }

        private void Update()
        {
            if (!activeProjectile)
                return;

            remainingLifetime -= Time.deltaTime;
            if (remainingLifetime <= 0f)
                Recycle();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!activeProjectile)
                return;

            IDamageable target = collision.collider.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                ContactPoint contact = collision.GetContact(0);
                DamageData damageData = new DamageData(
                    damage,
                    DamageKind.Projectile,
                    contact.point,
                    body.linearVelocity.normalized,
                    gameObject);
                target.ApplyDamage(in damageData);
            }

            Recycle();
        }

        public void Recycle()
        {
            if (!activeProjectile)
                return;

            activeProjectile = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            gameObject.SetActive(false);
            owner?.Return(this);
        }
    }
}
