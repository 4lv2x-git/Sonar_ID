namespace SonarCrew.Submarine
{
    /// <summary>
    /// Mandos del submarino que escriben las estaciones y que leerá el
    /// <c>SubmarineMotor</c> del Hito 1. Aislar la entrada detrás de una
    /// interfaz permite montar el motor (u otro sistema) sin tocar estaciones.
    /// </summary>
    public interface ISubmarineControls
    {
        /// <summary>Giro del timón normalizado en [-1, 1].</summary>
        float Steer { get; }

        /// <summary>Avance/retroceso normalizado en [-1, 1].</summary>
        float Throttle { get; }

        /// <summary>Ascenso/descenso normalizado en [-1, 1].</summary>
        float Vertical { get; }

        /// <summary>Motor en marcha (el paro completo lo deja en false).</summary>
        bool EngineRunning { get; }

        /// <summary>Timón bloqueado por daño grave en la sala de mandos.</summary>
        bool WheelLocked { get; }

        void SetSteer(float value);
        void SetThrottle(float value);
        void SetVertical(float value);
        void SetEngineRunning(bool running);
        void SetWheelLocked(bool locked);
    }
}
