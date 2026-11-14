using System;
using System.Collections;
using UnityEngine;
using TPSShooter.CameraSystem;
using TPSShooter.Combat;
using TPSShooter.Player;

namespace TPSShooter.Weapons
{
    /// <summary>
    /// Arma hitscan (raycast) para TPS.
    ///
    /// Cómo apunta: se lanza un rayo desde el centro de la pantalla (la mira) para saber
    /// a qué punto del mundo estás apuntando, y después un segundo rayo desde el cañón
    /// hacia ese punto. Así la bala sale del arma pero va a donde marca la mira, y si hay
    /// un obstáculo entre el arma y el objetivo, el obstáculo recibe el impacto.
    /// </summary>
    public class RaycastWeapon : MonoBehaviour
    {
        [Header("Referencias (se autocompletan si están vacías)")]
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private ThirdPersonCamera cameraRig;
        [SerializeField] private Transform muzzle;

        [Header("Daño")]
        [SerializeField] private float damage = 20f;
        [SerializeField] private float range = 150f;
        [SerializeField] private float impactForce = 8f;
        [Tooltip("Capas que pueden recibir disparos. El jugador debe quedar fuera (por defecto está en 'Ignore Raycast').")]
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;

        [Header("Cadencia")]
        [SerializeField] private bool automatic = true;
        [Tooltip("Disparos por segundo.")]
        [SerializeField] private float fireRate = 10f;

        [Header("Munición")]
        [SerializeField] private int magazineSize = 30;
        [SerializeField] private int startingReserveAmmo = 120;
        [SerializeField] private float reloadTime = 1.6f;
        [SerializeField] private bool autoReloadWhenEmpty = true;

        [Header("Precisión (grados de dispersión)")]
        [SerializeField] private float hipSpread = 1.5f;
        [SerializeField] private float aimSpread = 0.25f;

        [Header("Retroceso de cámara")]
        [SerializeField] private float recoilUp = 0.6f;
        [SerializeField] private float recoilSide = 0.25f;

        [Header("Efectos (opcionales)")]
        [SerializeField] private LineRenderer tracer;
        [SerializeField] private float tracerDuration = 0.04f;
        [SerializeField] private Light muzzleFlash;
        [Tooltip("Prefab a instanciar en el punto de impacto. Si está vacío se crea una pequeña esfera.")]
        [SerializeField] private GameObject impactPrefab;
        [SerializeField] private Material impactMaterial;
        [SerializeField] private float impactLifetime = 3f;

        // ---------- Estado ----------
        public int CurrentAmmo { get; private set; }
        public int ReserveAmmo { get; private set; }
        public int MagazineSize => magazineSize;
        public bool IsReloading { get; private set; }
        public float ReloadProgress => IsReloading ? Mathf.Clamp01((Time.time - reloadStartTime) / reloadTime) : 0f;

        // ---------- Eventos (para HUD, sonido, animaciones...) ----------
        /// <summary>(munición en cargador, munición de reserva)</summary>
        public event Action<int, int> AmmoChanged;
        public event Action Fired;
        public event Action DryFired;
        public event Action ReloadStarted;
        public event Action ReloadFinished;
        /// <summary>Cualquier impacto contra el mundo.</summary>
        public event Action<RaycastHit> HitSurface;
        /// <summary>Impacto contra algo con vida. El bool indica si lo eliminó.</summary>
        public event Action<IDamageable, bool> HitDamageable;

        private float nextFireTime;
        private float reloadStartTime;
        private float effectsOffTime;
        private Coroutine reloadRoutine;

        private void Awake()
        {
            if (input == null) input = GetComponentInParent<PlayerInputHandler>();
            if (aimCamera == null) aimCamera = Camera.main;
            if (cameraRig == null && aimCamera != null) cameraRig = aimCamera.GetComponent<ThirdPersonCamera>();
            if (muzzle == null) muzzle = transform;

            CurrentAmmo = magazineSize;
            ReserveAmmo = startingReserveAmmo;
            SetEffects(false);
        }

        private void OnDisable()
        {
            if (reloadRoutine != null) StopCoroutine(reloadRoutine);
            reloadRoutine = null;
            IsReloading = false;
            SetEffects(false);
        }

        private void Update()
        {
            if (Time.time >= effectsOffTime) SetEffects(false);
            if (input == null) return;

            if (input.ReloadPressed) StartReload();

            bool trigger = automatic ? input.FireHeld : input.FirePressed;
            if (trigger) TryFire();
        }

        // ------------------------------------------------------------------
        // API pública
        // ------------------------------------------------------------------
        public bool TryFire()
        {
            if (IsReloading || Time.time < nextFireTime) return false;

            if (CurrentAmmo <= 0)
            {
                nextFireTime = Time.time + 0.25f;
                DryFired?.Invoke();
                if (autoReloadWhenEmpty) StartReload();
                return false;
            }

            nextFireTime = Time.time + 1f / Mathf.Max(0.01f, fireRate);
            CurrentAmmo--;
            Shoot();
            AmmoChanged?.Invoke(CurrentAmmo, ReserveAmmo);

            if (CurrentAmmo == 0 && autoReloadWhenEmpty) StartReload();
            return true;
        }

        public void StartReload()
        {
            if (IsReloading || CurrentAmmo >= magazineSize || ReserveAmmo <= 0) return;
            reloadRoutine = StartCoroutine(ReloadRoutine());
        }

        public void AddReserveAmmo(int amount)
        {
            ReserveAmmo += Mathf.Max(0, amount);
            AmmoChanged?.Invoke(CurrentAmmo, ReserveAmmo);
        }

        // ------------------------------------------------------------------
        // Disparo
        // ------------------------------------------------------------------
        private void Shoot()
        {
            Vector3 origin = muzzle.position;
            Vector3 aimPoint = ComputeAimPoint();
            Vector3 dir = aimPoint - origin;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : muzzle.forward;

            Vector3 end = origin + dir * range;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                ProcessHit(hit, dir);
            }

            ShowEffects(origin, end);
            if (cameraRig != null) cameraRig.AddRecoil(recoilUp, UnityEngine.Random.Range(-recoilSide, recoilSide));
            Fired?.Invoke();
        }

        /// <summary>Punto del mundo bajo la mira (centro de pantalla), con dispersión.</summary>
        private Vector3 ComputeAimPoint()
        {
            if (aimCamera == null) return muzzle.position + muzzle.forward * range;

            Transform camT = aimCamera.transform;
            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            float spread = input != null && input.AimHeld ? aimSpread : hipSpread;
            Vector2 r = UnityEngine.Random.insideUnitCircle * spread;
            Vector3 dir = Quaternion.AngleAxis(r.x, camT.up) * Quaternion.AngleAxis(r.y, camT.right) * ray.direction;

            // Empezamos el rayo a la altura del personaje para no acertar a cosas detrás de él
            float skip = Mathf.Max(0f, Vector3.Dot(muzzle.position - ray.origin, ray.direction));
            Vector3 start = ray.origin + ray.direction * skip;

            return Physics.Raycast(start, dir, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : start + dir * range;
        }

        private void ProcessHit(RaycastHit hit, Vector3 dir)
        {
            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                bool killed = damageable.TakeDamage(damage, hit.point, hit.normal);
                HitDamageable?.Invoke(damageable, killed);
            }

            if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                hit.rigidbody.AddForceAtPosition(dir * impactForce, hit.point, ForceMode.Impulse);

            SpawnImpact(hit);
            HitSurface?.Invoke(hit);
        }

        private void SpawnImpact(RaycastHit hit)
        {
            Vector3 pos = hit.point + hit.normal * 0.01f;
            Quaternion rot = Quaternion.LookRotation(hit.normal);
            GameObject fx;

            if (impactPrefab != null)
            {
                fx = Instantiate(impactPrefab, pos, rot);
            }
            else
            {
                fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fx.name = "Impact";
                Destroy(fx.GetComponent<Collider>());
                fx.transform.SetPositionAndRotation(pos, rot);
                fx.transform.localScale = Vector3.one * 0.08f;
                if (impactMaterial != null) fx.GetComponent<Renderer>().sharedMaterial = impactMaterial;
            }

            // Que se mueva con lo que golpeó (cajas físicas, etc.)
            fx.transform.SetParent(hit.collider.transform, true);
            Destroy(fx, impactLifetime);
        }

        // ------------------------------------------------------------------
        // Recarga
        // ------------------------------------------------------------------
        private IEnumerator ReloadRoutine()
        {
            IsReloading = true;
            reloadStartTime = Time.time;
            ReloadStarted?.Invoke();

            yield return new WaitForSeconds(reloadTime);

            int needed = magazineSize - CurrentAmmo;
            int taken = Mathf.Min(needed, ReserveAmmo);
            CurrentAmmo += taken;
            ReserveAmmo -= taken;

            IsReloading = false;
            reloadRoutine = null;
            ReloadFinished?.Invoke();
            AmmoChanged?.Invoke(CurrentAmmo, ReserveAmmo);
        }

        // ------------------------------------------------------------------
        // Efectos
        // ------------------------------------------------------------------
        private void ShowEffects(Vector3 from, Vector3 to)
        {
            if (tracer != null)
            {
                tracer.useWorldSpace = true;
                tracer.positionCount = 2;
                tracer.SetPosition(0, from);
                tracer.SetPosition(1, to);
            }
            SetEffects(true);
            effectsOffTime = Time.time + tracerDuration;
        }

        private void SetEffects(bool on)
        {
            if (tracer != null) tracer.enabled = on;
            if (muzzleFlash != null) muzzleFlash.enabled = on;
        }
    }
}
