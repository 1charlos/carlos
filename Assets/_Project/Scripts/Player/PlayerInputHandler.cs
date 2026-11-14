using UnityEngine;
using TPSShooter.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace TPSShooter.Player
{
    /// <summary>
    /// Punto único de entrada del jugador. Junta teclado/ratón (PC) y controles táctiles
    /// (joystick virtual + botones + zona de arrastre para la cámara) en un mismo "estado de input"
    /// que leen el controlador, la cámara y el arma.
    ///
    /// Funciona con el Input Manager clásico y también con el nuevo Input System
    /// (si en Player Settings sólo está activo el nuevo).
    /// </summary>
    [DefaultExecutionOrder(-100)] // se ejecuta antes que el resto para que todos lean el input del frame actual
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Controles móviles")]
        [Tooltip("Muestra y usa los controles táctiles aunque no estés en un móvil (útil para probar en el Editor).")]
        [SerializeField] private bool forceMobileControls = false;
        [Tooltip("Si el joystick se empuja más allá de este valor se corre automáticamente (estilo Free Fire). 0 = desactivado.")]
        [SerializeField, Range(0f, 1f)] private float joystickAutoSprint = 0.95f;

        [Header("Sensibilidad")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float touchSensitivity = 0.2f;
        [SerializeField] private bool invertY = false;

        [Header("Cursor (PC)")]
        [SerializeField] private bool lockCursor = true;

        // ---------- Estado público (se recalcula cada frame) ----------
        public Vector2 Move { get; private set; }          // x = lateral, y = adelante (magnitud 0..1)
        public Vector2 Look { get; private set; }          // grados a rotar este frame (x = yaw, y = pitch)
        public bool SprintHeld { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool FireHeld { get; private set; }
        public bool FirePressed { get; private set; }
        public bool AimHeld { get; private set; }
        public bool ReloadPressed { get; private set; }

        public bool UsingMobileControls => forceMobileControls || Application.isMobilePlatform;

        // Referencias a la UI táctil (se buscan automáticamente en la escena)
        private MobileControlsPanel mobilePanel;
        private VirtualJoystick moveJoystick;
        private TouchLookArea lookArea;
        private MobileButton fireButton, aimButton, jumpButton, reloadButton, sprintButton;

        private void Start()
        {
            FindMobileControls();
            RefreshMobileVisibility();
            if (!UsingMobileControls && lockCursor) SetCursorLocked(true);
        }

        private void Update()
        {
            Move = Vector2.zero;
            Look = Vector2.zero;
            SprintHeld = JumpPressed = FireHeld = FirePressed = AimHeld = ReloadPressed = false;

            RefreshMobileVisibility();
            ReadDesktop();
            if (UsingMobileControls) ReadMobile();

            Move = Vector2.ClampMagnitude(Move, 1f);
            if (invertY) Look = new Vector2(Look.x, -Look.y);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && !UsingMobileControls && lockCursor && Cursor.lockState == CursorLockMode.Locked)
                SetCursorLocked(true);
        }

        // ------------------------------------------------------------------
        // PC: teclado y ratón
        // ------------------------------------------------------------------
        private void ReadDesktop()
        {
            Vector2 move = Vector2.zero, mouseDelta = Vector2.zero;
            bool sprint = false, jump = false, reload = false, escape = false;
            bool fireHeld = false, firePressed = false, aim = false;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = Keyboard.current;
            if (kb != null)
            {
                move.x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                move.y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
                sprint = kb.leftShiftKey.isPressed;
                jump = kb.spaceKey.wasPressedThisFrame;
                reload = kb.rKey.wasPressedThisFrame;
                escape = kb.escapeKey.wasPressedThisFrame;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                mouseDelta = mouse.delta.ReadValue() * 0.1f; // escala similar a "Mouse X/Y" del sistema clásico
                fireHeld = mouse.leftButton.isPressed;
                firePressed = mouse.leftButton.wasPressedThisFrame;
                aim = mouse.rightButton.isPressed;
            }
#else
            move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            sprint = Input.GetKey(KeyCode.LeftShift);
            jump = Input.GetKeyDown(KeyCode.Space);
            reload = Input.GetKeyDown(KeyCode.R);
            escape = Input.GetKeyDown(KeyCode.Escape);
            mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            fireHeld = Input.GetMouseButton(0);
            firePressed = Input.GetMouseButtonDown(0);
            aim = Input.GetMouseButton(1);
#endif
            // El teclado siempre funciona (también con controles móviles forzados en el Editor)
            Move += move;
            SprintHeld |= sprint;
            JumpPressed |= jump;
            ReloadPressed |= reload;

            // Con controles móviles el ratón se usa para pulsar la UI, no para jugar
            if (UsingMobileControls) return;

            bool mouseActive = true;
            if (lockCursor)
            {
                if (escape) SetCursorLocked(false);
                else if (firePressed && Cursor.lockState != CursorLockMode.Locked)
                {
                    SetCursorLocked(true); // el clic que vuelve a capturar el ratón no dispara
                    return;
                }
                mouseActive = Cursor.lockState == CursorLockMode.Locked;
            }

            if (!mouseActive) return;
            Look += mouseDelta * mouseSensitivity;
            FireHeld |= fireHeld;
            FirePressed |= firePressed;
            AimHeld |= aim;
        }

        // ------------------------------------------------------------------
        // Móvil: joystick virtual, zona táctil y botones
        // ------------------------------------------------------------------
        private void ReadMobile()
        {
            if (moveJoystick != null)
            {
                Vector2 v = moveJoystick.Value;
                if (v.sqrMagnitude > Move.sqrMagnitude) Move = v;
                if (joystickAutoSprint > 0f && v.magnitude >= joystickAutoSprint && v.y > 0.5f) SprintHeld = true;
            }

            if (lookArea != null)
            {
                // Normalizamos por la resolución para que la sensibilidad sea igual en cualquier pantalla
                float resolutionScale = 1080f / Mathf.Max(1, Screen.height);
                Look += lookArea.ConsumeDelta() * touchSensitivity * resolutionScale;
            }

            if (fireButton != null) { FireHeld |= fireButton.IsHeld; FirePressed |= fireButton.ConsumePress(); }
            if (aimButton != null) { AimHeld |= aimButton.IsHeld; aimButton.ConsumePress(); }
            if (jumpButton != null) JumpPressed |= jumpButton.ConsumePress();
            if (reloadButton != null) ReloadPressed |= reloadButton.ConsumePress();
            if (sprintButton != null) { SprintHeld |= sprintButton.IsHeld; sprintButton.ConsumePress(); }
        }

        private void FindMobileControls()
        {
            mobilePanel = FindFirstObjectByType<MobileControlsPanel>(FindObjectsInactive.Include);
            moveJoystick = FindFirstObjectByType<VirtualJoystick>(FindObjectsInactive.Include);
            lookArea = FindFirstObjectByType<TouchLookArea>(FindObjectsInactive.Include);

            foreach (var button in FindObjectsByType<MobileButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                switch (button.Action)
                {
                    case MobileAction.Fire: fireButton = button; break;
                    case MobileAction.Aim: aimButton = button; break;
                    case MobileAction.Jump: jumpButton = button; break;
                    case MobileAction.Reload: reloadButton = button; break;
                    case MobileAction.Sprint: sprintButton = button; break;
                }
            }
        }

        private void RefreshMobileVisibility()
        {
            if (mobilePanel != null && mobilePanel.gameObject.activeSelf != UsingMobileControls)
                mobilePanel.gameObject.SetActive(UsingMobileControls);
        }

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
