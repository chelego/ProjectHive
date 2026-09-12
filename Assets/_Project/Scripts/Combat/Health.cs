using System;
using ProjectHive.Core.Contracts;
using UnityEngine;

namespace ProjectHive.Combat
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private bool destroyOnDeath;
        [SerializeField] private float destroyDelay = 1.5f;

        private float currentHealth;
        private bool isDead;

        public event Action<Health, DamageData> Damaged;
        public event Action<Health> Died;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float Normalized => maxHealth <= 0f ? 0f : currentHealth / maxHealth;
        public bool IsDead => isDead;
        // Runtime-only diagnostic protection. Detection and attack attempts remain active.
        public bool IsInvulnerable { get; set; }

        private void Awake()
        {
            currentHealth = Mathf.Max(1f, maxHealth);
        }

        public void ApplyDamage(in DamageData damage)
        {
            if (isDead || IsInvulnerable || damage.Amount <= 0f)
                return;

            currentHealth = Mathf.Max(0f, currentHealth - damage.Amount);
            Damaged?.Invoke(this, damage);

            if (currentHealth <= 0f)
                Die();
        }

        public void Heal(float amount)
        {
            if (isDead || amount <= 0f)
                return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        }

        public void ResetHealth()
        {
            isDead = false;
            currentHealth = Mathf.Max(1f, maxHealth);
        }

        private void Die()
        {
            if (isDead)
                return;

            isDead = true;
            Died?.Invoke(this);

            if (destroyOnDeath)
                Destroy(gameObject, destroyDelay);
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            destroyDelay = Mathf.Max(0f, destroyDelay);
        }
    }
}
