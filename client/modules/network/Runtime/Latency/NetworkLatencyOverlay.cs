using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Internal
{
    // Debug overlay of NetworkLatencyMonitor and the frame rate, toggled by N (F-keys are taken by the browser: F3 opens
    // the page search) or a three finger tap on a phone. Built from code on its own canvas (UGUI + TMP with the font of the
    // game from NetworkOptions): no prefab, drawn where the game puts its screen canvases (IScreenCanvasCamera). Not
    // IMGUI: one OnGUI in the player keeps the IMGUI, TextCoreTextEngine and TextCoreFontEngine engine modules in a WebGL
    // build.
    public class NetworkLatencyOverlay : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.N;
        private const int ToggleTouches = 3;
        private const float FrameRateWindow = 1f;
        private const float FontSize = 14f;

        private INetworkLatencyMonitor _monitor;
        private GameObject _panel;
        private TextMeshProUGUI _label;

        private float _windowTime;
        private int _windowFrames;
        private float _windowWorst;
        private string _frameRate = "FPS -";

        public static void Create(
            IReadOnlyLifetime lifetime,
            INetworkLatencyMonitor monitor,
            TMP_FontAsset font,
            IScreenCanvasCamera screenCanvas)
        {
            var gameObject = new GameObject(nameof(NetworkLatencyOverlay), typeof(RectTransform));
            DontDestroyOnLoad(gameObject);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.sortingOrder = short.MaxValue;

            var overlay = gameObject.AddComponent<NetworkLatencyOverlay>();
            overlay._monitor = monitor;
            overlay.CreatePanel(font);
            screenCanvas.Attach(canvas);

            lifetime.Listen(() => Destroy(gameObject));
        }

        // No GraphicRaycaster on the canvas and no raycast targets: the overlay never takes clicks or touches.
        private void CreatePanel(TMP_FontAsset font)
        {
            var panel = new GameObject("Panel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(10f, -10f);

            var background = panel.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.7f);
            background.raycastTarget = false;

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var label = new GameObject("Label", typeof(RectTransform));
            label.transform.SetParent(rect, false);

            // TextMeshProUGUI.Awake (run by AddComponent on an active object) loads the font: with none assigned yet it takes
            // TMP_Settings.defaultFontAsset and warns when it is missing. On an inactive object Awake waits for the activation,
            // so the font is assigned first; the other properties go after Awake, its LoadDefaultSettings would replace them.
            label.SetActive(false);
            _label = label.AddComponent<TextMeshProUGUI>();
            _label.font = font;
            label.SetActive(true);
            _label.fontSize = FontSize;
            _label.color = Color.white;
            _label.alignment = TextAlignmentOptions.TopLeft;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.raycastTarget = false;

            _panel = panel;
            _panel.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey) == true && IsTyping() == false)
                _panel.SetActive(!_panel.activeSelf);

            if (Input.touchCount == ToggleTouches && Input.GetTouch(ToggleTouches - 1).phase == TouchPhase.Began)
                _panel.SetActive(!_panel.activeSelf);

            UpdateFrameRate();

            if (_panel.activeSelf == true)
                UpdateLabel();
        }

        // Average frames per second over the last window and its longest frame: the slowdown of the weapon list
        // scales Time.deltaTime, the unscaled one is the real frame.
        private void UpdateFrameRate()
        {
            var delta = Time.unscaledDeltaTime;

            _windowTime += delta;
            _windowFrames++;
            _windowWorst = Mathf.Max(_windowWorst, delta);

            if (_windowTime < FrameRateWindow)
                return;

            _frameRate = $"FPS {_windowFrames / _windowTime:0}  worst {_windowWorst * 1000f:0} ms  {Screen.width}x{Screen.height}";
            _windowTime = 0f;
            _windowFrames = 0;
            _windowWorst = 0f;
        }

        // The text mesh is rebuilt only when the report changes.
        private void UpdateLabel()
        {
            var text = _frameRate + "\n" + _monitor.Report;

            if (_label.text != text)
                _label.text = text;
        }

        // N typed into a text field (the chat, the room code) is a letter.
        private static bool IsTyping()
        {
            var selected = EventSystem.current.currentSelectedGameObject;

            if (selected == null)
                return false;

            return selected.TryGetComponent<TMP_InputField>(out var field) == true
                   && field.isFocused == true;
        }
    }
}
