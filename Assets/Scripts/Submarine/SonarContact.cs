using UnityEngine;

namespace SonarCrew.Submarine
{
    /// <summary>Tipo de contacto del sónar (hito 3).</summary>
    public enum SonarContactKind
    {
        Terrain,
        Hazard
    }

    /// <summary>
    /// Contacto del sónar en coordenadas de mapa (AGENTS.md §3.1): estable frente al
    /// rebase. La lista que emite SonarSystem es una instantánea inmutable por emisión.
    /// </summary>
    public readonly struct SonarContact
    {
        /// <summary>Posición del contacto en coordenadas de mapa.</summary>
        public readonly Vector3 MapPosition;

        /// <summary>Distancia del contacto al casco en el momento del ping (m).</summary>
        public readonly float Distance;

        /// <summary>Terreno o hazard.</summary>
        public readonly SonarContactKind Kind;

        public SonarContact(Vector3 mapPosition, float distance, SonarContactKind kind)
        {
            MapPosition = mapPosition;
            Distance = distance;
            Kind = kind;
        }
    }
}
