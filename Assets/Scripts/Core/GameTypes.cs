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

    /// <summary>Severidad del daño del casco (§5 de AGENTS.md).</summary>
    public enum DamageLevel
    {
        Leve,
        Grave,
        Extremo
    }
}
