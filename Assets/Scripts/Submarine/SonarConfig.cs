using UnityEngine;

namespace SonarCrew.Submarine
{
    /// <summary>
    /// Balance del sónar (AGENTS.md §7: configuración en ScriptableObject).
    /// Crear el asset desde Create > SonarCrew > Sonar Config.
    /// </summary>
    [CreateAssetMenu(fileName = "SonarConfig", menuName = "SonarCrew/Sonar Config")]
    public class SonarConfig : ScriptableObject
    {
        [Tooltip("Radio de detección del sónar alrededor del casco (m).")]
        [SerializeField] private float range = 120f;

        [Tooltip("Segundos entre emisiones de contactos.")]
        [SerializeField] private float pingInterval = 1.5f;

        [Tooltip("Máximo de contactos por emisión (límite para HUD y mapa).")]
        [SerializeField] private int maxContacts = 64;

        public float Range => range;
        public float PingInterval => pingInterval;
        public int MaxContacts => maxContacts;
    }
}
