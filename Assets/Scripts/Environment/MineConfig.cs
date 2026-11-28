using UnityEngine;

namespace SonarCrew.Environment
{
    /// <summary>
    /// Balance de las minas del mapa (hito 6a). El impulso de la explosión se
    /// aplica al rigidbody del proxy; la clasificación leve/grave/extremo la
    /// hace DamageSystem (persona B) a partir de la colisión física real.
    /// </summary>
    [CreateAssetMenu(fileName = "MineConfig", menuName = "SonarCrew/Mine Config")]
    public class MineConfig : ScriptableObject
    {
        [Tooltip("Impulso de la explosión en kg·m/s (con masa 1 del proxy equivale a m/s de patada).")]
        public float impulse = 8f;
    }
}
