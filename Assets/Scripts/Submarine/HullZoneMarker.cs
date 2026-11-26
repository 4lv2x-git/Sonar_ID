using UnityEngine;
using SonarCrew.Core;

namespace SonarCrew.Submarine
{
    /// <summary>
    /// Marca el collider de una sala del casco con su HullZone (AGENTS.md §3.1).
    /// Los colliders hijo del proxy son triggers: no intervienen en la física del
    /// casco (la lleva la Capsule). El DamageSystem de la persona B resuelve el
    /// punto de contacto de una colisión a su sala con estos volúmenes.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class HullZoneMarker : MonoBehaviour
    {
        [SerializeField] private HullZone zone;

        public HullZone Zone => zone;

#if UNITY_EDITOR
        /// <summary>Fija la zona desde la herramienta de montaje del Editor.</summary>
        public void EditorSetZone(HullZone value)
        {
            zone = value;
        }
#endif
    }
}
