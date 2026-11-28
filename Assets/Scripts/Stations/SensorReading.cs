namespace SonarCrew.Stations
{
    /// <summary>Lectura de los sensores de posición y cota.</summary>
    public struct SensorReading
    {
        public float X;
        public float Z;
        public float Depth;

        /// <summary>Con daño grave los datos son erróneos y aleatorios (README).</summary>
        public bool Imprecise;

        public static SensorReading Empty => new SensorReading { Imprecise = true };
    }
}
