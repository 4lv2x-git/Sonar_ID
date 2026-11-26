using UnityEngine;
using SonarCrew.Multiplayer;
using SonarCrew.Core;

namespace SonarCrew.Stations
{
    /// <summary>
    /// Periscopio de la sala de mandos: cede la vista al ocupante y gira 360º en
    /// horizontal con el stick derecho. Con daño grave se puede seguir mirando por
    /// él, pero la cámara no rota.
    /// </summary>
    public class PeriscopeStation : StationBase
    {
        [Header("Periscopio")]
        [SerializeField] private Transform periscopeHead;
        [SerializeField] private Camera periscopeCamera;
        [SerializeField] private float turnSpeed = 140f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 55f;

        private Camera _crewCamera;
        private bool _locked;
        private float _yaw;
        private float _pitch;

        protected override string DefaultStationName => "Periscopio";

        public bool IsLocked => _locked;

        public override string StatusText => _locked
            ? "Periscopio fijo (daño grave)"
            : "Rumbo " + Mathf.Repeat(_yaw, 360f).ToString("0") + "º";

        protected override void OnOccupied(CrewController crew)
        {
            Camera crewCamera = crew != null ? crew.CrewCamera : null;
            if (crewCamera == null || periscopeCamera == null) return;

            _crewCamera = crewCamera;
            periscopeCamera.rect = _crewCamera.rect;
            periscopeCamera.depth = _crewCamera.depth + 10;
            periscopeCamera.enabled = true;
        }

        protected override void OnReleased(CrewController crew)
        {
            if (periscopeCamera != null)
                periscopeCamera.enabled = false;
            _crewCamera = null;
        }

        protected override void OnOperate(InputRouter router, int slotIndex)
        {
            if (_locked) return;

            Vector2 look = router.GetLook(slotIndex);
            _yaw += look.x * turnSpeed * Time.deltaTime;
            _pitch = Mathf.Clamp(_pitch - look.y * turnSpeed * Time.deltaTime, minPitch, maxPitch);
            ApplyRotation();
        }

        protected override void OnRoomDamage(DamageLevel level)
        {
            _locked = level >= DamageLevel.Severe;
        }

        private void ApplyRotation()
        {
            if (periscopeHead == null) return;
            periscopeHead.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }
    }
}
