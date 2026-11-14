using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using TPSShooter.Combat;
using TPSShooter.Weapons;

namespace TPSShooter.Player
{
    /// <summary>
    /// Vida del jugador (100 por defecto).
    /// - Recibe daño de cualquier fuente a través de IDamageable (p. ej. los disparos de EnemyAI).
    /// - Al llegar a 0 desactiva el movimiento y el arma, y reinicia la escena tras un breve retraso.
    ///
    /// Colócalo en el mismo GameObject que el CharacterController del jugador ("Player"),
    /// para que los raycast enemigos que impacten en su collider lo encuentren.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Vida")]
        [SerializeField] private float maxHealth = 100f;
        [Tooltip("Segundos de invulnerabilidad tras recibir un golpe (0 = ninguno).")]
        [SerializeField] private float invulnerabilityAfterHit = 0f;

        [Header("Muerte")]
        [SerializeField] private bool restartSceneOnDeath = true;
        [Tooltip("Segundos entre la muerte y el reinicio de la escena.")]
        [SerializeField] private float restartDelay = 2.5f;
        [SerializeField] private UnityEvent onDeath;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;
        public bool IsDead { get; private set; }

        /// <summary>(vida actual, vida máxima)</summary>
        public event Action<float, float> Changed;
        /// <summary>(daño recibido, punto de impacto)</summary>
        public event Action<float, Vector3> Damaged;
        public event Action Died;

        private float invulnerableUntil;

        private void Awake()
        {
            Current = maxHealth;
        }

        private void Start()
        {
            // Notifica el valor inicial (para el HUD)
            Changed?.Invoke(Current, maxHealth);
        }

        // ------------------------------------------------------------------
        // IDamageable
        // ------------------------------------------------------------------
        public bool TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (IsDead || amount <= 0f || Time.time < invulnerableUntil) return false;

            Current = Mathf.Max(0f, Current - amount);
            invulnerableUntil = Time.time + invulnerabilityAfterHit;

            Damaged?.Invoke(amount, hitPoint);
            Changed?.Invoke(Current, maxHealth);

            if (Current > 0f) return false;

            Die();
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            Changed?.Invoke(Current, maxHealth);
        }

        // ------------------------------------------------------------------
        // Muerte y reinicio
        // ------------------------------------------------------------------
        private void Die()
        {
            IsDead = true;
            Debug.Log("[PlayerHealth] El jugador ha muerto.");

            // Bloquea movimiento y disparo (la cámara sigue funcionando)
            var controller = GetComponent<ThirdPersonController>();
            if (controller != null) controller.enabled = false;
            foreach (var weapon in GetComponentsInChildren<RaycastWeapon>()) weapon.enabled = false;

            Died?.Invoke();
            onDeath?.Invoke();

            if (restartSceneOnDeath) StartCoroutine(RestartAfterDelay());
        }

        private IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSeconds(restartDelay);
            RestartScene();
        }

        /// <summary>Recarga la escena activa.</summary>
        public static void RestartScene()
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
                return;
            }

#if UNITY_EDITOR
            // La escena no está en Build Settings: en el Editor la recargamos por ruta igualmente
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.name);
#endif
        }
    }
}
