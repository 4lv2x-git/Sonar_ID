using UnityEngine;

namespace SonarCrew.Environment
{
    /// <summary>
    /// Raíz del mapa SubmarineTest (AGENTS.md §3.1 y §4): todo el mapa cuelga de
    /// ella y sus hijos usan coordenadas de diseño (= MapPosition). Su pose local
    /// es la pose de aparejo del proxy en Bootstrap, de modo que al parentarla
    /// bajo el WorldRoot (que arranca en la pose inversa) las coordenadas de
    /// diseño del mapa coinciden exactamente con MapPosition.
    /// Se parenta con worldPositionStays=false para conservar esa pose local.
    /// Find solo en Start (una vez), nunca en Update (§7).
    /// </summary>
    [DisallowMultipleComponent]
    public class MapRootAttachment : MonoBehaviour
    {
        [Tooltip("WorldRoot de Bootstrap. Si se deja vacío, se busca por nombre una vez en Start.")]
        [SerializeField] private Transform worldRoot;

        private void Start()
        {
            if (worldRoot == null)
            {
                GameObject found = GameObject.Find("WorldRoot");
                if (found != null) worldRoot = found.transform;
            }

            if (worldRoot == null)
            {
                // Escena abierta en solitario (previsualización o edición del
                // mapa): sin marco de referencia no hay nada que parentar.
                Debug.LogWarning("MapRootAttachment: no hay WorldRoot en la escena; " +
                                 "abre Bootstrap + SubmarineTest juntas para jugar.", this);
                return;
            }

            if (transform.parent == worldRoot) return;
            transform.SetParent(worldRoot, false);
        }
    }
}
