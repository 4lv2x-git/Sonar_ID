using System;

namespace SonarCrew.Submarine
{
    /// <summary>
    /// Suministro eléctrico del submarino (lo implementa PowerSystem, hito 2).
    /// Hito 1 usa PowerStateStub ("siempre con energía") hasta que llegue el real.
    /// </summary>
    public interface IPowerState
    {
        /// <summary>True si el suministro eléctrico está disponible.</summary>
        bool HasPower { get; }

        /// <summary>Se lanza al cambiar la disponibilidad (fallo eléctrico de la sala de máquinas, hito 5).</summary>
        event Action<bool> PowerChanged;
    }
}
