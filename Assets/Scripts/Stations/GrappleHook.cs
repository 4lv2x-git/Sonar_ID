using UnityEngine;
using SonarCrew.Core;
using SonarCrew.Environment;

namespace SonarCrew.Stations
{
    /// <summary>
    /// Gancho recolector (hito 4). Es equipo del casco y vive en el marco estático
    /// del interior, igual que el periscopio: cuelga por debajo del suelo y el mundo
    /// se desplaza a su alrededor. La estación de la sala del gancho solo lo acciona
    /// en remoto; el cable, la punta y la cámara de baja calidad están aquí.
    /// Captura el único tesoro del mapa cuando la punta entra en su radio.
    /// </summary>
    public class GrappleHook : MonoBehaviour
    {
        [Header("Geometría")]
        [SerializeField] private Transform cable;
        [SerializeField] private Transform hookHead;
        [SerializeField] private float cableWidth = 0.06f;

        [Header("Cabestrante")]
        [Tooltip("Metros de cable disponibles desde el origen del gancho.")]
        [SerializeField] private float maxDepth = 8f;
        [SerializeField] private float winchSpeed = 2.5f;

        [Header("Captura")]
        [SerializeField] private float captureRadius = 1.2f;
        [Tooltip("Opcional: si se deja vacío se resuelve por el ServiceLocator.")]
        [SerializeField] private TreasureCollectible treasure;

        [Header("Cámara de baja calidad")]
        [SerializeField] private Camera feedCamera;
        [Tooltip("Opcional: si se deja vacío se crea una RenderTexture pequeña al arrancar.")]
        [SerializeField] private RenderTexture feedTexture;
        [Tooltip("Solo mapa y hazards: el interior y el casco nunca entran en la máscara.")]
        [SerializeField] private LayerMask worldMask;

        /// <summary>Resolución de la señal del gancho: deliberadamente mala (README).</summary>
        private const int FeedWidth = 256;
        private const int FeedHeight = 160;

        private RenderTexture _runtimeFeed;
        private float _depth;

        public float Depth => _depth;
        public float MaxDepth => maxDepth;
        public float Depth01 => maxDepth > 0.0001f ? _depth / maxDepth : 0f;

        /// <summary>Imagen que consume la pantalla de la estación del gancho.</summary>
        public RenderTexture FeedTexture => feedTexture != null ? feedTexture : _runtimeFeed;

        public bool FeedEnabled => feedCamera != null && feedCamera.enabled;

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
            EnsureFeed();
            ApplyGeometry();
        }

        private void OnDestroy()
        {
            if (_runtimeFeed == null) return;
            if (feedCamera != null && feedCamera.targetTexture == _runtimeFeed)
                feedCamera.targetTexture = null;
            _runtimeFeed.Release();
            Destroy(_runtimeFeed);
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
        public void SetFeedEnabled(bool enabled)
        {
            if (feedCamera != null)
                feedCamera.enabled = enabled;
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

        private void EnsureFeed()
        {
            if (feedCamera == null) return;

            if (worldMask == 0)
                worldMask = MaskOf("World") | MaskOf("Hazard");

            if (feedTexture == null && _runtimeFeed == null)
            {
                _runtimeFeed = new RenderTexture(FeedWidth, FeedHeight, 16)
                {
                    name = "GrappleFeed",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                _runtimeFeed.Create();
            }

            feedCamera.targetTexture = FeedTexture;
            feedCamera.cullingMask = worldMask;
            feedCamera.allowHDR = false;
            feedCamera.allowMSAA = false;
            feedCamera.enabled = false;
        }

        /// <summary>Máscara de una capa del proyecto; 0 si la capa no existe.</summary>
        private static int MaskOf(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? 1 << layer : 0;
        }
    }
}
