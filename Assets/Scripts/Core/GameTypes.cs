namespace SonarCrew.Core
{
    /// <summary>Salas del submarino (§4 de AGENTS.md).</summary>
    public enum HullZone
    {
        Mandos,
        Sensores,
        Maquinas,
        Informacion,
        Gancho
    }

    /// <summary>
    /// Severidad del daño del casco (§5 de AGENTS.md).
    /// <see cref="None"/> es la sala sana o reparada con chapa.
    /// </summary>
    public enum DamageLevel
    {
        None = 0,
        Leve = 1,
        Grave = 2,
        Extremo = 3
    }
}
