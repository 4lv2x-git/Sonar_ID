using UnityEngine;
using SonarCrew.Multiplayer;

namespace SonarCrew.Stations
{
    /// <summary>
    /// Contrato de una estación del submarino (timón, sónar, gancho...).
    /// Una estación solo puede ser ocupada por un tripulante a la vez.
    /// </summary>
    public interface IStation
    {
        string StationName { get; }
        Transform StationTransform { get; }
        bool IsOccupied { get; }
        CrewController Occupant { get; }

        /// <summary>Texto de estado que la estación muestra a su ocupante.</summary>
        string StatusText { get; }

        /// <summary>Motivo por el que el tripulante puede o no ocuparla.</summary>
        StationAvailability AvailabilityFor(CrewController crew);

        /// <summary>Puede el tripulante ocuparla ahora (libre, sala sin daño grave, con energía...).</summary>
        bool CanOccupy(CrewController crew);

        /// <summary>Intenta ocuparla. Devuelve true si el tripulante pasa a ocuparla.</summary>
        bool TryOccupy(CrewController crew);

        /// <summary>Libera la estación si el tripulante la ocupaba.</summary>
        void Release(CrewController crew);
    }
}
