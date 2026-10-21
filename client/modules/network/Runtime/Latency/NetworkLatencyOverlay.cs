using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Internal
{
    // Debug overlay of NetworkLatencyMonitor and the frame rate, toggled by N (F-keys are taken by the browser: F3 opens
    // the page search) or a three finger tap on a phone. IMGUI: no prefab, no canvas, works in WebGL.
    public class NetworkLatencyOverlay : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.N;
        private const int ToggleTouches = 3;
        private const float FrameRateWindow = 1f;

        private INetworkLatencyMonitor _monitor;
        private GUIStyle _style;
        private bool _isVisible;

        private float _windowTime;
        private int _windowFrames;
        private float _windowWorst;
        private string _frameRate = "FPS -";

        public static void Create(IReadOnlyLifetime lifetime, INetworkLatencyMonitor monitor)
        {
            var gameObject = new GameObject(nameof(NetworkLatencyOverlay));
            DontDestroyOnLoad(gameObject);

            var overlay = gameObject.AddComponent<NetworkLatencyOverlay>();
            overlay._monitor = monitor;

            lifetime.Listen(() => Destroy(gameObject));
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey) == true && IsTyping() == false)
                _isVisible = !_isVisible;

            if (Input.touchCount == ToggleTouches && Input.GetTouch(ToggleTouches - 1).phase == TouchPhase.Began)
                _isVisible = !_isVisible;

            UpdateFrameRate();
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

        // N typed into a text field (the chat, the room code) is a letter.
        private static bool IsTyping()
        {
            var selected = EventSystem.current.currentSelectedGameObject;

            if (selected == null)
                return false;

            return selected.TryGetComponent<TMP_InputField>(out var field) == true
                   && field.isFocused == true;
        }

        private void OnGUI()
        {
            if (_isVisible == false)
                return;

            _style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                padding = new RectOffset(8, 8, 6, 6),
                normal = { textColor = Color.white }
            };

            var content = new GUIContent(_frameRate + "\n" + _monitor.Report);
            var size = _style.CalcSize(content);

            GUI.Box(new Rect(10f, 10f, size.x, size.y), content, _style);
        }
    }
}
