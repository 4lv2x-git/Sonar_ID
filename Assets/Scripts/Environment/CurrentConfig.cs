using UnityEngine;

namespace SonarCrew.Environment
{
    /// <summary>
    /// Balance de una corriente (AGENTS.md §5, hito 6a). La fuerza se define en
    /// ejes de diseño del mapa (los mismos de MapPosition) y en m/s², igual que
    /// el resto de aceleraciones del motor. Nada de literales en escena.
    /// </summary>
    [CreateAssetMenu(fileName = "CurrentConfig", menuName = "SonarCrew/Current Config")]
    public class CurrentConfig : ScriptableObject
    {
        [Tooltip("Empuje de la corriente en m/s² (aceleración sobre el casco), en ejes de diseño del mapa.")]
        public Vector3 force = Vector3.zero;
    }
}
