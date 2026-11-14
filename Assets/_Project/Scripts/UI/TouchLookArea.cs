using UnityEngine;
using UnityEngine.EventSystems;

namespace TPSShooter.UI
{
    /// <summary>
    /// Zona de pantalla (normalmente la mitad derecha) donde arrastrar el dedo rota la cámara.
    /// Va en una Image transparente con "Raycast Target" activado.
    /// </summary>
    public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const int NoPointer = int.MinValue;
        private int activePointer = NoPointer;
        private Vector2 accumulated;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointer == NoPointer) activePointer = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointer) accumulated += eventData.delta;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointer) activePointer = NoPointer;
        }

        /// <summary>Permite que otros elementos (p. ej. el botón de disparo) también muevan la cámara.</summary>
        public void AddDelta(Vector2 delta) => accumulated += delta;

        /// <summary>Devuelve el arrastre acumulado (en píxeles) desde la última llamada.</summary>
        public Vector2 ConsumeDelta()
        {
            Vector2 d = accumulated;
            accumulated = Vector2.zero;
            return d;
        }

        private void OnDisable()
        {
            activePointer = NoPointer;
            accumulated = Vector2.zero;
        }
    }
}
