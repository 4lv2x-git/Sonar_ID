using System;
using UnityEngine.InputSystem;
using SonarCrew.Multiplayer;
using SonarCrew.Stations;

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
