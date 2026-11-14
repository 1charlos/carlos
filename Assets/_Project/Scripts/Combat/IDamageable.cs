using UnityEngine;

namespace TPSShooter.Combat
{
    /// <summary>Cualquier cosa que pueda recibir daño (jugadores, enemigos, dianas, barriles...).</summary>
    public interface IDamageable
    {
        /// <returns>true si este impacto lo ha eliminado.</returns>
        bool TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal);
    }
}
