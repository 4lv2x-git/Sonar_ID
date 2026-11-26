using UnityEngine;
using SonarCrew.Core;
using SonarCrew.Multiplayer;
using SonarCrew.Submarine;

namespace SonarCrew.Stations
{
    /// <summary>
    /// Motor de la sala de máquinas: lo arranca y lo para el tripulante.
    /// El paro completo por daño grave lo aplica <see cref="EngineRoomFailure"/>,
    /// que es quien hace la tirada 50/50 del README.
    /// </summary>
    public class EngineStation : StationBase
    {
        [Header("Motor")]
        [SerializeField] private Transform motorVisual;
        [SerializeField] private Color runningColor = new Color(0.25f, 0.9f, 0.35f);
        [SerializeField] private Color stoppedColor = new Color(0.45f, 0.15f, 0.15f);

        private bool _motorAvailable = true;
        private bool _motorRunning;
        private Renderer _motorRenderer;

        protected override string DefaultStationName => "Motor";

        public bool MotorAvailable => _motorAvailable;
        public bool MotorRunning => _motorRunning;

        public override string OperateHint => "[X] arrancar / parar · [Y] soltar o aléjate";

        public override string StatusText
        {
            get
            {
                if (!HasPower) return "MOTOR SIN ENERGÍA";
                if (!_motorAvailable) return "MOTOR AVERIADO (paro completo)";
                return _motorRunning ? "Motor en marcha" : "Motor parado";
            }
        }

        protected override void OnStationStart()
        {
            if (motorVisual != null)
                _motorRenderer = motorVisual.GetComponentInChildren<Renderer>();
        }

        protected override void OnOperate(InputRouter router, int slotIndex)
        {
            if (router.GetAttackPressed(slotIndex))
                ToggleMotor();
        }

        public void ToggleMotor()
        {
            if (!_motorAvailable) return;
            SetMotorRunning(!_motorRunning);
        }

        public void SetMotorRunning(bool running)
        {
            _motorRunning = running;

            if (ServiceLocator.TryGet(out ISubmarineControls controls))
                controls.SetEngineRunning(running);

            if (_motorRenderer != null && _motorRenderer.material != null)
                _motorRenderer.material.color = running ? runningColor : stoppedColor;
        }

        /// <summary>Paro completo del motor: no se puede volver a arrancar hasta reparar.</summary>
        public void SetMotorAvailable(bool available)
        {
            _motorAvailable = available;
            if (available) return;

            _motorRunning = false;
            if (ServiceLocator.TryGet(out ISubmarineControls controls))
                controls.SetEngineRunning(false);
        }
    }
}
