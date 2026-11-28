using UnityEngine;
using SonarCrew.Core;
using SonarCrew.Multiplayer;

namespace SonarCrew.Stations
{
    /// <summary>
    /// Sensores de coordenadas y cota de la sala de sensores.
    /// Con daño grave devuelven datos erróneos y aleatorios (README).
    /// El sistema de navegación real llega con el Hito 3.
    /// </summary>
    public class SensorStation : StationBase
    {
        [Header("Sensores")]
        [Tooltip("Punto de referencia del casco para calcular coordenadas.")]
        [SerializeField] private Transform referencePoint;
        [SerializeField] private float impreciseRange = 999f;

        private SensorReading _reading;

        protected override string DefaultStationName => "Sensores";

        public SensorReading Reading => _reading;

        public override string OperateHint => "[X] nueva lectura · [Y] soltar o aléjate";

        public override string StatusText
        {
            get
            {
                if (!HasPower) return "SENSORES SIN ENERGÍA";
                if (HasSevereDamage)
                    return "DATOS ERRÓNEOS · X " + _reading.X.ToString("0") +
                           "  Z " + _reading.Z.ToString("0") +
                           "  Cota " + _reading.Depth.ToString("0");
                return "X " + _reading.X.ToString("0") +
                       "   Z " + _reading.Z.ToString("0") +
                       "   Cota " + _reading.Depth.ToString("0");
            }
        }

        protected override void OnOccupied(CrewController crew) => RefreshReading();

        protected override void OnOperate(InputRouter router, int slotIndex)
        {
            if (router.GetAttackPressed(slotIndex))
                RefreshReading();
        }

        /// <summary>Toma una lectura nueva de los sensores.</summary>
        public void RefreshReading()
        {
            if (HasSevereDamage)
            {
                _reading = new SensorReading
                {
                    X = Random.Range(-impreciseRange, impreciseRange),
                    Z = Random.Range(-impreciseRange, impreciseRange),
                    Depth = Random.Range(0f, impreciseRange),
                    Imprecise = true
                };
                return;
            }

            Vector3 origin = referencePoint != null ? referencePoint.position : transform.position;
            _reading = new SensorReading
            {
                X = origin.x,
                Z = origin.z,
                Depth = Mathf.Max(0f, -origin.y),
                Imprecise = false
            };
        }
    }
}
