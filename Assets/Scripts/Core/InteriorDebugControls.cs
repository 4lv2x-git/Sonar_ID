using UnityEngine;
using UnityEngine.InputSystem;
using SonarCrew.Submarine;

namespace SonarCrew.Core
{
    /// <summary>
    /// Atajos de teclado para probar el Hito 2 mientras el daño por colisión no
    /// existe (Hito 5). F1-F5 alternan un daño grave en cada sala, F6 repara todo,
    /// F7 conmuta la red eléctrica y F8 simula la avería eléctrica.
    /// Es andamiaje de pruebas: se retira cuando llegue el DamageSystem.
    /// </summary>
    public class InteriorDebugControls : MonoBehaviour
    {
        [SerializeField] private HullRoom[] zones = new HullRoom[5];
        [SerializeField] private PowerSystem powerSystem;
        [SerializeField] private bool enabledInBuilds;

        /// <summary>Teclas de daño por sala, en el orden del enum <see cref="HullZone"/>.</summary>
        private static readonly Key[] ZoneKeys = { Key.F1, Key.F2, Key.F3, Key.F4, Key.F5 };

        private void Update()
        {
            if (!enabledInBuilds && !Application.isEditor) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            for (int i = 0; i < ZoneKeys.Length && i < zones.Length; i++)
            {
                if (keyboard[ZoneKeys[i]].wasPressedThisFrame)
                    ToggleSevereDamage(zones[i]);
            }

            if (keyboard[Key.F6].wasPressedThisFrame)
                RepairAll();

            if (keyboard[Key.F7].wasPressedThisFrame && powerSystem != null)
                powerSystem.ToggleBreaker();

            if (keyboard[Key.F8].wasPressedThisFrame && powerSystem != null)
                powerSystem.SetFailure(!powerSystem.FailureActive);
        }

        private static void ToggleSevereDamage(HullRoom zone)
        {
            if (zone == null) return;

            if (zone.HasSevereDamage)
            {
                zone.Repair();
                Debug.Log("[Prueba] " + zone.DisplayName + " reparada.");
                return;
            }

            zone.SetDamage(DamageLevel.Grave);
            Debug.Log("[Prueba] " + zone.DisplayName + " con daño grave.");
        }

        public void RepairAll()
        {
            foreach (HullRoom zone in zones)
                zone?.Repair();

            Debug.Log("[Prueba] Todas las salas reparadas.");
        }
    }
}
