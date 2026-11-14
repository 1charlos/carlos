using UnityEngine;
using UnityEngine.UI;
using TPSShooter.Combat;
using TPSShooter.Player;
using TPSShooter.Weapons;

namespace TPSShooter.UI
{
    /// <summary>HUD básico: munición, recarga, marcador de impacto y vida del jugador.</summary>
    public class HUDController : MonoBehaviour
    {
        [SerializeField] private RaycastWeapon weapon;
        [SerializeField] private Text ammoText;
        [SerializeField] private Text reloadText;
        [Tooltip("Grupo con la 'X' del marcador de impacto.")]
        [SerializeField] private CanvasGroup hitMarker;
        [SerializeField] private float hitMarkerDuration = 0.15f;
        [SerializeField] private Color hitColor = Color.white;
        [SerializeField] private Color killColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField] private Color lowAmmoColor = new Color(1f, 0.4f, 0.3f);

        [Header("Vida del jugador (opcional)")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Text healthText;
        [Tooltip("Barra de vida: se escala en X según la vida (pivote a la izquierda).")]
        [SerializeField] private RectTransform healthBarFill;
        [Tooltip("Imagen a pantalla completa que parpadea en rojo al recibir daño.")]
        [SerializeField] private Image damageFlash;
        [SerializeField] private float damageFlashAlpha = 0.35f;
        [SerializeField] private float damageFlashFade = 3f;

        private Graphic[] hitMarkerGraphics;
        private Color ammoNormalColor = Color.white;
        private float hitTimer;

        private void Start()
        {
            SetupPlayerHealth();
            if (weapon == null) weapon = FindFirstObjectByType<RaycastWeapon>();
            if (ammoText != null) ammoNormalColor = ammoText.color;
            if (hitMarker != null)
            {
                hitMarkerGraphics = hitMarker.GetComponentsInChildren<Graphic>();
                hitMarker.alpha = 0f;
            }

            if (weapon == null)
            {
                Debug.LogWarning("[HUD] No se encontró ningún RaycastWeapon en la escena.");
                return;
            }
            weapon.AmmoChanged += OnAmmoChanged;
            weapon.HitDamageable += OnHitDamageable;
            OnAmmoChanged(weapon.CurrentAmmo, weapon.ReserveAmmo);
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.Changed -= OnHealthChanged;
                playerHealth.Damaged -= OnPlayerDamaged;
            }
            if (weapon == null) return;
            weapon.AmmoChanged -= OnAmmoChanged;
            weapon.HitDamageable -= OnHitDamageable;
        }

        private void Update()
        {
            if (damageFlash != null && damageFlash.color.a > 0f)
            {
                Color c = damageFlash.color;
                c.a = Mathf.MoveTowards(c.a, 0f, damageFlashFade * damageFlashAlpha * Time.deltaTime);
                damageFlash.color = c;
            }

            if (playerHealth != null && playerHealth.IsDead && reloadText != null)
            {
                reloadText.text = "HAS MUERTO - Reiniciando...";
            }
            else if (weapon != null && reloadText != null)
            {
                if (weapon.IsReloading)
                    reloadText.text = $"RECARGANDO... {Mathf.RoundToInt(weapon.ReloadProgress * 100f)}%";
                else if (weapon.CurrentAmmo == 0 && weapon.ReserveAmmo == 0)
                    reloadText.text = "SIN MUNICIÓN";
                else
                    reloadText.text = string.Empty;
            }

            if (hitMarker != null && hitTimer > 0f)
            {
                hitTimer -= Time.deltaTime;
                hitMarker.alpha = Mathf.Clamp01(hitTimer / hitMarkerDuration);
            }
        }

        private void OnAmmoChanged(int current, int reserve)
        {
            if (ammoText == null) return;
            ammoText.text = $"{current} / {reserve}";
            ammoText.color = current <= Mathf.CeilToInt(weapon.MagazineSize * 0.25f) ? lowAmmoColor : ammoNormalColor;
        }

        private void SetupPlayerHealth()
        {
            if (playerHealth == null) playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (damageFlash != null)
            {
                Color c = damageFlash.color;
                c.a = 0f;
                damageFlash.color = c;
            }
            if (playerHealth == null) return;
            playerHealth.Changed += OnHealthChanged;
            playerHealth.Damaged += OnPlayerDamaged;
            OnHealthChanged(playerHealth.Current, playerHealth.Max);
        }

        private void OnHealthChanged(float current, float max)
        {
            float n = max > 0f ? current / max : 0f;
            if (healthText != null) healthText.text = Mathf.CeilToInt(current).ToString();
            if (healthBarFill != null) healthBarFill.localScale = new Vector3(n, 1f, 1f);
        }

        private void OnPlayerDamaged(float amount, Vector3 point)
        {
            if (damageFlash == null) return;
            Color c = damageFlash.color;
            c.a = damageFlashAlpha;
            damageFlash.color = c;
        }

        private void OnHitDamageable(IDamageable target, bool killed)
        {
            if (hitMarker == null) return;
            hitTimer = killed ? hitMarkerDuration * 2f : hitMarkerDuration;
            hitMarker.alpha = 1f;
            if (hitMarkerGraphics != null)
                foreach (var g in hitMarkerGraphics) g.color = killed ? killColor : hitColor;
        }
    }
}
