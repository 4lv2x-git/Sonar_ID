using UnityEngine;

namespace SonarCrew.Submarine
{
    /// <summary>
    /// Configuración del marco de referencia del submarino (AGENTS.md §3.1).
    /// Crear el asset desde Create > SonarCrew > Submarine Frame Config.
    /// </summary>
    [CreateAssetMenu(fileName = "SubmarineFrameConfig", menuName = "SonarCrew/Submarine Frame Config")]
    public class SubmarineFrameConfig : ScriptableObject
    {
        [Tooltip("Distancia de recorrido (m) a la que SubmarineFrame rebasa mundo y proxy al origen.")]
        [SerializeField] private float rebaseDistance = 2000f;

        public float RebaseDistance => rebaseDistance;
    }
}
