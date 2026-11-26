using UnityEngine;

namespace SonarCrew.Submarine
{
    /// <summary>
    /// Stub del suministro eléctrico para el hito 1: siempre disponible.
    /// Sustituir por PowerSystem (hito 2), que decide qué estaciones quedan sin energía.
    /// </summary>
    public class PowerStateStub : MonoBehaviour, IPowerState
    {
        public bool HasPower => true;

        public event System.Action<bool> PowerChanged;
    }
}
