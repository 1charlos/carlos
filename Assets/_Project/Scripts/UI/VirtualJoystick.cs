using UnityEngine;
using UnityEngine.EventSystems;

namespace TPSShooter.UI
{
    /// <summary>
    /// Joystick virtual táctil. Va en una Image (el fondo) con un hijo "handle".
    /// Devuelve Value en el rango -1..1.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [Tooltip("Cuánto puede salir el handle respecto al radio del fondo.")]
        [SerializeField, Range(0.1f, 1.5f)] private float handleRange = 1f;
        [SerializeField, Range(0f, 0.5f)] private float deadZone = 0.1f;

        public Vector2 Value { get; private set; }
        public bool IsPressed { get; private set; }

        private void Awake()
        {
            if (background == null) background = (RectTransform)transform;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsPressed = true;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            // Coordenadas relativas al centro del fondo
            Vector2 center = background.rect.center;
            Vector2 radius = background.rect.size * 0.5f;
            Vector2 v = new Vector2((local.x - center.x) / radius.x, (local.y - center.y) / radius.y);
            v = Vector2.ClampMagnitude(v, 1f);

            Value = v.magnitude < deadZone ? Vector2.zero : v;
            if (handle != null) handle.anchoredPosition = v * radius * handleRange;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ResetStick();
        }

        private void OnDisable()
        {
            ResetStick();
        }

        private void ResetStick()
        {
            IsPressed = false;
            Value = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }
    }
}
