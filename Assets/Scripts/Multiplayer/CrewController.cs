using UnityEngine;
using SonarCrew.Core;
using SonarCrew.Stations;

namespace SonarCrew.Multiplayer
{
    /// <summary>
    /// Movimiento en primera persona del tripulante dentro del casco.
    /// Detecta la estación más cercana, ocupa/libera con Interact y libera
    /// al alejarse. Vive dentro del interior estático del casco: no hereda
    /// ninguna pose móvil (lo que se desplaza por el mapa es la cápsula del proxy).
    /// Al operar una estación el stick izquierdo sigue moviendo al tripulante
    /// (para poder alejarse y soltarla) y el derecho pasa al mando de la estación.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CrewController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Transform cameraPivot;

        [Header("Movimiento")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float sprintMultiplier = 1.6f;
        [SerializeField] private float jumpSpeed = 4.5f;
        [SerializeField] private float gravity = -12f;

        [Header("Cámara")]
        [SerializeField] private float lookSensitivity = 90f;
        [SerializeField] private float maxPitch = 85f;

        [Header("Interacción con estaciones")]
        [SerializeField] private float interactRadius = 2.2f;
        [SerializeField] private float releaseRadius = 3.2f;
        [SerializeField] private LayerMask stationLayer = ~0;

        /// <summary>Paso máximo por frame: evita saltos grandes tras un hitch.</summary>
        private const float MaxFrameStep = 0.05f;

        /// <summary>Cadencia del escaneo de estaciones cercanas (no hace falta por frame).</summary>
        private const float StationScanInterval = 0.15f;

        private readonly Collider[] _stationHits = new Collider[24];
        private float _scanTimer;

        private CharacterController _controller;
        private PlayerSlot _slot;
        private Camera _crewCamera;
        private float _yaw;
        private float _pitch;
        private float _verticalVelocity;

        public PlayerSlot Slot => _slot;

        /// <summary>Cámara en primera persona del slot (vive en la escena de A: solo se lee).</summary>
        public Camera CrewCamera
        {
            get
            {
                if (_crewCamera == null)
                    _crewCamera = GetComponentInChildren<Camera>(true);
                return _crewCamera;
            }
        }

        /// <summary>Estación más cercana, ocupada o no (el HUD explica el motivo).</summary>
        public IStation CurrentStation { get; private set; }

        /// <summary>Estación que este tripulante está operando, si hay alguna.</summary>
        public IStation OccupiedStation { get; private set; }

        public bool IsOperatingStation => OccupiedStation != null;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _yaw = transform.eulerAngles.y;
        }

        public void Bind(PlayerSlot slot)
        {
            _slot = slot;
        }

        private void Update()
        {
            if (_slot == null) return;

            if (!_slot.IsActive)
            {
                // Slot desactivado (gamepad desconectado): su estación no puede
                // quedarse bloqueada para el resto de la tripulación.
                if (OccupiedStation != null)
                    OccupiedStation.Release(this);
                return;
            }

            if (!ServiceLocator.TryGet(out InputRouter router)) return;

            float dt = Mathf.Min(Time.deltaTime, MaxFrameStep);

            // Operando una estación la mirada pasa al mando de esa estación,
            // pero el movimiento sigue disponible para poder alejarse y soltarla.
            if (!IsOperatingStation)
                HandleLook(router, dt);

            HandleMove(router, dt);
            HandleStations(router, dt);
        }

        private void HandleLook(InputRouter router, float dt)
        {
            Vector2 look = router.GetLook(_slot.Index);
            _yaw += look.x * lookSensitivity * dt;
            _pitch -= look.y * lookSensitivity * dt;
            _pitch = Mathf.Clamp(_pitch, -maxPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void HandleMove(InputRouter router, float dt)
        {
            Vector2 move = router.GetMove(_slot.Index);
            bool sprint = router.GetSprintHeld(_slot.Index);
            float speed = moveSpeed * (sprint ? sprintMultiplier : 1f);

            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = transform.right;
            right.y = 0f;
            right.Normalize();
            Vector3 wishDir = forward * move.y + right * move.x;
            if (wishDir.sqrMagnitude > 1f)
                wishDir.Normalize();

            if (_controller.isGrounded)
            {
                _verticalVelocity = -0.5f;
                if (router.GetJumpPressed(_slot.Index))
                    _verticalVelocity = jumpSpeed;
            }
            else
            {
                _verticalVelocity += gravity * dt;
            }

            Vector3 motion = wishDir * speed + Vector3.up * _verticalVelocity;
            _controller.Move(motion * dt);
        }

        private void HandleStations(InputRouter router, float dt)
        {
            RefreshNearestStation(dt);

            if (router.GetInteractPressed(_slot.Index))
            {
                if (OccupiedStation != null)
                    OccupiedStation.Release(this);
                else if (CurrentStation != null)
                    CurrentStation.TryOccupy(this);
            }

            if (OccupiedStation != null)
            {
                float distance = Vector3.Distance(transform.position, OccupiedStation.StationTransform.position);
                if (distance > releaseRadius)
                    OccupiedStation.Release(this);
            }
        }

        /// <summary>Escanea a ~6 Hz: localizar la estación cercana no necesita cadencia de frame.</summary>
        private void RefreshNearestStation(float dt)
        {
            _scanTimer -= dt;
            if (_scanTimer > 0f) return;
            _scanTimer = StationScanInterval;
            CurrentStation = FindNearestStation();
        }

        private IStation FindNearestStation()
        {
            Vector3 eye = transform.position + Vector3.up * 0.9f;
            IStation best = null;
            float bestSqr = interactRadius * interactRadius;

            int count = Physics.OverlapSphereNonAlloc(eye, interactRadius, _stationHits, stationLayer, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider hit = _stationHits[i];
                _stationHits[i] = null;
                if (hit == null) continue;

                IStation station = hit.GetComponentInParent<IStation>();
                if (station == null) continue;

                float sqr = (station.StationTransform.position - eye).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = station;
                }
            }

            return best;
        }

        /// <summary>Lo llama la estación cuando este tripulante la ocupa. Devuelve false si ya ocupaba otra.</summary>
        public bool NotifyOccupied(IStation station)
        {
            if (station == null || OccupiedStation != null) return false;
            OccupiedStation = station;
            return true;
        }

        /// <summary>Lo llama la estación cuando este tripulante la libera.</summary>
        public void NotifyReleased(IStation station)
        {
            if (OccupiedStation == station)
                OccupiedStation = null;
        }
    }
}
