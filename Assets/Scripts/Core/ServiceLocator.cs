using System;
using System.Collections.Generic;

namespace SonarCrew.Core
{
    /// <summary>
    /// Localizador de servicios mínimo para desacoplar sistemas.
    /// Cada sistema se registra en Awake y se consulta por tipo.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) return;
            Services[typeof(T)] = service;
        }

        public static T Get<T>() where T : class
        {
            if (Services.TryGetValue(typeof(T), out object service))
                return (T)service;
            throw new InvalidOperationException("Servicio no registrado: " + typeof(T).Name);
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out object found))
            {
                service = (T)found;
                return true;
            }
            service = null;
            return false;
        }

        public static void Clear()
        {
            Services.Clear();
        }
    }
}
