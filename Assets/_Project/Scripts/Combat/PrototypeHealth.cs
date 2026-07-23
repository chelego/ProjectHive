using System;
using ProjectHive.Core.Contracts;
using UnityEngine;

namespace ProjectHive.Prototype
{
    [DisallowMultipleComponent]
    public sealed class PrototypeHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maximumHealth = 100f;

        private float currentHealth;
        private bool isDead;

        public event Action<Vector3, Vector3> Damaged;
        public event Action<Vector3, Vector3> Died;

        public float CurrentHealth => currentHealth;
        public float MaximumHealth => maximumHealth;
        public bool IsDead => isDead;

        private void Awake()
        {
            currentHealth = maximumHealth;
        }

        public void Configure(float health)
        {
            maximumHealth = Mathf.Max(1f, health);
            currentHealth = maximumHealth;
            isDead = false;
        }

        public void ApplyDamage(float amount, Vector3 hitPoint, Vector3 direction)
        {
            if (isDead || amount <= 0f)
                return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            Damaged?.Invoke(hitPoint, direction);

            if (currentHealth > 0f)
                return;

            isDead = true;
            Died?.Invoke(hitPoint, direction);
        }

        public void ApplyDamage(in DamageData damage)
        {
            ApplyDamage(damage.Amount, damage.HitPoint, damage.Direction);
        }
    }
}
