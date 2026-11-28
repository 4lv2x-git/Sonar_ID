namespace SonarCrew.Stations
{
    /// <summary>Motivo por el que un tripulante puede o no ocupar una estación.</summary>
    public enum StationAvailability
    {
        /// <summary>Libre y en servicio.</summary>
        Available = 0,

        /// <summary>Ya la ocupa este mismo tripulante.</summary>
        OccupiedBySelf = 1,

        /// <summary>Otra persona la ocupa: no se puede arrebatar.</summary>
        OccupiedByOther = 2,

        /// <summary>La sala no tiene energía.</summary>
        NoPower = 3,

        /// <summary>El daño grave de la sala deja la estación inutilizable.</summary>
        OutOfService = 4,

        /// <summary>La estación no admite ocupación ahora mismo.</summary>
        Blocked = 5,

        /// <summary>Sin tripulante válido.</summary>
        InvalidCrew = 6
    }
}
