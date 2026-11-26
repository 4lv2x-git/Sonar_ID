using UnityEngine;
using SonarCrew.Core;
using SonarCrew.Stations;

namespace SonarCrew.Submarine
{
    /// <summary>
    /// Efecto 50/50 del README para la sala de máquinas: al recibir daño grave
    /// la partida elige entre paro completo del motor o fallo eléctrico.
    /// Va en la propia sala para que la tirada ocurra una sola vez por daño.
    /// </summary>
    [RequireComponent(typeof(HullRoom))]
    public class EngineRoomFailure : MonoBehaviour
    {
        /// <summary>Resultado de la tirada, para poder comprobarlo en pruebas.</summary>
        public enum FailureMode
        {
            None,
            EngineStall,
            ElectricalFailure
        }

        [SerializeField] private EngineStation engineStation;
        [SerializeField] private PowerSystem powerSystem;
        [Range(0f, 1f)]
        [SerializeField] private float electricalFailureChance = 0.5f;

        private HullRoom _zone;
        private FailureMode _mode = FailureMode.None;

        public FailureMode LastMode => _mode;

        private void Awake()
        {
            _zone = GetComponent<HullRoom>();
        }

        private void OnEnable()
        {
            if (_zone != null)
                _zone.DamageChanged += OnDamageChanged;
        }

        private void OnDisable()
        {
            if (_zone != null)
                _zone.DamageChanged -= OnDamageChanged;
        }

        private void OnDamageChanged(HullRoom zone, DamageLevel level)
        {
            if (level >= DamageLevel.Grave)
                RollFailure();
            else
                ClearFailure();
        }

        /// <summary>Resuelve la tirada del README y aplica el efecto elegido.</summary>
        private void RollFailure()
        {
            if (_mode != FailureMode.None) return;

            bool electrical = Random.value < electricalFailureChance;
            if (electrical)
            {
                _mode = FailureMode.ElectricalFailure;
                if (powerSystem != null) powerSystem.SetFailure(true);
                Debug.Log("[Sala de máquinas] Fallo eléctrico: las estaciones dependientes pierden energía.");
            }
            else
            {
                _mode = FailureMode.EngineStall;
                if (engineStation != null) engineStation.SetMotorAvailable(false);
                Debug.Log("[Sala de máquinas] Paro completo del motor: el submarino deja de acelerar.");
            }
        }

        private void ClearFailure()
        {
            if (_mode == FailureMode.None) return;

            if (_mode == FailureMode.ElectricalFailure && powerSystem != null)
                powerSystem.SetFailure(false);
            if (_mode == FailureMode.EngineStall && engineStation != null)
                engineStation.SetMotorAvailable(true);

            Debug.Log("[Sala de máquinas] Reparada: se restaura la función de la sala.");
            _mode = FailureMode.None;
        }
    }
}
