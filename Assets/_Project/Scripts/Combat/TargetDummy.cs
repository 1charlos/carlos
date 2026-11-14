using System.Collections;
using UnityEngine;

namespace TPSShooter.Combat
{
    /// <summary>
    /// Diana de práctica: parpadea al recibir un impacto, desaparece al morir
    /// y reaparece pasados unos segundos.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class TargetDummy : MonoBehaviour
    {
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float flashDuration = 0.08f;
        [SerializeField] private float respawnDelay = 3f;

        private Health health;
        private Renderer[] renderers;
        private Collider[] colliders;
        private Material[] materials;
        private Color[] baseColors;
        private Coroutine flashRoutine;

        private void Awake()
        {
            health = GetComponent<Health>();
            renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();

            materials = new Material[renderers.Length];
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                materials[i] = renderers[i].material; // instancia propia para poder cambiar el color
                baseColors[i] = materials[i].color;
            }
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void OnDamaged(Vector3 point, Vector3 normal, float amount)
        {
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            SetColor(flashColor);
            yield return new WaitForSeconds(flashDuration);
            RestoreColors();
            flashRoutine = null;
        }

        private void OnDied(Health h)
        {
            Debug.Log($"[TargetDummy] '{name}' eliminado.");
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            SetVisible(false);
            yield return new WaitForSeconds(respawnDelay);
            RestoreColors();
            health.ResetHealth();
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in renderers) r.enabled = visible;
            foreach (var c in colliders) c.enabled = visible;
        }

        private void SetColor(Color c)
        {
            foreach (var m in materials) m.color = c;
        }

        private void RestoreColors()
        {
            for (int i = 0; i < materials.Length; i++) materials[i].color = baseColors[i];
        }

        private void OnDestroy()
        {
            if (materials == null) return;
            foreach (var m in materials) if (m != null) Destroy(m);
        }
    }
}
