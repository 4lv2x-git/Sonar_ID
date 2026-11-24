using UnityEngine;
using SonarCrew.Core;
using SonarCrew.Stations;

namespace SonarCrew.Multiplayer
{
    /// <summary>
    /// Movimiento en primera persona del tripulante dentro del casco.
    /// Detecta la estación más cercana, ocupa/libera con Interact y libera
    /// al alejarse. Vive como hijo del transform del submarino (movimiento relativo al casco).
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

        private CharacterController _controller;
        private PlayerSlot _slot;
        private float _yaw;
        private float _pitch;
        private float _verticalVelocity;

        public PlayerSlot Slot => _slot;
        public IStation CurrentStation { get; private set; }
        public IStation OccupiedStation { get; private set; }

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
            if (_slot == null || !_slot.IsActive) return;
            if (!ServiceLocator.TryGet(out InputRouter router)) return;

            HandleLook(router);
            HandleMove(router);
            HandleStations(router);
        }

        private void HandleLook(InputRouter router)
        {
            Vector2 look = router.GetLook(_slot.Index);
            _yaw += look.x * lookSensitivity * Time.deltaTime;
            _pitch -= look.y * lookSensitivity * Time.deltaTime;
            _pitch = Mathf.Clamp(_pitch, -maxPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void HandleMove(InputRouter router)
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
                _verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 motion = wishDir * speed + Vector3.up * _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }

        private void HandleStations(InputRouter router)
        {
            Vector3 eye = transform.position + Vector3.up * 0.9f;
            IStation best = null;
            float bestSqr = interactRadius * interactRadius;
            Collider[] hits = Physics.OverlapSphere(eye, interactRadius, stationLayer);
            foreach (Collider hit in hits)
            {
                IStation station = hit.GetComponentInParent<IStation>();
                if (station == null) continue;
                if (station.IsOccupied && station.Occupant != this) continue;
                float sqr = (station.StationTransform.position - eye).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = station;
                }
            }
            CurrentStation = best;

            if (router.GetInteractPressed(_slot.Index))
            {
                if (OccupiedStation != null)
                    OccupiedStation.Release(this);
                else if (best != null)
                    best.TryOccupy(this);
            }

            if (OccupiedStation != null)
            {
                float dist = Vector3.Distance(transform.position, OccupiedStation.StationTransform.position);
                if (dist > releaseRadius)
                    OccupiedStation.Release(this);
            }
        }

        /// <summary>Lo llama la estación cuando este tripulante la ocupa. Devuelve false si ya ocupaba otra.</summary>
        public bool NotifyOccupied(IStation station)
        {
            if (OccupiedStation != null || station == null) return false;
            OccupiedStation = station;
            GameEvents.RaiseStationOccupied(station, this);
            return true;
        }

        /// <summary>Lo llama la estación cuando este tripulante la libera.</summary>
        public void NotifyReleased(IStation station)
        {
            if (OccupiedStation == station)
            {
                OccupiedStation = null;
                GameEvents.RaiseStationReleased(station, this);
            }
        }
    }
}
