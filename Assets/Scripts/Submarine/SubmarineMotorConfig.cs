using UnityEngine;

namespace SonarCrew.Submarine
{
    /// <summary>
    /// Balance numérico del motor (AGENTS.md §7: configuración en ScriptableObject,
    /// nunca literal repetido). Crear el asset desde Create > SonarCrew > Submarine Motor Config.
    /// </summary>
    [CreateAssetMenu(fileName = "SubmarineMotorConfig", menuName = "SonarCrew/Submarine Motor Config")]
    public class SubmarineMotorConfig : ScriptableObject
    {
        [Header("Empuje (palancas)")]
        [Tooltip("Aceleración de avance/retroceso (m/s²).")]
        [SerializeField] private float thrustAcceleration = 8f;
        [Tooltip("Aceleración de ascenso/descenso (m/s²).")]
        [SerializeField] private float verticalAcceleration = 5f;
        [Tooltip("Velocidad máxima de avance (m/s).")]
        [SerializeField] private float maxThrustSpeed = 6f;
        [Tooltip("Velocidad máxima vertical (m/s).")]
        [SerializeField] private float maxVerticalSpeed = 3f;

        [Header("Timón")]
        [Tooltip("Aceleración angular del timón (grados/s²).")]
        [SerializeField] private float wheelTorque = 120f;
        [Tooltip("Velocidad angular máxima del timón (grados/s).")]
        [SerializeField] private float maxAngularSpeed = 45f;

        [Header("Arrastre")]
        [Tooltip("Arrastre lineal, proporcional a la velocidad.")]
        [SerializeField] private float linearDrag = 1.4f;
        [Tooltip("Arrastre angular.")]
        [SerializeField] private float angularDrag = 2f;

        [Header("Sonda de terreno (hito 1: no atravesar el suelo)")]
        [Tooltip("Distancia del centro del casco a su punto más bajo (m). Ajustar al collider del submarino.")]
        [SerializeField] private float hullBottomOffset = 1f;
        [Tooltip("Margen de frenado bajo la panza (m): el freno nace a esa altura del suelo y es máximo al rozarlo.")]
        [SerializeField] private float terrainBrakeHeight = 2.5f;
        [Tooltip("Desaceleración máxima del freno tocando el suelo (m/s²).")]
        [SerializeField] private float terrainBrakeDecel = 10f;
        [Tooltip("Velocidad de descenso permitida justo al tocar el suelo (m/s).")]
        [SerializeField] private float terrainTouchdownSpeed = 1.5f;

        public float ThrustAcceleration => thrustAcceleration;
        public float VerticalAcceleration => verticalAcceleration;
        public float MaxThrustSpeed => maxThrustSpeed;
        public float MaxVerticalSpeed => maxVerticalSpeed;
        public float WheelTorque => wheelTorque;
        public float MaxAngularSpeed => maxAngularSpeed;
        public float LinearDrag => linearDrag;
        public float AngularDrag => angularDrag;
        public float HullBottomOffset => hullBottomOffset;
        public float TerrainBrakeHeight => terrainBrakeHeight;
        public float TerrainBrakeDecel => terrainBrakeDecel;
        public float TerrainTouchdownSpeed => terrainTouchdownSpeed;
    }
}
