using UnityEngine;

namespace SonarCrew.Environment
{
    /// <summary>
    /// Corriente lateral o vertical del mapa (hito 6a). Volumen invisible con
    /// gizmo que empuja al casco del submarino mientras lo solapa.
    /// La fuerza vive en CurrentConfig y se define en ejes de diseño del mapa:
    /// al girar el casco, el WorldRoot gira con el mundo y TransformDirection
    /// mantiene el empuje apuntando al mismo rumbo del mapa.
    /// Física solo en FixedUpdate. El solape se mide con OverlapBox sin alocar
    /// y aplicando una única fuerza por rigidbody: el proxy es un collider
    /// compuesto (uno por sala) y OnTriggerStay sumaría el empuje por sala.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CurrentVolume : MonoBehaviour
    {
        [Header("Configuración (ScriptableObject)")]
        [SerializeField] private CurrentConfig config;

        [Header("Objetivo")]
        [Tooltip("Solo empuja colliders del casco. Excluir Interior y World/Hazard.")]
        [SerializeField] private LayerMask hullMask = 0;

        private static readonly Collider[] s_overlaps = new Collider[32];
        private BoxCollider _box;

        /// <summary>Empuje configurado, en ejes de diseño del mapa (para gizmos y tests).</summary>
        public Vector3 Force => config != null ? config.force : Vector3.zero;

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
        }

        private void FixedUpdate()
        {
            if (config == null) return;

            Vector3 halfExtents = Vector3.Scale(_box.size * 0.5f, transform.lossyScale);
            Vector3 center = transform.TransformPoint(_box.center);
            int hits = Physics.OverlapBoxNonAlloc(center, halfExtents, s_overlaps,
                transform.rotation, hullMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits; i++)
            {
                Rigidbody body = s_overlaps[i].attachedRigidbody;
                if (body == null) continue;

                // El proxy aporta varios colliders al mismo rigidbody: aplicar
                // solo el primero de cada cuerpo evita multiplicar el empuje.
                bool yaAplicado = false;
                for (int j = 0; j < i; j++)
                {
                    if (s_overlaps[j].attachedRigidbody == body) { yaAplicado = true; break; }
                }
                if (yaAplicado) continue;

                body.AddForce(transform.TransformDirection(config.force), ForceMode.Acceleration);
            }
        }

        private void OnDrawGizmos()
        {
            if (!TryGetComponent(out BoxCollider box)) return;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);

            // Flecha del empuje: TransformDirection mantiene los ejes de diseño.
            Vector3 force = Force;
            if (force.sqrMagnitude > 0.001f)
            {
                Vector3 dir = force.normalized;
                Vector3 from = box.center;
                Vector3 to = from + dir * (3f + force.magnitude * 2f);
                Gizmos.DrawLine(from, to);
                Gizmos.DrawLine(to, to - dir * 1.5f + Vector3.up * 0.8f);
                Gizmos.DrawLine(to, to - dir * 1.5f - Vector3.up * 0.8f);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
