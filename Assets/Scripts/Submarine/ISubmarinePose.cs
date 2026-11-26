using UnityEngine;

namespace SonarCrew.Submarine
{
    /// <summary>
    /// Pose del submarino en el mapa (AGENTS.md §3.1). Es la única API que el resto de
    /// sistemas puede usar para saber dónde está y cómo se mueve el casco: leer el
    /// transform del proxy está prohibido porque la posición local se rebasa.
    /// La implementa SubmarineFrame, que es la única autoridad del WorldRoot.
    /// </summary>
    public interface ISubmarinePose
    {
        /// <summary>Coordenada de mapa del casco: acumula el recorrido y nunca se rebasa.</summary>
        Vector3 MapPosition { get; }

        /// <summary>Rumbo en grados sobre el plano del mapa (0 = +Z).</summary>
        float Yaw { get; }

        /// <summary>Velocidad lineal actual del casco (m/s) en ejes del mapa.</summary>
        Vector3 Velocity { get; }

        /// <summary>Punto del mundo físico → marco del casco (relativo al proxy, girado con él).</summary>
        Vector3 WorldToSubmarine(Vector3 worldPoint);

        /// <summary>Marco del casco → punto del mundo físico. Inversa de WorldToSubmarine.</summary>
        Vector3 SubmarineToWorld(Vector3 submarinePoint);
    }
}
