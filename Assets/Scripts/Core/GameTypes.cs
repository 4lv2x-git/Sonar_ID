using System;
using UnityEngine;

namespace SonarCrew.Core
{
    /// <summary>
    /// Nivel de daño de una sala, tal y como lo define el README:
    /// leve (solo alarma), grave (fuga + pérdida de función) y extremo (derrota).
    /// </summary>
    public enum DamageLevel
    {
        None = 0,
        Minor = 1,
        Severe = 2,
        Extreme = 3
    }

    /// <summary>Las cinco salas obligatorias del submarino (AGENTS §4).</summary>
    public enum RoomId
    {
        ControlRoom = 0,
        SensorRoom = 1,
        EngineRoom = 2,
        InfoRoom = 3,
        GrappleRoom = 4
    }

    /// <summary>
    /// Lo implementa cualquier sistema de una sala que deba reaccionar a su daño
    /// o a la pérdida de energía (las estaciones, por ejemplo).
    /// </summary>
    public interface IRoomListener
    {
        void OnZoneDamageChanged(HullZone zone, DamageLevel level);

        void OnZonePowerChanged(HullZone zone, bool powered);
    }

    /// <summary>
    /// Una sala del casco: guarda su nivel de daño y si recibe energía, y avisa a
    /// los sistemas que contiene. La severidad la decide quien la aplica (el
    /// DamageSystem en el hito 5; el andamiaje de pruebas hasta entonces) y el
    /// PowerSystem es quien informa de la energía disponible.
    /// </summary>
    public class HullZone : MonoBehaviour
    {
        [Header("Sala")]
        [SerializeField] private RoomId roomId = RoomId.ControlRoom;
        [SerializeField] private string displayName = "Sala";
        [Tooltip("Si es false, la sala no depende del suministro eléctrico.")]
        [SerializeField] private bool dependsOnPower = true;

        [Header("Estado inicial (pruebas)")]
        [SerializeField] private DamageLevel damage = DamageLevel.None;
        [SerializeField] private bool poweredAtStart = true;

        private bool _powerAvailable = true;

        public event Action<HullZone, DamageLevel> DamageChanged;

        public event Action<HullZone, bool> PowerAvailabilityChanged;

        public RoomId RoomId => roomId;
        public string DisplayName => displayName;
        public DamageLevel Damage => damage;
        public bool DependsOnPower => dependsOnPower;
        public bool HasSevereDamage => damage >= DamageLevel.Severe;

        /// <summary>Energía disponible para esta sala.</summary>
        public bool HasPower => !dependsOnPower || _powerAvailable;

        private void Awake()
        {
            _powerAvailable = poweredAtStart;
        }

        /// <summary>Fija el daño de la sala y avisa a los sistemas afectados.</summary>
        public void SetDamage(DamageLevel level)
        {
            if (damage == level) return;
            damage = level;

            foreach (IRoomListener listener in GetComponentsInChildren<IRoomListener>(true))
                listener.OnZoneDamageChanged(this, damage);

            DamageChanged?.Invoke(this, damage);
        }

        /// <summary>Reparar con chapa deja la sala como nueva (hito 5).</summary>
        public void Repair() => SetDamage(DamageLevel.None);

        /// <summary>El PowerSystem informa a la sala de la energía disponible.</summary>
        public void SetPowerAvailable(bool available)
        {
            if (_powerAvailable == available) return;
            _powerAvailable = available;

            if (dependsOnPower)
            {
                foreach (IRoomListener listener in GetComponentsInChildren<IRoomListener>(true))
                    listener.OnZonePowerChanged(this, available);
            }

            PowerAvailabilityChanged?.Invoke(this, available);
        }
    }
}
