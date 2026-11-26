using UnityEngine;
using SonarCrew.Core;
using SonarCrew.Multiplayer;
using SonarCrew.Submarine;

namespace SonarCrew.Stations
{
    /// <summary>
    /// Suministro eléctrico de la sala de máquinas: el interruptor que da o quita
    /// energía a las estaciones dependientes. No necesita energía para poder
    /// accionarse (si no, un corte no tendría vuelta atrás).
    /// </summary>
    public class PowerStation : StationBase
    {
        [Header("Suministro")]
        [SerializeField] private Transform breakerLever;
        [SerializeField] private float leverTiltDegrees = 25f;

        private PowerSystem _powerSystem;

        protected override string DefaultStationName => "Suministro eléctrico";

        public override string OperateHint => "[X] conectar / cortar · [Y] soltar o aléjate";

        public override string StatusText
        {
            get
            {
                if (Power == null) return "Suministro desconectado";
                if (Power.FailureActive) return "AVERÍA ELÉCTRICA · requiere reparación";
                return Power.HasPower ? "Red eléctrica activa" : "Red eléctrica cortada";
            }
        }

        private PowerSystem Power
        {
            get
            {
                if (_powerSystem == null && ServiceLocator.TryGet(out PowerSystem found))
                    _powerSystem = found;
                return _powerSystem;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            // El suministro no depende de sí mismo.
            ConfigurePowerRequirement(false);
        }

        protected override void OnStationStart()
        {
            RefreshLever();
        }

        protected override void OnOperate(InputRouter router, int slotIndex)
        {
            if (!router.GetAttackPressed(slotIndex)) return;
            PowerSystem power = Power;
            if (power == null) return;

            power.ToggleBreaker();
            RefreshLever();
        }

        protected override void OnPowerChanged(bool powered)
        {
            RefreshLever();
        }

        private void RefreshLever()
        {
            if (breakerLever == null) return;
            float angle = Power != null && Power.BreakerOn ? leverTiltDegrees : -leverTiltDegrees;
            breakerLever.localRotation = Quaternion.Euler(-angle, 0f, 0f);
        }
    }
}
