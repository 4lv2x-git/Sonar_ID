using UnityEngine;
using SonarCrew.Core;

namespace SonarCrew.Submarine
{
    /// <summary>
    /// Coordenadas y altitud del submarino (hito 3, AGENTS.md §5), leídas siempre de
    /// ISubmarinePose.MapPosition: estables frente al rebase. Solo expone datos; el
    /// mapa de papel y el HUD de navegación son de la persona B.
    /// </summary>
    public class NavigationSystem : MonoBehaviour
    {
        [Header("Depuración (solo lectura)")]
        [SerializeField] private Vector2 debugMapCoordinates;
        [SerializeField] private float debugAltitude;
        [SerializeField] private float debugHeading;

        /// <summary>Coordenadas de mapa en el plano (x, z).</summary>
        public Vector2 MapCoordinates
        {
            get
            {
                Vector3 position = TryMapPosition();
                return new Vector2(position.x, position.z);
            }
        }

        /// <summary>Altitud en el mapa (m, eje y de MapPosition).</summary>
        public float Altitude => TryMapPosition().y;

        /// <summary>Rumbo en grados sobre el plano del mapa (0 = +Z).</summary>
        public float Heading
        {
            get
            {
                return ServiceLocator.TryGet(out ISubmarinePose pose) ? pose.Yaw : 0f;
            }
        }

        private void Awake()
        {
            ServiceLocator.Register(this);
        }

        private void Update()
        {
            debugMapCoordinates = MapCoordinates;
            debugAltitude = Altitude;
            debugHeading = Heading;
        }

        private static Vector3 TryMapPosition()
        {
            return ServiceLocator.TryGet(out ISubmarinePose pose) ? pose.MapPosition : Vector3.zero;
        }
    }
}
