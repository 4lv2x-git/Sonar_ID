using UnityEngine;
using SonarCrew.Core;
using SonarCrew.Multiplayer;

namespace SonarCrew.Submarine
{
    /// <summary>
    /// Motor del submarino. Solo física, y solo en FixedUpdate: empuje de palancas
    /// (avance/retroceso), ascenso/descenso, timón (yaw) y arrastre lineal/angular.
    /// La colisión influye en trayectoria y rotación con el impulso del Rigidbody.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SubmarineMotor : MonoBehaviour
    {
        [Header("Configuración (ScriptableObject)")]
        [SerializeField] private SubmarineMotorConfig config;

        [Header("Pilotaje")]
        [Tooltip("Slot cuyo driver pilota mientras el hito 2 no gatee el timón en la estación de mandos.")]
        [SerializeField] private int driverSlot = 0;

        [Header("Confines del mapa")]
        [Tooltip("Recinto del mapa (coordenadas de mapa, no posición local) en el que el submarino queda contenido.")]
        [SerializeField] private Bounds worldBounds = new Bounds(Vector3.zero, new Vector3(300f, 120f, 300f));

        [Header("Sonda de terreno")]
        [Tooltip("Capas que cuentan como terreno para la sonda. Excluir Interior y HullProxy.")]
        [SerializeField] private LayerMask terrainMask = ~0;

        [Header("Depuración (solo lectura)")]
        [SerializeField] private float debugThrust;
        [SerializeField] private float debugVertical;
        [SerializeField] private float debugWheel;
        [SerializeField] private float debugSpeed;

        public float Speed { get; private set; }
        public bool HasPower { get; private set; } = true;

        private Rigidbody _hull;

        private void Awake()
        {
            _hull = GetComponent<Rigidbody>();
            // Cuerpo dinámico sin gravedad; la colisión (CCD) transmite impulso real,
            // ni gravedad externa ni animaciones falsas.
            _hull.useGravity = false;
            _hull.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // Sin interpolación: la pose la copia SubmarineFrame en FixedUpdate y el
            // rebase es un salto sin interpolar (AGENTS.md §3.1); interpolar aquí
            // produciría jitter en el mundo.
            _hull.interpolation = RigidbodyInterpolation.None;
            _hull.freezeRotation = false;
            // El arrastre lo modelamos nosotros desde config; a cero en el Rigidbody.
            _hull.linearDamping = 0f;
            _hull.angularDamping = 0f;

            if (ServiceLocator.TryGet(out IPowerState power))
                HasPower = power.HasPower;
        }

        private void OnEnable()
        {
            if (ServiceLocator.TryGet(out IPowerState power))
            {
                power.PowerChanged += OnPowerChanged;
                HasPower = power.HasPower;
            }
        }

        private void OnDisable()
        {
            if (ServiceLocator.TryGet(out IPowerState power))
                power.PowerChanged -= OnPowerChanged;
        }

        private void OnPowerChanged(bool hasPower)
        {
            HasPower = hasPower;
        }

        private void FixedUpdate()
        {
            float thrust = 0f;
            float vertical = 0f;
            float wheel = 0f;

            if (HasPower)
            {
                if (ServiceLocator.TryGet(out ISubmarineControls controls))
                {
                    // Hito 2: el pilotaje llega de las estaciones de mandos
                    // (timón y palancas) a través del estado de mandos.
                    thrust = controls.Throttle;
                    vertical = controls.Vertical;
                    wheel = controls.Steer;
                }
                else if (ServiceLocator.TryGet(out InputRouter router))
                {
                    // Sin estaciones montadas (pruebas sueltas del hito 1): pilotaje directo.
                    float forward = router.GetThrustForward(driverSlot);
                    float backward = router.GetThrustBackward(driverSlot);
                    thrust = forward - backward;
                    if (router.GetAscendHeld(driverSlot)) vertical += 1f;
                    if (router.GetDescendHeld(driverSlot)) vertical -= 1f;
                    wheel = router.GetWheel(driverSlot);
                }
            }

            ApplyThrust(thrust, vertical);
            ApplyWheel(wheel);
            ApplyDrag();
            ApplyConfines();

            Speed = _hull.linearVelocity.magnitude;
            debugThrust = thrust;
            debugVertical = vertical;
            debugWheel = wheel;
            debugSpeed = Speed;
        }

        private void ApplyThrust(float thrust, float vertical)
        {
            float thrustAccel = config != null ? config.ThrustAcceleration : 8f;
            float verticalAccel = config != null ? config.VerticalAcceleration : 5f;
            float maxHorizontal = config != null ? config.MaxThrustSpeed : 6f;
            float maxVertical = config != null ? config.MaxVerticalSpeed : 3f;
            float dt = Time.fixedDeltaTime;

            Vector3 velocity = _hull.linearVelocity;

            // Palancas: avance/retroceso a lo largo del eje del casco,
            // ascenso/descenso a lo largo de su up.
            velocity += transform.forward * (thrust * thrustAccel * dt);
            velocity += transform.up * (vertical * verticalAccel * dt);

            // Límite aplicado en velocidad, no en fuerza.
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            if (horizontal.magnitude > maxHorizontal)
                horizontal = horizontal.normalized * maxHorizontal;
            velocity.x = horizontal.x;
            velocity.z = horizontal.z;
            velocity.y = Mathf.Clamp(velocity.y, -maxVertical, maxVertical);

            _hull.linearVelocity = velocity;
        }

        private void ApplyWheel(float wheel)
        {
            float wheelTorque = config != null ? config.WheelTorque : 120f;
            float maxDegrees = config != null ? config.MaxAngularSpeed : 45f;
            float dt = Time.fixedDeltaTime;

            // Timón: gira el casco alrededor de SU eje vertical (transform.up),
            // no del eje del mundo: si una colisión cabecea o escora el casco,
            // el yaw debe seguir siendo yaw. Solo se limita la guiñada; los giros
            // que la colisión haya impreso en el resto de ejes se conservan.
            float accelRad = wheel * wheelTorque * Mathf.Deg2Rad * dt;
            float omegaAlong = Vector3.Dot(_hull.angularVelocity, transform.up);
            float omegaMax = maxDegrees * Mathf.Deg2Rad;
            float omegaTarget = Mathf.Clamp(omegaAlong + accelRad, -omegaMax, omegaMax);
            _hull.angularVelocity += transform.up * (omegaTarget - omegaAlong);
        }

        private void ApplyDrag()
        {
            float linearDrag = config != null ? config.LinearDrag : 1.4f;
            float angularDrag = config != null ? config.AngularDrag : 2f;
            float dt = Time.fixedDeltaTime;

            _hull.linearVelocity = _hull.linearVelocity * Mathf.Max(0f, 1f - linearDrag * dt);
            _hull.angularVelocity = _hull.angularVelocity * Mathf.Max(0f, 1f - angularDrag * dt);
        }

        private void ApplyConfines()
        {
            // Confines del mapa en coordenadas de mapa (AGENTS.md §3.1): la posición
            // del transform se rebasa al origen, así que limitar por ella fallaría
            // tras el primer rebase. Sin pose registrada (escena de prueba suelta)
            // se cae a la posición local para no romper el pilotaje.
            Vector3 mapPosition = ServiceLocator.TryGet(out ISubmarinePose pose)
                ? pose.MapPosition
                : _hull.position;

            Vector3 clamped = mapPosition;
            clamped.x = Mathf.Clamp(clamped.x, worldBounds.min.x, worldBounds.max.x);
            clamped.y = Mathf.Clamp(clamped.y, worldBounds.min.y, worldBounds.max.y);
            clamped.z = Mathf.Clamp(clamped.z, worldBounds.min.z, worldBounds.max.z);

            Vector3 velocity = _hull.linearVelocity;
            Vector3 overrun = mapPosition - clamped;
            if (overrun != Vector3.zero)
            {
                // Se devuelve el casco dentro del recinto (a través del Rigidbody,
                // nunca del transform) y se anula la velocidad que apuntaba fuera.
                _hull.position -= overrun;
                if (overrun.x != 0f) velocity.x = 0f;
                if (overrun.y != 0f) velocity.y = 0f;
                if (overrun.z != 0f) velocity.z = 0f;
                _hull.linearVelocity = velocity;
            }

            // Sonda de terreno: Raycast (sin radio, no falla aunque el casco roce
            // el suelo) desde el centro del casco hacia abajo. La urgencia mide la
            // altura real de la panza sobre el suelo: freno nulo a brakeHeight de
            // margen y máximo al rozar. Solo actúa si hay caída: flotar quieto no
            // genera sustento, y un empuje sin caída sería un ascensor invisible.
            float bottomOffset = config != null ? config.HullBottomOffset : 1f;
            float brakeHeight = config != null ? config.TerrainBrakeHeight : 2.5f;
            float brakeDecel = config != null ? config.TerrainBrakeDecel : 10f;
            float maxDescent = config != null ? config.MaxVerticalSpeed : 3f;
            float touchdownSpeed = config != null ? config.TerrainTouchdownSpeed : 1.5f;
            float dt = Time.fixedDeltaTime;

            if (Physics.Raycast(_hull.position, Vector3.down, out RaycastHit hit,
                    bottomOffset + brakeHeight, terrainMask, QueryTriggerInteraction.Ignore))
            {
                float urgency = 1f - Mathf.Clamp01((hit.distance - bottomOffset) / brakeHeight);

                // Limitador de descenso: solo frena el exceso sobre la velocidad
                // permitida a esta distancia; por debajo, el mando desciende libre.
                float allowed = Mathf.Lerp(touchdownSpeed, maxDescent, 1f - urgency);
                float into = Vector3.Dot(velocity, -hit.normal);
                float excess = into - allowed;
                if (excess > 0f)
                {
                    velocity += hit.normal * Mathf.Min(excess, brakeDecel * dt);
                    _hull.linearVelocity = velocity;
                }
            }
        }
    }
}
