using UnityEngine;
using SonarCrew.Core;
using SonarCrew.Environment;

namespace SonarCrew.Stations
{
    /// <summary>
    /// Gancho recolector (hito 4). Es equipo del casco y vive en el marco estático
    /// del interior, como el periscopio: cuelga por debajo del suelo y el mundo se
    /// desplaza a su alrededor. La estación de la sala del gancho lo acciona en
    /// remoto y cede su vista al ocupante a través de <see cref="ViewCamera"/>.
    /// Captura el único tesoro del mapa cuando la punta entra en su radio.
    /// </summary>
    public class GrappleHook : MonoBehaviour
    {
        [Header("Geometría")]
        [SerializeField] private Transform cable;
        [SerializeField] private Transform hookHead;
        [SerializeField] private float cableWidth = 0.1f;

        [Header("Cabestrante")]
        [Tooltip("Metros de cable disponibles desde el origen del gancho.")]
        [SerializeField] private float maxDepth = 7.5f;
        [SerializeField] private float winchSpeed = 2.5f;

        [Header("Captura")]
        [SerializeField] private float captureRadius = 1.2f;
        [Tooltip("Opcional: si se deja vacío se resuelve por el ServiceLocator.")]
        [SerializeField] private TreasureCollectible treasure;

        [Header("Cámara")]
        [Tooltip("Vista fija bajo el casco: muestra el cable, la punta y el fondo.")]
        [SerializeField] private Camera viewCamera;
        [Tooltip("Mapa, hazards y el propio gancho. El interior nunca entra en la máscara.")]
        [SerializeField] private LayerMask worldMask;

        private float _depth;

        public float Depth => _depth;
        public float MaxDepth => maxDepth;
        public float Depth01 => maxDepth > 0.0001f ? _depth / maxDepth : 0f;

        public Camera ViewCamera => viewCamera;

        public bool CameraEnabled => viewCamera != null && viewCamera.enabled;

        private TreasureCollectible Treasure
        {
            get
            {
                if (treasure == null)
                    ServiceLocator.TryGet(out treasure);
                return treasure;
            }
        }

        private void Awake()
        {
            // Equipo de la estación: se resuelve por el localizador, como las estaciones.
            ServiceLocator.Register(this);
            ApplyGeometry();

            // La cámara la enciende la estación mientras su ocupante mira el gancho.
            if (viewCamera == null) return;

            int hullLayer = MaskOf("HullProxy");
            if (worldMask == 0)
                worldMask = MaskOf("World") | MaskOf("Hazard") | hullLayer;
            else
                worldMask |= hullLayer; // el propio gancho siempre se ve en su cámara

            viewCamera.cullingMask = worldMask;
            viewCamera.allowHDR = false;
            viewCamera.allowMSAA = false;
            viewCamera.enabled = false;
        }

        private void Update()
        {
            UpdateCapture();
        }

        /// <summary>
        /// Suelta cable con entrada positiva y lo enrolla con negativa. La velocidad
        /// la fija el cabestrante; el mando de la estación decide la dirección.
        /// </summary>
        public void Drive(float input)
        {
            if (Mathf.Approximately(input, 0f)) return;
            SetDepth(_depth + input * winchSpeed * Time.deltaTime);
        }

        public void SetDepth(float depth)
        {
            _depth = Mathf.Clamp(depth, 0f, maxDepth);
            ApplyGeometry();
        }

        /// <summary>Retrae el cable de golpe (daño grave de la sala).</summary>
        public void Retract() => SetDepth(0f);

        /// <summary>Enciende o apaga la cámara: solo se renderiza mientras alguien mira.</summary>
        public void SetCameraEnabled(bool enabled)
        {
            if (viewCamera != null)
                viewCamera.enabled = enabled;
        }

        /// <summary>
        /// Distancia de la punta al tesoro en metros. Falso si ya no queda tesoro o
        /// si el gancho todavía no tiene punta.
        /// </summary>
        public bool TryGetTreasureDistance(out float meters)
        {
            meters = 0f;
            TreasureCollectible target = Treasure;
            if (target == null || target.IsRecovered || hookHead == null)
                return false;

            meters = Vector3.Distance(hookHead.position, target.transform.position);
            return true;
        }

        private void UpdateCapture()
        {
            if (_depth <= 0.001f || hookHead == null) return;

            TreasureCollectible target = Treasure;
            if (target == null || target.IsRecovered) return;
            if ((hookHead.position - target.transform.position).sqrMagnitude > captureRadius * captureRadius) return;

            if (target.TryRecover())
                target.AttachTo(hookHead);
        }

        /// <summary>Coloca cable y punta según la profundidad actual.</summary>
        private void ApplyGeometry()
        {
            if (cable != null)
            {
                cable.localPosition = new Vector3(0f, -_depth * 0.5f, 0f);
                cable.localScale = new Vector3(cableWidth, Mathf.Max(_depth, 0.001f), cableWidth);
            }

            if (hookHead != null)
                hookHead.localPosition = new Vector3(0f, -_depth, 0f);
        }

        /// <summary>Máscara de una capa del proyecto; 0 si la capa no existe.</summary>
        private static int MaskOf(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? 1 << layer : 0;
        }
    }
}
