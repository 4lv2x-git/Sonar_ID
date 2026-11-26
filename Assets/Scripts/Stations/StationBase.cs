using System;
using UnityEngine;
using SonarCrew.Core;
using SonarCrew.Multiplayer;

namespace SonarCrew.Stations
{
    /// <summary>
    /// Base de todas las estaciones del submarino (Hito 2).
    /// Gestiona dueño único, ocupación/liberación, bloqueo por falta de energía
    /// y el aviso de daño grave de la sala, que cada estación concreta aplica.
    /// </summary>
    public abstract class StationBase : MonoBehaviour, IStation, IRoomListener
    {
        [Header("Estación")]
        [SerializeField] private string stationName = string.Empty;
        [Tooltip("Si necesita energía del suministro eléctrico para funcionar.")]
        [SerializeField] private bool requiresPower = true;
        [Tooltip("Sala a la que pertenece. Si se deja vacío se toma la del padre.")]
        [SerializeField] private HullZone hullZone;
        [SerializeField] private bool showPrompt = true;

        private CrewController _occupant;

        /// <summary>Se lanza cuando cambia el estado visible de la estación.</summary>
        public event Action<StationBase> StateChanged;

        public string StationName => string.IsNullOrEmpty(stationName) ? DefaultStationName : stationName;
        protected virtual string DefaultStationName => "Estación";

        public Transform StationTransform => transform;
        public bool IsOccupied => _occupant != null;
        public CrewController Occupant => _occupant;

        public bool RequiresPower => requiresPower;
        public HullZone Zone => hullZone;
        public bool ShowPrompt => showPrompt;

        /// <summary>La sala suministra energía a esta estación.</summary>
        public bool HasPower => !requiresPower || hullZone == null || hullZone.HasPower;

        /// <summary>La sala tiene daño grave sin reparar.</summary>
        public bool HasSevereDamage => hullZone != null && hullZone.HasSevereDamage;

        /// <summary>Daño que deja la estación completamente inutilizable.</summary>
        protected virtual bool OutOfServiceByDamage => false;

        /// <summary>Puede prestar servicio ahora mismo (energía y daño).</summary>
        public bool IsInService => HasPower && !OutOfServiceByDamage;

        public virtual string StatusText => StationName;

        protected virtual void Awake()
        {
            if (hullZone == null)
                hullZone = GetComponentInParent<HullZone>();
        }

        private void Start()
        {
            // Si la sala empieza dañada, la estación aplica su efecto en el primer frame.
            if (hullZone != null)
                OnRoomDamage(hullZone.Damage);
            OnStationStart();
        }

        /// <summary>Punto de entrada para la inicialización de las estaciones concretas.</summary>
        protected virtual void OnStationStart()
        {
        }

        protected virtual void Update()
        {
            if (_occupant == null) return;

            if (!IsInService)
            {
                Release(_occupant);
                return;
            }

            if (!ServiceLocator.TryGet(out InputRouter router)) return;
            PlayerSlot slot = _occupant.Slot;
            if (slot == null || !slot.IsActive) return;

            OnOperate(router, slot.Index);
        }

        /// <summary>Lee los mandos mientras el tripulante ocupa la estación.</summary>
        protected virtual void OnOperate(InputRouter router, int slotIndex)
        {
        }

        public StationAvailability AvailabilityFor(CrewController crew)
        {
            if (_occupant != null)
                return _occupant == crew ? StationAvailability.OccupiedBySelf : StationAvailability.OccupiedByOther;
            if (crew == null)
                return StationAvailability.InvalidCrew;
            if (!HasPower)
                return StationAvailability.NoPower;
            if (OutOfServiceByDamage)
                return StationAvailability.OutOfService;
            return CanOccupyInternal(crew) ? StationAvailability.Available : StationAvailability.Blocked;
        }

        /// <summary>Condición extra de la estación concreta (por defecto, ninguna).</summary>
        protected virtual bool CanOccupyInternal(CrewController crew) => true;

        public bool CanOccupy(CrewController crew) => AvailabilityFor(crew) == StationAvailability.Available;

        public bool TryOccupy(CrewController crew)
        {
            if (AvailabilityFor(crew) != StationAvailability.Available) return false;
            if (!crew.NotifyOccupied(this)) return false;

            _occupant = crew;
            OnOccupied(crew);
            GameEvents.RaiseStationOccupied(this, crew);
            StateChanged?.Invoke(this);
            return true;
        }

        public void Release(CrewController crew)
        {
            if (_occupant == null) return;
            if (crew != null && crew != _occupant) return;

            CrewController previous = _occupant;
            _occupant = null;

            previous.NotifyReleased(this);
            OnReleased(previous);
            GameEvents.RaiseStationReleased(this, previous);
            StateChanged?.Invoke(this);
        }

        protected virtual void OnOccupied(CrewController crew)
        {
        }

        protected virtual void OnReleased(CrewController crew)
        {
        }

        /// <summary>Efecto del daño de la sala. Cada estación aplica el suyo.</summary>
        protected virtual void OnRoomDamage(DamageLevel level)
        {
        }

        protected virtual void OnPowerChanged(bool powered)
        {
        }

        public void OnZoneDamageChanged(HullZone zone, DamageLevel level)
        {
            OnRoomDamage(level);
            if (_occupant != null && !IsInService)
                Release(_occupant);
            StateChanged?.Invoke(this);
        }

        public void OnZonePowerChanged(HullZone zone, bool powered)
        {
            OnPowerChanged(powered);
            if (_occupant != null && !IsInService)
                Release(_occupant);
            StateChanged?.Invoke(this);
        }

        /// <summary>Permite a un sistema externo inyectar la sala (pruebas).</summary>
        public void SetHullZone(HullZone zone) => hullZone = zone;

        /// <summary>Permite a una subclase fijar si necesita energía.</summary>
        protected void ConfigurePowerRequirement(bool required) => requiresPower = required;
    }
}
