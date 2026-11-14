using UnityEngine;
using TPSShooter.Player;

namespace TPSShooter.CameraSystem
{
    /// <summary>
    /// Cámara orbital "sobre el hombro". Va en la Main Camera.
    /// - Rota alrededor del personaje con ratón / arrastre táctil.
    /// - Sigue al personaje con suavizado.
    /// - Se acerca y reduce el FOV al apuntar.
    /// - Evita atravesar paredes con un SphereCast.
    /// - Admite retroceso (recoil) del arma.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Objetivo")]
        [SerializeField] private Transform target;
        [Tooltip("Si está vacío se toma del objetivo.")]
        [SerializeField] private PlayerInputHandler input;

        [Header("Encuadre")]
        [Tooltip("Punto (relativo a los pies del personaje) alrededor del cual orbita la cámara.")]
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.55f, 0f);
        [Tooltip("Desplazamiento lateral (hombro derecho).")]
        [SerializeField] private Vector3 shoulderOffset = new Vector3(0.55f, 0.1f, 0f);
        [SerializeField] private float distance = 3.2f;
        [SerializeField] private float aimDistance = 1.6f;

        [Header("Rotación")]
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 65f;
        [SerializeField] private float startPitch = 10f;

        [Header("Suavizado")]
        [SerializeField] private float followSmoothTime = 0.05f;
        [SerializeField] private float zoomSpeed = 10f;

        [Header("Campo de visión")]
        [SerializeField] private float normalFov = 60f;
        [SerializeField] private float aimFov = 45f;
        [SerializeField] private float fovSpeed = 10f;

        [Header("Colisión")]
        [Tooltip("Capas contra las que choca la cámara. El jugador debe estar en una capa excluida (por defecto 'Ignore Raycast').")]
        [SerializeField] private LayerMask collisionMask = Physics.DefaultRaycastLayers;
        [SerializeField] private float collisionRadius = 0.2f;
        [SerializeField] private float minDistance = 0.4f;

        [Header("Retroceso")]
        [SerializeField] private float recoilRecovery = 8f;

        private Camera cam;
        private float yaw, pitch;
        private float recoilPitch;
        private Vector3 pivot, pivotVelocity;
        private float zoomDistance, currentDistance;

        public float Yaw => yaw;
        public float Pitch => pitch;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            zoomDistance = currentDistance = distance;
        }

        private void Start()
        {
            if (target != null) SetTarget(target);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target == null) return;
            if (input == null) input = target.GetComponentInParent<PlayerInputHandler>();
            yaw = target.eulerAngles.y;
            pitch = startPitch;
            pivot = target.position + pivotOffset;
            pivotVelocity = Vector3.zero;
        }

        /// <summary>Patada de cámara: "up" en grados hacia arriba, "side" en grados horizontales.</summary>
        public void AddRecoil(float up, float side)
        {
            recoilPitch += up;
            yaw += side;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            // --- Rotación ---
            if (input != null)
            {
                Vector2 look = input.Look;
                yaw += look.x;
                pitch -= look.y;
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            recoilPitch = Mathf.Lerp(recoilPitch, 0f, 1f - Mathf.Exp(-recoilRecovery * dt));
            Quaternion rotation = Quaternion.Euler(Mathf.Clamp(pitch - recoilPitch, minPitch, maxPitch), yaw, 0f);

            // --- Seguimiento suave ---
            pivot = Vector3.SmoothDamp(pivot, target.position + pivotOffset, ref pivotVelocity, followSmoothTime);

            // --- Zoom al apuntar ---
            bool aiming = input != null && input.AimHeld;
            zoomDistance = Mathf.Lerp(zoomDistance, aiming ? aimDistance : distance, 1f - Mathf.Exp(-zoomSpeed * dt));

            // --- Colisión: pivote -> hombro ---
            Vector3 shoulderPos = pivot + rotation * shoulderOffset;
            Vector3 toShoulder = shoulderPos - pivot;
            if (toShoulder.sqrMagnitude > 0.0001f &&
                Physics.SphereCast(pivot, collisionRadius, toShoulder.normalized, out RaycastHit hit,
                                   toShoulder.magnitude, collisionMask, QueryTriggerInteraction.Ignore))
            {
                shoulderPos = pivot + toShoulder.normalized * hit.distance;
            }

            // --- Colisión: hombro -> cámara ---
            Vector3 back = rotation * Vector3.back;
            float allowed = zoomDistance;
            if (Physics.SphereCast(shoulderPos, collisionRadius, back, out hit, zoomDistance,
                                   collisionMask, QueryTriggerInteraction.Ignore))
            {
                allowed = Mathf.Max(minDistance, hit.distance);
            }
            // Acercarse es instantáneo (no atravesar paredes), alejarse es suave
            currentDistance = allowed < currentDistance
                ? allowed
                : Mathf.Lerp(currentDistance, allowed, 1f - Mathf.Exp(-zoomSpeed * dt));

            transform.SetPositionAndRotation(shoulderPos + back * currentDistance, rotation);

            // --- FOV ---
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, aiming ? aimFov : normalFov, 1f - Mathf.Exp(-fovSpeed * dt));
        }
    }
}
