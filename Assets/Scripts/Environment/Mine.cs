using UnityEngine;

namespace SonarCrew.Environment
{
    /// <summary>
    /// Mina del mapa (hito 6a): collider sólido en capa Hazard, detectable por
    /// el sónar. Al contacto con el casco detona una única vez: aplica el
    /// impulso de explosión de MineConfig en el punto de contacto (para que la
    /// detonación también imprima rotación, como el resto de colisiones, §5) y
    /// se desactiva. VFX y audio llegan con el hito 6b.
    /// </summary>
    public class Mine : MonoBehaviour
    {
        [Header("Configuración (ScriptableObject)")]
        [SerializeField] private MineConfig config;

        [Header("Objetivo")]
        [Tooltip("Solo detona contra colliders del casco. Excluir Interior y World.")]
        [SerializeField] private LayerMask hullMask = 0;

        private bool _detonated;

        private void OnCollisionEnter(Collision collision)
        {
            if (_detonated || config == null) return;
            if (collision.rigidbody == null) return;
            if (((1 << collision.gameObject.layer) & hullMask.value) == 0) return;

            _detonated = true;

            // La explosión empuja desde la mina hacia el casco; con AddForceAtPosition
            // en el punto de contacto también gira el casco, no solo lo desplaza.
            Vector3 contactPoint = collision.GetContact(0).point;
            Vector3 direction = (collision.rigidbody.position - transform.position).normalized;
            if (direction.sqrMagnitude < 0.5f) direction = Vector3.up;
            collision.rigidbody.AddForceAtPosition(direction * config.impulse, contactPoint, ForceMode.Impulse);

            // Un solo uso: sin roguelike ni respawn en la versión inicial.
            gameObject.SetActive(false);
        }
    }
}
