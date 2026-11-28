using UnityEngine;
using SonarCrew.Core;
using SonarCrew.Multiplayer;
using SonarCrew.Submarine;

namespace SonarCrew.Stations
{
    /// <summary>
    /// Palancas de la sala de mandos: avance/retroceso con el stick derecho
    /// (vertical) y ascenso/descenso con el mismo stick (horizontal).
    /// El stick izquierdo queda libre para caminar: así el tripulante puede
    /// alejarse de la estación y se libera por distancia.
    /// Escribe en el estado de mandos que consumirá el motor del Hito 1.
    /// </summary>
    public class LeversStation : StationBase
    {
        [Header("Palancas")]
        [SerializeField] private Transform throttleLever;
        [SerializeField] private Transform verticalLever;
        [SerializeField] private float leverTiltDegrees = 35f;

        private ISubmarineControls _controls;
        private float _throttle;
        private float _vertical;

        protected override string DefaultStationName => "Palancas";

        private ISubmarineControls Controls
        {
            get
            {
                if (_controls == null && ServiceLocator.TryGet(out ISubmarineControls found))
                    _controls = found;
                return _controls;
            }
        }

        public override string StatusText =>
            "Avance " + _throttle.ToString("+0.00;-0.00;0.00") +
            "   Cota " + _vertical.ToString("+0.00;-0.00;0.00");

        public override string OperateHint =>
            "Stick derecho ▲▼ avance · ◄► cota · [Y] soltar o aléjate";

        protected override void OnOccupied(CrewController crew) => Apply(0f, 0f);

        protected override void OnReleased(CrewController crew) => Apply(0f, 0f);

        protected override void OnOperate(InputRouter router, int slotIndex)
        {
            Vector2 stick = router.GetLook(slotIndex);
            Apply(stick.y, stick.x);
        }

        private void Apply(float throttle, float vertical)
        {
            _throttle = throttle;
            _vertical = vertical;

            ISubmarineControls controls = Controls;
            if (controls != null)
            {
                controls.SetThrottle(throttle);
                controls.SetVertical(vertical);
            }

            if (throttleLever != null)
                throttleLever.localRotation = Quaternion.Euler(-throttle * leverTiltDegrees, 0f, 0f);
            if (verticalLever != null)
                verticalLever.localRotation = Quaternion.Euler(-vertical * leverTiltDegrees, 0f, 0f);
        }
    }
}
