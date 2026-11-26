using UnityEngine;
using SonarCrew.Core;

namespace SonarCrew.Submarine
{
    /// <summary>
    /// Marco de referencia del submarino (AGENTS.md §3.1). En cada FixedUpdate:
    /// acumula el recorrido del proxy en MapPosition (coordenada de mapa estable,
    /// nunca se rebasa), copia la pose inversa completa al WorldRoot y, al superar
    /// rebaseDistance, traslada proxy y mundo juntos al origen sin tocar MapPosition.
    /// Es la única autoridad sobre el WorldRoot y la única fuente de ISubmarinePose.
    /// </summary>
    public class SubmarineFrame : MonoBehaviour, ISubmarinePose
    {
        [Header("Referencias")]
        [SerializeField] private SubmarineProxy proxy;
        [SerializeField] private Transform worldRoot;
        [SerializeField] private SubmarineFrameConfig config;

        [Header("Depuración (solo lectura)")]
        [SerializeField] private Vector3 debugMapPosition;
        [SerializeField] private float debugLocalDistance;
        [SerializeField] private int debugRebases;

        private Rigidbody _body;
        private Vector3 _lastLocalPosition;
        private int _rebaseCount;

        /// <summary>Coordenada de mapa del casco: acumula el recorrido y nunca se rebasa.</summary>
        public Vector3 MapPosition { get; private set; }

        /// <summary>Rumbo en grados sobre el plano del mapa (0 = +Z).</summary>
        public float Yaw => proxy != null ? proxy.transform.eulerAngles.y : 0f;

        /// <summary>Velocidad lineal del casco en ejes del mapa (m/s).</summary>
        public Vector3 Velocity => _body != null ? _body.linearVelocity : Vector3.zero;

        public Vector3 WorldToSubmarine(Vector3 worldPoint)
        {
            return proxy != null ? proxy.transform.InverseTransformPoint(worldPoint) : worldPoint;
        }

        public Vector3 SubmarineToWorld(Vector3 submarinePoint)
        {
            return proxy != null ? proxy.transform.TransformPoint(submarinePoint) : submarinePoint;
        }

        private float RebaseDistance => config != null ? Mathf.Max(1f, config.RebaseDistance) : 2000f;

        private void Awake()
        {
            if (proxy == null) proxy = GetComponent<SubmarineProxy>();
            if (proxy != null) _body = proxy.GetComponent<Rigidbody>();

            if (proxy != null)
            {
                // El mapa arranca en la posición de aparejo: el primer tramo de
                // recorrido no se cuenta como desplazamiento de mapa.
                _lastLocalPosition = proxy.transform.position;
                MapPosition = _lastLocalPosition;
            }

            ServiceLocator.Register<ISubmarinePose>(this);
        }

        private void FixedUpdate()
        {
            if (proxy == null || _body == null) return;

            Vector3 localPosition = proxy.transform.position;

            // Recorrido acumulado: delta físico del último paso de física. El
            // rebase no altera MapPosition porque corrige también la referencia.
            MapPosition += localPosition - _lastLocalPosition;
            _lastLocalPosition = localPosition;

            // Pose inversa completa: el mundo se ve desde el casco (interior fijo).
            ApplyInversePose();

            if (localPosition.sqrMagnitude >= RebaseDistance * RebaseDistance)
            {
                Rebase(localPosition);
                ApplyInversePose();
                _rebaseCount++;
            }

            debugMapPosition = MapPosition;
            debugLocalDistance = proxy.transform.position.magnitude;
            debugRebases = _rebaseCount;
        }

        /// <summary>Copia al WorldRoot la transformación inversa de la pose del proxy.</summary>
        private void ApplyInversePose()
        {
            if (worldRoot == null) return;

            // T_w(w) = pose⁻¹ · w: el punto w del mapa aparece en su coordenada
            // relativa al casco, con el interior estático en el origen.
            Quaternion inverseRotation = Quaternion.Inverse(proxy.transform.rotation);
            worldRoot.SetPositionAndRotation(inverseRotation * -proxy.transform.position, inverseRotation);
        }

        /// <summary>Traslada proxy y mundo juntos al origen, sin interpolación y sin tocar MapPosition.</summary>
        private void Rebase(Vector3 offset)
        {
            RigidbodyInterpolation interpolation = _body.interpolation;
            _body.interpolation = RigidbodyInterpolation.None;

            _body.position -= offset;
            _lastLocalPosition -= offset;

            _body.interpolation = interpolation;
        }
    }
}
