using UnityEngine;
using UnityEngine.UI;
using SonarCrew.Core;

namespace SonarCrew.UI
{
    /// <summary>
    /// Pantalla de resultado (hito 4): se muestra cuando `GameFlow` pasa a victoria
    /// y recuerda cómo reiniciar. Se construye en tiempo de ejecución, como el resto
    /// del HUD provisional, para no depender de prefabs ni de TextMeshPro.
    /// </summary>
    public class VictoryHUD : MonoBehaviour
    {
        private const int CanvasSortingOrder = 100;

        private static Font _font;

        private Canvas _canvas;
        private Text _title;
        private Text _hint;

        private void Awake()
        {
            Build();
            SetVisible(false);
        }

        private void Update()
        {
            bool victory = ServiceLocator.TryGet(out GameFlow flow) && flow.State == GameState.Victory;
            SetVisible(victory);
        }

        private void SetVisible(bool visible)
        {
            if (_canvas != null && _canvas.enabled != visible)
                _canvas.enabled = visible;
        }

        private void Build()
        {
            GameObject canvasObject = new GameObject("HUD_Victoria", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CanvasSortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject backdrop = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.SetParent(canvasObject.transform, false);
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            Image backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = new Color(0.02f, 0.06f, 0.09f, 0.82f);
            backdropImage.raycastTarget = false;

            _title = CreateText(backdrop.transform, "Titulo", 64, new Color(1f, 0.84f, 0.35f), TextAnchor.MiddleCenter);
            Anchor(_title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 40f), new Vector2(0f, 110f));
            _title.text = "¡TESORO RECUPERADO!";

            _hint = CreateText(backdrop.transform, "Ayuda", 30, Color.white, TextAnchor.MiddleCenter);
            Anchor(_hint.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -60f), new Vector2(0f, 60f));
            _hint.text = "[Y] volver a jugar";
        }

        private static Text CreateText(Transform parent, string objectName, int fontSize, Color color, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.font = HudFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
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
    }
}
