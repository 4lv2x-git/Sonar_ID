using UnityEngine;
using SonarCrew.Core;
using SonarCrew.Multiplayer;
using SonarCrew.Submarine;

namespace SonarCrew.Stations
{
    /// <summary>
    /// Timón de la sala de mandos: giro (yaw) con el stick izquierdo.
    /// Con daño grave la sala se queda sin timón: no responde y el submarino
    /// queda quieto (lo aplica el estado de mandos, que anula avance y giro).
    /// </summary>
    public class WheelStation : StationBase
    {
        [Header("Timón")]
        [SerializeField] private Transform wheelVisual;
        [SerializeField] private float wheelDegrees = 220f;

        private ISubmarineControls _controls;
        private float _steer;
        private bool _locked;

        protected override string DefaultStationName => "Timón";

        private ISubmarineControls Controls
        {
            get
            {
                if (_controls == null && ServiceLocator.TryGet(out ISubmarineControls found))
                    _controls = found;
                return _controls;
            }
        }

        public bool IsLocked => _locked;
        public float Steer => _steer;

        public override string StatusText => _locked
            ? "Timón bloqueado (daño grave)"
            : "Timón " + _steer.ToString("+0.00;-0.00;0.00");

        protected override void OnOccupied(CrewController crew)
        {
            ApplySteer(0f);
        }

        protected override void OnReleased(CrewController crew)
        {
            ApplySteer(0f);
        }

        protected override void OnOperate(InputRouter router, int slotIndex)
        {
            ApplySteer(_locked ? 0f : router.GetMove(slotIndex).x);
        }

        protected override void OnRoomDamage(DamageLevel level)
        {
            bool locked = level >= DamageLevel.Severe;
            if (locked == _locked) return;

            _locked = locked;
            ISubmarineControls controls = Controls;
            if (controls != null)
                controls.SetWheelLocked(locked);
            if (locked)
                ApplySteer(0f);
        }

        private void ApplySteer(float steer)
        {
            _steer = steer;
            ISubmarineControls controls = Controls;
            if (controls != null)
                controls.SetSteer(steer);
            if (wheelVisual != null)
                wheelVisual.localRotation = Quaternion.Euler(0f, 0f, -steer * wheelDegrees);
        }
    }
}
