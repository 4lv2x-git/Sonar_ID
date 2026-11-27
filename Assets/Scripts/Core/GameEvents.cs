using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SonarCrew.Multiplayer;
using SonarCrew.Stations;
using SonarCrew.Submarine;

namespace SonarCrew.Core
{
    /// <summary>
    /// Eventos globales del juego. Comunicación entre sistemas con eventos C# puros.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Slot de jugador y gamepad que se le ha asignado.</summary>
        public static event Action<int, Gamepad> PlayerDeviceAssigned;
        public static event Action<int> PlayerActivated;
        public static event Action<int> PlayerDeactivated;

        public static event Action<IStation, CrewController> StationOccupied;
        public static event Action<IStation, CrewController> StationReleased;

        /// <summary>Colisión del casco: impulso, punto de contacto, sala y severidad (hito 5).</summary>
        public static event Action<float, Vector3, HullZone, DamageLevel> HullImpacted;
        public static event Action<HullZone> LeakOpened;
        public static event Action<HullZone> LeakClosed;
        public static event Action AlarmRaised;

        /// <summary>Contactos del sónar (hito 3): instantánea inmutable en coordenadas de mapa.</summary>
        public static event Action<IReadOnlyList<SonarContact>> SonarContactsUpdated;

        public static void RaiseHullImpacted(float impulse, Vector3 point, HullZone zone, DamageLevel level)
        {
            HullImpacted?.Invoke(impulse, point, zone, level);
        }

        public static void RaiseLeakOpened(HullZone zone)
        {
            LeakOpened?.Invoke(zone);
        }

        public static void RaiseLeakClosed(HullZone zone)
        {
            LeakClosed?.Invoke(zone);
        }

        public static void RaiseAlarm()
        {
            AlarmRaised?.Invoke();
        }

        public static void RaiseSonarContactsUpdated(IReadOnlyList<SonarContact> contacts)
        {
            SonarContactsUpdated?.Invoke(contacts);
        }

        public static void RaisePlayerDeviceAssigned(int slot, Gamepad pad)
        {
            PlayerDeviceAssigned?.Invoke(slot, pad);
        }

        public static void RaisePlayerActivated(int slot)
        {
            PlayerActivated?.Invoke(slot);
        }

        public static void RaisePlayerDeactivated(int slot)
        {
            PlayerDeactivated?.Invoke(slot);
        }

        public static void RaiseStationOccupied(IStation station, CrewController crew)
        {
            StationOccupied?.Invoke(station, crew);
        }

        public static void RaiseStationReleased(IStation station, CrewController crew)
        {
            StationReleased?.Invoke(station, crew);
        }
    }
}
