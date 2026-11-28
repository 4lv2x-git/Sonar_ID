using UnityEngine;
using SonarCrew.Core;

namespace SonarCrew.Submarine
{
    /// <summary>
    /// Estado de mandos compartido. Las estaciones escriben aquí y el motor del
    /// Hito 1 leerá de aquí, de modo que ambos hitos avanzan sin colisionar.
    /// Los bloqueos por daño anulan la orden sin borrarla, para que al reparar
    /// la sala el mando vuelva a responder.
    /// </summary>
    public class SubmarineControlState : MonoBehaviour, ISubmarineControls
    {
        [SerializeField] private bool engineRunningAtStart;

        private float _steer;
        private float _throttle;
        private float _vertical;
        private bool _engineRunning;
        private bool _wheelLocked;

        public float Steer => _wheelLocked ? 0f : _steer;
        public float Throttle => _wheelLocked || !_engineRunning ? 0f : _throttle;
        public float Vertical => _wheelLocked ? 0f : _vertical;
        public bool EngineRunning => _engineRunning;
        public bool WheelLocked => _wheelLocked;

        private void Awake()
        {
            _engineRunning = engineRunningAtStart;
            ServiceLocator.Register<ISubmarineControls>(this);
        }

        public void SetSteer(float value) => _steer = Mathf.Clamp(value, -1f, 1f);
        public void SetThrottle(float value) => _throttle = Mathf.Clamp(value, -1f, 1f);
        public void SetVertical(float value) => _vertical = Mathf.Clamp(value, -1f, 1f);

        public void SetEngineRunning(bool running) => _engineRunning = running;

        /// <summary>Daño grave en la sala de mandos: el submarino queda quieto.</summary>
        public void SetWheelLocked(bool locked) => _wheelLocked = locked;

        /// <summary>Devuelve los mandos a punto muerto (reinicio de partida).</summary>
        public void ResetControls()
        {
            _steer = 0f;
            _throttle = 0f;
            _vertical = 0f;
            _wheelLocked = false;
            _engineRunning = engineRunningAtStart;
        }
    }
}
