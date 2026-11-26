using UnityEngine;
using SonarCrew.Multiplayer;

namespace SonarCrew.Stations
{
    /// <summary>
    /// Sónar de la sala de sensores. En el Hito 2 solo es la estación: emite el
    /// barrido y guarda el número de contactos. El detector real (SphereCast por
    /// capas) llega en el Hito 3 y sustituirá el cuerpo de <see cref="Ping"/>.
    /// Con daño grave el sónar deja de funcionar.
    /// </summary>
    public class SonarStation : StationBase
    {
        [Header("Sónar")]
        [SerializeField] private float pingCooldown = 1.2f;

        private float _nextPingTime;
        private int _contactCount;
        private bool _pinged;

        protected override string DefaultStationName => "Sónar";

        public int ContactCount => _contactCount;
        public bool IsOperational => !HasSevereDamage && HasPower;

        public override string StatusText
        {
            get
            {
                if (HasSevereDamage) return "SÓNAR AVERIADO";
                if (!HasPower) return "SÓNAR SIN ENERGÍA";
                if (!_pinged) return "Sónar listo · acciona para barrer";
                return _contactCount == 0
                    ? "Barrido completado · sin contactos"
                    : "Barrido completado · " + _contactCount + " contactos";
            }
        }

        protected override void OnOperate(InputRouter router, int slotIndex)
        {
            if (router.GetAttackPressed(slotIndex))
                Ping();
        }

        /// <summary>Emite un barrido. Hito 3: rellenar la lista de contactos real.</summary>
        public void Ping()
        {
            if (Time.time < _nextPingTime) return;
            _nextPingTime = Time.time + pingCooldown;

            if (!IsOperational)
            {
                _contactCount = 0;
                _pinged = false;
                return;
            }

            _contactCount = 0;
            _pinged = true;
        }
    }
}
