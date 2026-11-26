using UnityEngine;
using SonarCrew.Core;

namespace SonarCrew.Submarine
{
    /// <summary>
    /// Cuerpo físico del submarino (AGENTS.md §3.1): Rigidbody sin gravedad ni arrastre
    /// propio (el arrastre lo modela SubmarineMotor) y un collider hijo por sala con su
    /// HullZone. Es lo único que se mueve: el interior es estático y el WorldRoot lo
    /// desplaza SubmarineFrame con la pose inversa.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SubmarineProxy : MonoBehaviour
    {
        [Tooltip("Volúmenes de sala (triggers con HullZoneMarker) para resolver contacto → sala.")]
        [SerializeField] private HullZoneMarker[] zoneMarkers;

        private Rigidbody _body;

        public Rigidbody Body => _body;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            // El proxy no interpola: su pose se copia en FixedUpdate y el rebase
            // es un salto sin interpolación (AGENTS.md §3.1).
            _body.useGravity = false;
            _body.linearDamping = 0f;
            _body.angularDamping = 0f;
            _body.interpolation = RigidbodyInterpolation.None;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (zoneMarkers == null || zoneMarkers.Length == 0)
                zoneMarkers = GetComponentsInChildren<HullZoneMarker>(true);

            ServiceLocator.Register(this);
        }

        /// <summary>
        /// Resuelve la sala cuyo volumen de sala contiene el punto de contacto
        /// (coordenadas del mundo físico). Falso si el punto cae fuera del casco.
        /// </summary>
        public bool TryResolveZone(Vector3 worldPoint, out HullZone zone)
        {
            if (zoneMarkers != null)
            {
                for (int i = 0; i < zoneMarkers.Length; i++)
                {
                    HullZoneMarker marker = zoneMarkers[i];
                    if (marker == null) continue;
                    Collider volume = marker.GetComponent<Collider>();
                    if (volume == null) continue;
                    if (volume.bounds.Contains(worldPoint))
                    {
                        zone = marker.Zone;
                        return true;
                    }
                }
            }
            zone = default;
            return false;
        }
    }
}
