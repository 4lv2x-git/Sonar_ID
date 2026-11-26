using UnityEngine;
using UnityEngine.UI;
using SonarCrew.Core;
using SonarCrew.Multiplayer;
using SonarCrew.Stations;
using SonarCrew.Submarine;

namespace SonarCrew.UI
{
    /// <summary>
    /// HUD de estación por jugador (Hito 2): retícula, aviso de estación cercana
    /// con su motivo de bloqueo, estado de la estación ocupada, aviso de falta de
    /// energía y alarmas. Se construye en tiempo de ejecución para no depender de
    /// prefabs ni de TextMeshPro; uGUI con la fuente heredada del editor.
    /// El grupo se ancla al rect de la cámara del slot para respetar la pantalla dividida.
    /// </summary>
    public class StationHUD : MonoBehaviour
    {
        [SerializeField] private CrewController crew;
        [SerializeField] private Camera hudCamera;
        [SerializeField] private int baseFontSize = 18;

        [Header("Glifos (dependen del mando; en Xbox: Y = interactuar, X = accionar)")]
        [SerializeField] private string interactGlyph = "Y";
        [SerializeField] private string attackGlyph = "X";

        private const float AlarmDuration = 4f;
        private static Font _font;

        private RectTransform _root;
        private Text _promptText;
        private Text _statusText;
        private Text _powerText;
        private Text _alarmText;
        private Rect _lastRect = new Rect(-1f, -1f, -1f, -1f);
        private float _alarmUntil;

        private int SlotIndex => crew != null && crew.Slot != null ? crew.Slot.Index : 0;

        private void Awake()
        {
            if (crew == null) crew = GetComponentInParent<CrewController>();
            if (hudCamera == null) hudCamera = GetComponent<Camera>();
            Build();
        }

        private void OnEnable()
        {
            GameEvents.AlarmRaised += OnAlarmRaised;
        }

        private void OnDisable()
        {
            GameEvents.AlarmRaised -= OnAlarmRaised;
        }

        private void Update()
        {
            if (_root == null) return;

            FollowCameraRect();
            UpdatePrompt();
            UpdateStatus();
            UpdatePower();
            UpdateAlarm();
        }

        private void Build()
        {
            GameObject canvasObject = new GameObject("HUD_J" + (SlotIndex + 1), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10 + SlotIndex;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject groupObject = new GameObject("Grupo", typeof(RectTransform));
            _root = groupObject.GetComponent<RectTransform>();
            _root.SetParent(canvasObject.transform, false);

            // Retícula.
            GameObject crosshairObject = new GameObject("Reticula", typeof(RectTransform), typeof(Image));
            RectTransform crosshairRect = crosshairObject.GetComponent<RectTransform>();
            crosshairRect.SetParent(_root, false);
            crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
            crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
            crosshairRect.sizeDelta = new Vector2(5f, 5f);
            crosshairRect.anchoredPosition = Vector2.zero;
            Image crosshair = crosshairObject.GetComponent<Image>();
            crosshair.color = new Color(1f, 1f, 1f, 0.65f);
            crosshair.raycastTarget = false;

            _promptText = CreateText("Aviso", TextAnchor.MiddleCenter, baseFontSize, Color.white);
            Anchor(_promptText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 48f), new Vector2(1100f, 34f));

            _statusText = CreateText("Estado", TextAnchor.UpperLeft, baseFontSize - 2, new Color(0.85f, 0.92f, 1f, 0.95f));
            Anchor(_statusText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -16f), new Vector2(760f, 70f));

            _powerText = CreateText("Energia", TextAnchor.UpperRight, baseFontSize, new Color(1f, 0.75f, 0.2f));
            Anchor(_powerText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-20f, -16f), new Vector2(420f, 34f));

            _alarmText = CreateText("Alarma", TextAnchor.MiddleCenter, baseFontSize + 4, new Color(1f, 0.45f, 0.35f));
            Anchor(_alarmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -96f), new Vector2(1200f, 40f));

            _promptText.text = string.Empty;
            _statusText.text = string.Empty;
            _powerText.text = string.Empty;
            _alarmText.text = string.Empty;
        }

        private void FollowCameraRect()
        {
            Rect rect = hudCamera != null ? hudCamera.rect : new Rect(0f, 0f, 1f, 1f);
            if (rect == _lastRect) return;

            _lastRect = rect;
            _root.anchorMin = rect.min;
            _root.anchorMax = rect.max;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
        }

        private void UpdatePrompt()
        {
            if (crew == null || _promptText == null)
                return;

            if (crew.OccupiedStation != null)
            {
                _promptText.text = "[" + attackGlyph + "] Accionar      [" + interactGlyph + "] Soltar";
                _promptText.color = Color.white;
                return;
            }

            IStation focused = crew.CurrentStation;
            if (focused == null)
            {
                _promptText.text = string.Empty;
                return;
            }

            StationAvailability availability = focused.AvailabilityFor(crew);
            if (availability == StationAvailability.Available)
            {
                _promptText.text = "[" + interactGlyph + "] " + focused.StationName;
                _promptText.color = Color.white;
            }
            else
            {
                _promptText.text = focused.StationName + " · " + Describe(availability);
                _promptText.color = new Color(1f, 0.65f, 0.55f);
            }
        }

        private void UpdateStatus()
        {
            if (crew == null || _statusText == null)
                return;

            string label = "J" + (SlotIndex + 1);
            if (crew.OccupiedStation != null)
                _statusText.text = label + "  ·  " + crew.OccupiedStation.StationName + "\n" + crew.OccupiedStation.StatusText;
            else if (crew.CurrentStation != null)
                _statusText.text = label + "\n" + crew.CurrentStation.StationName;
            else
                _statusText.text = label;
        }

        private void UpdatePower()
        {
            if (_powerText == null) return;

            if (!ServiceLocator.TryGet(out PowerSystem power))
            {
                _powerText.text = string.Empty;
                return;
            }

            if (power.FailureActive)
                _powerText.text = "SIN ENERGÍA (avería)";
            else if (!power.HasPower)
                _powerText.text = "SIN ENERGÍA";
            else
                _powerText.text = string.Empty;
        }

        private void UpdateAlarm()
        {
            if (_alarmText == null) return;
            if (Time.time >= _alarmUntil && _alarmText.text.Length > 0)
                _alarmText.text = string.Empty;
        }

        private void OnAlarmRaised(string message)
        {
            if (_alarmText == null) return;
            _alarmText.text = "¡ALARMA! " + message;
            _alarmUntil = Time.time + AlarmDuration;
        }

        private Text CreateText(string objectName, TextAnchor alignment, int fontSize, Color color)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(_root, false);

            Text text = textObject.GetComponent<Text>();
            text.font = HudFont;
            text.fontSize = Mathf.Max(8, fontSize);
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static Font HudFont
        {
            get
            {
                if (_font != null) return _font;

                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null)
                    _font = Font.CreateDynamicFontFromOSFont("Arial", 18);
                return _font;
            }
        }

        private static string Describe(StationAvailability availability)
        {
            switch (availability)
            {
                case StationAvailability.OccupiedBySelf:
                    return "en uso";
                case StationAvailability.OccupiedByOther:
                    return "ocupada";
                case StationAvailability.NoPower:
                    return "sin energía";
                case StationAvailability.OutOfService:
                    return "averiada";
                case StationAvailability.Blocked:
                    return "bloqueada";
                case StationAvailability.Available:
                    return "disponible";
                default:
                    return "no disponible";
            }
        }
    }
}
