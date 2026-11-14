using UnityEngine;

namespace TPSShooter.Player
{
    /// <summary>
    /// Movimiento en tercera persona con CharacterController:
    /// caminar / correr relativo a la cámara, salto con "coyote time" y buffer,
    /// gravedad, y rotación suave del personaje. Mientras apuntas o disparas el
    /// personaje mira hacia donde mira la cámara (comportamiento típico de un TPS).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Cámara usada para orientar el movimiento. Si está vacío se usa Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        [Header("Velocidades (m/s)")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float sprintSpeed = 7f;
        [SerializeField] private float aimWalkSpeed = 2.5f;
        [Tooltip("Qué tan rápido se alcanza la velocidad objetivo.")]
        [SerializeField] private float acceleration = 12f;
        [SerializeField, Range(0f, 1f)] private float airControl = 0.4f;

        [Header("Rotación")]
        [SerializeField] private float rotationSmoothTime = 0.08f;

        [Header("Salto y gravedad")]
        [SerializeField] private float jumpHeight = 1.3f;
        [SerializeField] private float gravity = -20f;
        [Tooltip("Margen para saltar justo después de dejar el suelo.")]
        [SerializeField] private float coyoteTime = 0.12f;
        [Tooltip("Margen para registrar el salto pulsado justo antes de tocar el suelo.")]
        [SerializeField] private float jumpBufferTime = 0.12f;

        private CharacterController controller;
        private PlayerInputHandler input;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private float rotationVelocity;
        private float lastGroundedTime = -10f;
        private float lastJumpPressedTime = -10f;

        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsAiming { get; private set; }
        public float PlanarSpeed => planarVelocity.magnitude;
        public Vector3 Velocity => planarVelocity + Vector3.up * verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputHandler>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // --- Suelo ---
            IsGrounded = controller.isGrounded;
            if (IsGrounded)
            {
                lastGroundedTime = Time.time;
                if (verticalVelocity < 0f) verticalVelocity = -2f; // mantiene pegado al suelo en rampas
            }

            // --- Dirección relativa a la cámara ---
            Vector3 camForward = transform.forward;
            if (cameraTransform != null)
            {
                Vector3 flat = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
                if (flat.sqrMagnitude > 0.001f) camForward = flat.normalized;
            }
            Vector3 camRight = Vector3.Cross(Vector3.up, camForward);

            Vector2 move = input.Move;
            Vector3 desiredDir = camForward * move.y + camRight * move.x;
            if (desiredDir.sqrMagnitude > 1f) desiredDir.Normalize();

            // --- Estados ---
            IsAiming = input.AimHeld || input.FireHeld;
            IsSprinting = input.SprintHeld && move.y > 0.1f && !IsAiming;
            float targetSpeed = IsAiming ? aimWalkSpeed : (IsSprinting ? sprintSpeed : walkSpeed);

            // --- Velocidad horizontal con aceleración suave ---
            float accel = IsGrounded ? acceleration : acceleration * airControl;
            planarVelocity = Vector3.Lerp(planarVelocity, desiredDir * targetSpeed, 1f - Mathf.Exp(-accel * dt));

            // --- Rotación ---
            bool hasTargetYaw = false;
            float targetYaw = 0f;
            if (IsAiming)
            {
                targetYaw = Mathf.Atan2(camForward.x, camForward.z) * Mathf.Rad2Deg;
                hasTargetYaw = true;
            }
            else if (desiredDir.sqrMagnitude > 0.01f)
            {
                targetYaw = Mathf.Atan2(desiredDir.x, desiredDir.z) * Mathf.Rad2Deg;
                hasTargetYaw = true;
            }
            if (hasTargetYaw)
            {
                float smooth = IsAiming ? rotationSmoothTime * 0.5f : rotationSmoothTime;
                float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref rotationVelocity, smooth);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            // --- Salto ---
            if (input.JumpPressed) lastJumpPressedTime = Time.time;
            bool canJump = Time.time - lastGroundedTime <= coyoteTime;
            bool wantsJump = Time.time - lastJumpPressedTime <= jumpBufferTime;
            if (canJump && wantsJump)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                lastGroundedTime = -10f;
                lastJumpPressedTime = -10f;
            }

            // --- Gravedad y movimiento final ---
            verticalVelocity += gravity * dt;
            CollisionFlags flags = controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f; // golpe con el techo
        }
    }
}
