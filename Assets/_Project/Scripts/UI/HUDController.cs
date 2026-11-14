using UnityEngine;
using UnityEngine.UI;
using TPSShooter.Combat;
using TPSShooter.Weapons;

namespace TPSShooter.UI
{
    /// <summary>HUD básico: munición, estado de recarga y marcador de impacto.</summary>
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

        private Graphic[] hitMarkerGraphics;
        private Color ammoNormalColor = Color.white;
        private float hitTimer;

        private void Start()
        {
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
            if (weapon == null) return;
            weapon.AmmoChanged -= OnAmmoChanged;
            weapon.HitDamageable -= OnHitDamageable;
        }

        private void Update()
        {
            if (weapon != null && reloadText != null)
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
