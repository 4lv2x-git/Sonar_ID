using UnityEngine;
using SonarCrew.Multiplayer;
using SonarCrew.Core;

namespace SonarCrew.Stations
{
    /// <summary>
    /// Estación de la sala del gancho (hito 4). No mueve el gancho en el interior:
    /// manda sobre el <see cref="GrappleHook"/> que cuelga por debajo del casco y
    /// muestra su cámara de baja calidad en la pantalla del puesto. Con daño grave
    /// el gancho se retrae solo y la estación queda fuera de servicio (README).
    /// </summary>
    public class GrappleStation : StationBase
    {
        [Header("Gancho")]
        [Tooltip("Pantalla que reproduce la cámara del gancho.")]
        [SerializeField] private Renderer monitorRenderer;
        [Tooltip("Opcional: si se deja vacío se resuelve por el ServiceLocator.")]
        [SerializeField] private GrappleHook hook;

        [Header("Mando")]
        [Tooltip("Zona muerta del stick derecho para soltar o recoger cable.")]
        [SerializeField] private float stickDeadzone = 0.2f;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock _monitorBlock;

        /// <summary>Profundidad automática pedida con el botón de accionar (<0 = apagada).</summary>
        private float _autoDepth = -1f;

        protected override string DefaultStationName => "Gancho recolector";

        protected override bool OutOfServiceByDamage => HasSevereDamage;

        private GrappleHook Hook
        {
            get
            {
                if (hook == null)
                    ServiceLocator.TryGet(out hook);
                return hook;
            }
        }

        public override string OperateHint =>
            "[X] soltar / izar cable · Stick derecho ajusta la profundidad · [Y] soltar o aléjate";

        public override string StatusText
        {
            get
            {
                if (HasSevereDamage) return "GANCHO RETRAÍDO (daño grave)";
                if (!HasPower) return "GANCHO SIN ENERGÍA";

                GrappleHook rig = Hook;
                if (rig == null) return "Gancho no instalado";

                string cable = "Cable " + rig.Depth.ToString("0.0") + " m de " + rig.MaxDepth.ToString("0.0") + " m";
                return rig.TryGetTreasureDistance(out float meters)
                    ? cable + " · cofre a " + meters.ToString("0.0") + " m"
                    : cable;
            }
        }

        protected override void OnStationStart()
        {
            ApplyMonitorFeed();
            if (Hook == null)
                Debug.LogWarning("GrappleStation: no hay GrappleHook en la escena; el gancho no se puede accionar.");
        }

        protected override void OnOccupied(CrewController crew)
        {
            // La cámara del gancho solo se renderiza mientras alguien mira la pantalla.
            GrappleHook rig = Hook;
            if (rig != null)
                rig.SetFeedEnabled(true);
            ApplyMonitorFeed();
        }

        protected override void OnReleased(CrewController crew)
        {
            if (Hook != null)
                Hook.SetFeedEnabled(false);
            _autoDepth = -1f;
        }

        protected override void OnOperate(InputRouter router, int slotIndex)
        {
            GrappleHook rig = Hook;
            if (rig == null) return;

            if (router.GetAttackPressed(slotIndex))
            {
                bool fullyOut = rig.Depth >= rig.MaxDepth - 0.05f;
                _autoDepth = fullyOut ? 0f : rig.MaxDepth;
            }

            Vector2 look = router.GetLook(slotIndex);
            if (look.y > stickDeadzone)
            {
                _autoDepth = -1f;
                rig.Drive(1f);
            }
            else if (look.y < -stickDeadzone)
            {
                _autoDepth = -1f;
                rig.Drive(-1f);
            }

            DriveTowardsAutoTarget(rig);
        }

        protected override void OnRoomDamage(DamageLevel level)
        {
            // README: con daño grave el gancho se retrae solo y deja de funcionar.
            if (level < DamageLevel.Grave) return;

            _autoDepth = -1f;
            if (Hook != null)
            {
                Hook.Retract();
                Hook.SetFeedEnabled(false);
            }
        }

        /// <summary>Lleva el cable hacia la profundidad pedida con [X], a velocidad de cabestrante.</summary>
        private void DriveTowardsAutoTarget(GrappleHook rig)
        {
            if (_autoDepth < 0f) return;

            float delta = _autoDepth - rig.Depth;
            if (Mathf.Abs(delta) < 0.01f)
            {
                _autoDepth = -1f;
                return;
            }

            rig.Drive(Mathf.Sign(delta));
        }

        /// <summary>Pinta la señal del gancho en la pantalla del puesto.</summary>
        private void ApplyMonitorFeed()
        {
            if (monitorRenderer == null) return;

            RenderTexture feed = Hook != null ? Hook.FeedTexture : null;
            if (feed == null) return;

            _monitorBlock ??= new MaterialPropertyBlock();
            monitorRenderer.GetPropertyBlock(_monitorBlock);
            _monitorBlock.SetTexture(BaseMapId, feed);
            _monitorBlock.SetColor(BaseColorId, Color.white);
            monitorRenderer.SetPropertyBlock(_monitorBlock);
        }
    }
}
