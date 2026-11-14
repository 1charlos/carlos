using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TPSShooter.UI
{
    public enum MobileAction { Fire, Aim, Jump, Reload, Sprint }

    /// <summary>
    /// Botón táctil que se puede mantener pulsado. PlayerInputHandler lo encuentra
    /// automáticamente según su "Action".
    /// </summary>
    public class MobileButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        [SerializeField] private MobileAction action;
        [Tooltip("Si está activo, cada toque alterna encendido/apagado (útil para apuntar).")]
        [SerializeField] private bool toggleMode = false;
        [Tooltip("Si se asigna, arrastrar el dedo sobre este botón también mueve la cámara (disparar y apuntar a la vez, estilo Free Fire).")]
        [SerializeField] private TouchLookArea dragToLook;
        [SerializeField] private Color pressedTint = new Color(1f, 1f, 1f, 0.9f);

        public MobileAction Action => action;
        public bool IsHeld { get; private set; }

        private bool pressLatched;
        private Graphic graphic;
        private Color normalColor;

        private void Awake()
        {
            graphic = GetComponent<Graphic>();
            if (graphic != null) normalColor = graphic.color;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsHeld = toggleMode ? !IsHeld : true;
            pressLatched = true;
            UpdateVisual();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!toggleMode) IsHeld = false;
            UpdateVisual();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragToLook != null) dragToLook.AddDelta(eventData.delta);
        }

        /// <summary>true una sola vez por cada pulsación.</summary>
        public bool ConsumePress()
        {
            bool p = pressLatched;
            pressLatched = false;
            return p;
        }

        private void OnDisable()
        {
            IsHeld = false;
            pressLatched = false;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (graphic != null) graphic.color = IsHeld ? pressedTint : normalColor;
        }
    }
}
