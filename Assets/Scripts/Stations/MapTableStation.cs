using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SonarCrew.Core;
using SonarCrew.Multiplayer;

namespace SonarCrew.Stations
{
    /// <summary>
    /// Mapa en papel y documentación del submarino (sala de información).
    /// Con daño grave muestra daños falsos, la información deja de ser fiable y
    /// suenan alarmas de vez en cuando (README).
    /// </summary>
    public class MapTableStation : StationBase
    {
        [Header("Mapa")]
        [Tooltip("Salas que el mapa puede reportar. Si queda vacío se buscan al iniciar.")]
        [SerializeField] private HullRoom[] knownRooms;
        [SerializeField] private float falseAlarmInterval = 7f;

        private readonly List<HullRoom> _reportedDamaged = new List<HullRoom>();
        private float _nextFalseAlarmTime;
        private bool _roomsResolved;

        protected override string DefaultStationName => "Mapa y documentación";

        public IReadOnlyList<HullRoom> ReportedDamaged => _reportedDamaged;

        public override string OperateHint => "[X] informe de posición · [Y] soltar o aléjate";

        public override string StatusText
        {
            get
            {
                if (!HasPower) return "MAPA SIN ENERGÍA";
                if (HasSevereDamage)
                    return "INFORMACIÓN NO FIABLE · " + _reportedDamaged.Count + " daños reportados";

                StringBuilder builder = new StringBuilder("Mapa en papel · ");
                builder.Append(knownRooms != null ? knownRooms.Length : 0);
                builder.Append(" salas registradas");
                return builder.ToString();
            }
        }

        protected override void OnStationStart()
        {
            RefreshReport();
        }

        protected override void OnOccupied(CrewController crew) => RefreshReport();

        protected override void OnOperate(InputRouter router, int slotIndex)
        {
            if (router.GetAttackPressed(slotIndex))
                RefreshReport();
        }

        protected override void Update()
        {
            base.Update();

            if (!IsOccupied || !HasSevereDamage) return;
            if (Time.time < _nextFalseAlarmTime) return;

            _nextFalseAlarmTime = Time.time + falseAlarmInterval + Random.Range(0f, 3f);
            RefreshReport();
            GameEvents.RaiseAlarm();
        }

        /// <summary>Recalcula lo que muestra el mapa: real o con daños falsos.</summary>
        public void RefreshReport()
        {
            _reportedDamaged.Clear();
            ResolveRooms();
            if (knownRooms == null) return;

            if (!HasSevereDamage) return;

            foreach (HullRoom room in knownRooms)
            {
                if (room == null) continue;
                if (Random.value < 0.4f)
                    _reportedDamaged.Add(room);
            }
        }

        /// <summary>Las salas se enlazan en el primer refresco si el prefab no las trae.</summary>
        private void ResolveRooms()
        {
            if (_roomsResolved) return;
            _roomsResolved = true;

            if (knownRooms == null || knownRooms.Length == 0)
                knownRooms = Object.FindObjectsByType<HullRoom>();
        }
    }
}
