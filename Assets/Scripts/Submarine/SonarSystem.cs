using System.Collections.Generic;
using UnityEngine;
using SonarCrew.Core;

namespace SonarCrew.Submarine
{
    /// <summary>
    /// Sónar de terreno y hazards (hito 3, AGENTS.md §5): OverlapSphere desde la pose
    /// del proxy sobre las capas World/Hazard (nunca Interior), gateado por energía.
    /// Emite GameEvents.SonarContactsUpdated con una instantánea inmutable de contactos
    /// en coordenadas de mapa. El HUD y los blips del mapa son de la persona B (6b).
    /// </summary>
    [RequireComponent(typeof(SubmarineFrame))]
    public class SonarSystem : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private SubmarineFrame frame;
        [SerializeField] private SonarConfig config;

        [Header("Capas")]
        [Tooltip("Capas que detecta el sónar. Solo World y Hazard; nunca Interior.")]
        [SerializeField] private LayerMask contactMask = ~0;
        [Tooltip("Subconjunto de contactMask que se reporta como hazard (minas, etc.).")]
        [SerializeField] private LayerMask hazardMask = ~0;

        [Header("Depuración (solo lectura)")]
        [SerializeField] private int debugContactCount;
        [SerializeField] private float debugTimeSincePing;
        [SerializeField] private SonarContactKind debugLastKind;
        [SerializeField] private Vector3 debugLastMapPosition;

        private readonly Collider[] _overlaps = new Collider[64];
        private readonly List<SonarContact> _contacts = new List<SonarContact>(64);
        private readonly HashSet<Transform> _seen = new HashSet<Transform>();
        private float _timer;

        private SubmarineFrame Frame => frame != null ? frame : (frame = GetComponent<SubmarineFrame>());

        private bool HasPower => !ServiceLocator.TryGet(out IPowerState power) || power.HasPower;

        private void FixedUpdate()
        {
            _timer += Time.fixedDeltaTime;
            debugTimeSincePing = _timer;

            float interval = config != null ? Mathf.Max(0.1f, config.PingInterval) : 1.5f;
            if (_timer < interval) return;

            _timer = 0f;
            // Sin energía no hay ping: la sala de sensores depende del suministro
            // (el daño de la sala lo gatea el DamageSystem de la persona B).
            if (HasPower) Ping();
        }

        private void Ping()
        {
            SubmarineFrame currentFrame = Frame;
            if (currentFrame == null) return;

            float range = config != null ? config.Range : 120f;
            int maxContacts = config != null ? Mathf.Max(1, config.MaxContacts) : 64;

            Vector3 origin = currentFrame.transform.position;
            int hits = Physics.OverlapSphereNonAlloc(origin, range, _overlaps, contactMask,
                QueryTriggerInteraction.Ignore);

            _contacts.Clear();
            _seen.Clear();
            for (int i = 0; i < hits && _contacts.Count < maxContacts; i++)
            {
                Collider hit = _overlaps[i];
                if (hit == null) continue;
                // Un contacto por objeto aunque tenga varios colliders.
                if (!_seen.Add(hit.transform)) continue;

                Vector3 worldCenter = hit.bounds.center;
                SonarContactKind kind = IsHazard(hit.gameObject.layer)
                    ? SonarContactKind.Hazard
                    : SonarContactKind.Terrain;
                _contacts.Add(new SonarContact(currentFrame.WorldToMap(worldCenter),
                    Vector3.Distance(origin, worldCenter), kind));
            }

            debugContactCount = _contacts.Count;
            if (_contacts.Count > 0)
            {
                debugLastKind = _contacts[0].Kind;
                debugLastMapPosition = _contacts[0].MapPosition;
            }

            // Instantánea inmutable: la lista interna se reutiliza en el siguiente ping.
            GameEvents.RaiseSonarContactsUpdated(_contacts.ToArray());
        }

        private bool IsHazard(int layer)
        {
            return (hazardMask.value & (1 << layer)) != 0;
        }

        private void OnDrawGizmosSelected()
        {
            SubmarineFrame currentFrame = Frame;
            if (currentFrame == null) return;

            Gizmos.color = new Color(0f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(currentFrame.transform.position, config != null ? config.Range : 120f);

            for (int i = 0; i < _contacts.Count; i++)
            {
                SonarContact contact = _contacts[i];
                Gizmos.color = contact.Kind == SonarContactKind.Hazard
                    ? new Color(1f, 0.4f, 0.2f)
                    : Color.cyan;
                Gizmos.DrawSphere(currentFrame.MapToWorld(contact.MapPosition), 0.6f);
            }
        }
    }
}
