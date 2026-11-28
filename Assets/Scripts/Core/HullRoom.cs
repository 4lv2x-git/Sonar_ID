using System;
using UnityEngine;

namespace SonarCrew.Core
{
    /// <summary>
    /// Lo implementa cualquier sistema de una sala que deba reaccionar a su daño
    /// o a la pérdida de energía (las estaciones, por ejemplo).
    /// </summary>
    public interface IRoomListener
    {
        void OnRoomDamageChanged(HullRoom room, DamageLevel level);

        void OnRoomPowerChanged(HullRoom room, bool powered);
    }

    /// <summary>
    /// Estado de una sala del interior estático: nivel de daño y energía disponible,
    /// con aviso a los sistemas que contiene. La severidad la decide quien la aplica
    /// (el <c>DamageSystem</c> del hito 5; el andamiaje de pruebas hasta entonces) y
    /// el <c>PowerSystem</c> es quien informa de la energía.
    /// La identidad de la sala es el enum <see cref="HullZone"/>, el mismo que llevan
    /// los volúmenes de sala del <c>SubmarineProxy</c> de A.
    /// </summary>
    public class HullRoom : MonoBehaviour
    {
        [Header("Sala")]
        [SerializeField] private HullZone zone = HullZone.Mandos;
        [SerializeField] private string displayName = "Sala";
        [Tooltip("Si es false, la sala no depende del suministro eléctrico.")]
        [SerializeField] private bool dependsOnPower = true;

        [Header("Estado inicial (pruebas)")]
        [SerializeField] private DamageLevel damage = DamageLevel.None;
        [SerializeField] private bool poweredAtStart = true;

        private bool _powerAvailable = true;

        public event Action<HullRoom, DamageLevel> DamageChanged;

        public event Action<HullRoom, bool> PowerAvailabilityChanged;

        public HullZone Zone => zone;
        public string DisplayName => displayName;
        public DamageLevel Damage => damage;
        public bool DependsOnPower => dependsOnPower;

        /// <summary>Daño grave o extremo sin reparar: la sala pierde su función.</summary>
        public bool HasSevereDamage => damage >= DamageLevel.Grave;

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
                listener.OnRoomDamageChanged(this, damage);

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
                    listener.OnRoomPowerChanged(this, available);
            }

            PowerAvailabilityChanged?.Invoke(this, available);
        }
    }
}
