using System;
using UnityEngine;
using SonarCrew.Core;

namespace SonarCrew.Environment
{
    /// <summary>
    /// Único tesoro de la versión inicial (hito 4). Es contenido del mapa, así que
    /// cuelga del `WorldRoot` y se desplaza con él; por eso se coloca a partir de la
    /// cota del fondo, no del casco. Lo recoge el gancho cuando su punta entra en el
    /// radio de captura y entonces avisa a la partida (victoria en `GameFlow`).
    /// </summary>
    public class TreasureCollectible : MonoBehaviour
    {
        [SerializeField] private string displayName = "Cofre del tesoro";

        /// <summary>Se lanza una sola vez, cuando el gancho captura el cofre.</summary>
        public event Action<TreasureCollectible> Recovered;

        public string DisplayName => displayName;
        public bool IsRecovered { get; private set; }

        private void Awake()
        {
            // Un solo tesoro en la versión inicial: el gancho lo resuelve por aquí.
            ServiceLocator.Register(this);
        }

        /// <summary>
        /// Recoge el tesoro una sola vez. Devuelve false si ya estaba a bordo para
        /// que el gancho no repita la captura.
        /// </summary>
        public bool TryRecover()
        {
            if (IsRecovered) return false;

            IsRecovered = true;
            Recovered?.Invoke(this);
            GameEvents.RaiseTreasureRecovered(this);
            return true;
        }

        /// <summary>Cuelga el cofre del gancho para que suba con él (ya no es mapa).</summary>
        public void AttachTo(Transform carrier)
        {
            if (carrier == null) return;
            transform.SetParent(carrier, true);
        }
    }
}
