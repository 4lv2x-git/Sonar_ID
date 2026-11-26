using System;
using UnityEngine;
using SonarCrew.Core;

namespace SonarCrew.Submarine
{
    /// <summary>
    /// Suministro eléctrico del submarino (hito 2, persona B).
    /// Es la implementación real del contrato `IPowerState` de A: expone
    /// <see cref="HasPower"/> y <see cref="PowerChanged"/>; cuando ese archivo
    /// llegue al working tree basta con añadirle la interfaz.
    /// Separa las dos causas de corte: el interruptor del jugador
    /// (<see cref="BreakerOn"/>) y el fallo eléctrico por daño
    /// (<see cref="FailureActive"/>). Solo reparar la sala de máquinas retira el
    /// fallo; accionar el interruptor no lo resuelve.
    /// Decide qué salas (y por tanto qué estaciones) quedan sin energía.
    /// </summary>
    public class PowerSystem : MonoBehaviour
    {
        [SerializeField] private bool startBreakerOn = true;
        [SerializeField] private bool startFailed;

        private HullZone[] _zones;

        /// <summary>Se lanza cuando cambia la energía disponible.</summary>
        public event Action<bool> PowerChanged;

        public bool BreakerOn { get; private set; } = true;
        public bool FailureActive { get; private set; }
        public bool HasPower => BreakerOn && !FailureActive;

        private void Awake()
        {
            BreakerOn = startBreakerOn;
            FailureActive = startFailed;
            ServiceLocator.Register(this);
        }

        private void Start()
        {
            // Las salas viven en esta misma escena (SubmarineInterior), así que
            // existen todas cuando arranca el sistema.
            _zones = FindObjectsByType<HullZone>(FindObjectsInactive.Include);
            PublishToZones();
        }

        /// <summary>Interruptor manual de la estación de suministro.</summary>
        public void SetBreaker(bool on)
        {
            if (BreakerOn == on) return;
            BreakerOn = on;
            Refresh();
        }

        public void ToggleBreaker() => SetBreaker(!BreakerOn);

        /// <summary>Corte eléctrico por daño grave en la sala de máquinas.</summary>
        public void SetFailure(bool active)
        {
            if (FailureActive == active) return;
            FailureActive = active;
            Refresh();
        }

        /// <summary>Deja la energía en el estado inicial (reinicio de partida).</summary>
        public void ResetPower()
        {
            BreakerOn = startBreakerOn;
            FailureActive = startFailed;
            Refresh();
        }

        private void Refresh()
        {
            PublishToZones();
            PowerChanged?.Invoke(HasPower);
        }

        private void PublishToZones()
        {
            // Las salas son de esta misma escena; se resuelven una vez y se
            // reutilizan. Si el sistema arranca antes de que existan, se reintenta.
            if (_zones == null)
                _zones = FindObjectsByType<HullZone>(FindObjectsInactive.Include);

            if (_zones == null) return;

            foreach (HullZone zone in _zones)
            {
                if (zone != null)
                    zone.SetPowerAvailable(HasPower);
            }
        }
    }
}
