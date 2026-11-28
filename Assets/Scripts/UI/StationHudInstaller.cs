using UnityEngine;
using SonarCrew.Multiplayer;

namespace SonarCrew.UI
{
    /// <summary>
    /// HUD por jugador sin tocar la escena de A: las cámaras de los slots viven en
    /// `Bootstrap` (propiedad de A) y el HUD es de B, así que este instalador —que
    /// vive en `SubmarineInterior`— engancha el <see cref="StationHUD"/> a la
    /// cámara de cada <see cref="PlayerSlot"/> al arrancar.
    /// </summary>
    public class StationHudInstaller : MonoBehaviour
    {
        [Tooltip("Incluye los slots inactivos: el HUD aparece cuando se les asigna gamepad.")]
        [SerializeField] private bool includeInactiveSlots = true;

        private void Start()
        {
            FindObjectsInactive search = includeInactiveSlots
                ? FindObjectsInactive.Include
                : FindObjectsInactive.Exclude;

            foreach (PlayerSlot slot in FindObjectsByType<PlayerSlot>(search))
            {
                if (slot == null) continue;

                // La cámara del slot se busca por jerarquía: PlayerSlot es de A y
                // B no le añade API solo para esto.
                Camera camera = slot.GetComponentInChildren<Camera>(true);
                GameObject target = camera != null ? camera.gameObject : slot.gameObject;
                if (target.GetComponent<StationHUD>() == null)
                    target.AddComponent<StationHUD>();
            }

            // La victoria la comparte toda la tripulación: una sola pantalla.
            if (GetComponent<VictoryHUD>() == null)
                gameObject.AddComponent<VictoryHUD>();
        }
    }
}
