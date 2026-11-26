using UnityEngine;
using SonarCrew.Multiplayer;
using SonarCrew.Core;

namespace SonarCrew.Stations
{
    /// <summary>
    /// Gancho recolector de la sala del gancho. En el Hito 2 solo baja y sube el
    /// cabestrante; recoger el tesoro y la cámara de baja calidad llegan con el
    /// Hito 4. Con daño grave el gancho se retrae solo y queda fuera de servicio.
    /// </summary>
    public class GrappleStation : StationBase
    {
        [Header("Gancho")]
        [SerializeField] private Transform hook;
        [SerializeField] private float maxDepth = 6f;
        [SerializeField] private float hookSpeed = 2.5f;

        private float _extension;
        private bool _lowered;
        private Vector3 _hookRestPosition;

        protected override string DefaultStationName => "Gancho recolector";

        protected override bool OutOfServiceByDamage => HasSevereDamage;

        public float Extension => _extension;
        public bool IsLowered => _lowered;

        public override string StatusText
        {
            get
            {
                if (HasSevereDamage) return "GANCHO RETRAÍDO (daño grave)";
                if (!HasPower) return "GANCHO SIN ENERGÍA";
                if (!_lowered && _extension <= 0.001f) return "Gancho recogido";
                return "Gancho " + Mathf.RoundToInt(_extension * 100f) + "% desplegado";
            }
        }

        protected override void OnStationStart()
        {
            if (hook != null)
                _hookRestPosition = hook.localPosition;
        }

        protected override void OnOperate(InputRouter router, int slotIndex)
        {
            if (router.GetAttackPressed(slotIndex))
                ToggleHook();
        }

        protected override void OnRoomDamage(DamageLevel level)
        {
            // README: con daño grave el gancho se retrae solo y deja de funcionar.
            if (level >= DamageLevel.Severe)
                Retract();
        }

        protected override void Update()
        {
            base.Update();
            AnimateHook();
        }

        public void ToggleHook()
        {
            if (HasSevereDamage) return;
            if (!HasPower) return;
            _lowered = !_lowered;
        }

        /// <summary>Retrae el gancho de golpe (daño grave en la sala).</summary>
        public void Retract()
        {
            _lowered = false;
            _extension = 0f;
            if (hook != null)
                hook.localPosition = _hookRestPosition;
        }

        private void AnimateHook()
        {
            if (hook == null) return;

            float target = _lowered ? 1f : 0f;
            if (Mathf.Approximately(_extension, target)) return;

            _extension = Mathf.MoveTowards(_extension, target, hookSpeed / Mathf.Max(maxDepth, 0.01f) * Time.deltaTime);
            hook.localPosition = _hookRestPosition + Vector3.down * (_extension * maxDepth);
        }
    }
}
