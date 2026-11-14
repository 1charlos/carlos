using System;
using UnityEngine;
using UnityEngine.Events;

namespace TPSShooter.Combat
{
    /// <summary>Vida genérica. Recibe daño a través de IDamageable.</summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private UnityEvent onDeath;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;
        public bool IsDead { get; private set; }

        /// <summary>(vida actual, vida máxima)</summary>
        public event Action<float, float> Changed;
        /// <summary>(punto de impacto, normal, daño)</summary>
        public event Action<Vector3, Vector3, float> Damaged;
        public event Action<Health> Died;

        private void Awake()
        {
            Current = maxHealth;
        }

        public bool TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (IsDead || amount <= 0f) return false;

            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(hitPoint, hitNormal, amount);
            Changed?.Invoke(Current, maxHealth);

            if (Current > 0f) return false;

            IsDead = true;
            Died?.Invoke(this);
            onDeath?.Invoke();
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            Changed?.Invoke(Current, maxHealth);
        }

        public void ResetHealth()
        {
            IsDead = false;
            Current = maxHealth;
            Changed?.Invoke(Current, maxHealth);
        }
    }
}
