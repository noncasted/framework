using Internal;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Global.UI
{
    // "Update available": the server closed a socket because this build speaks another protocol (a cached web build
    // after a deploy). Nothing works over the old protocol, so the notice covers everything for good: its own canvas
    // over every other one, built from code in the style of the game (ClientOutdatedNoticeStyle). The button is
    // selected for the gamepad and reloads the page on the web; in the editor and a native build there is nothing to
    // reload, it only says so.
    public class ClientOutdatedNotice : IClientOutdatedNotice
    {
        public ClientOutdatedNotice(
            IReadOnlyLifetime lifetime,
            ILoadingScreen loadingScreen,
            ClientOutdatedNoticeStyle style)
        {
            _lifetime = lifetime;
            _loadingScreen = loadingScreen;
            _style = style;
        }

        private readonly IReadOnlyLifetime _lifetime;
        private readonly ILoadingScreen _loadingScreen;
        private readonly ClientOutdatedNoticeStyle _style;

        private GameObject _root;

        public bool IsShown => _root != null;

        public void Show()
        {
            if (_root != null)
                return;

            Debug.LogWarning("[Network] The server runs another protocol: this client is outdated");

            // On the web the loading screen is the page itself, over the canvas: it would hide the notice.
            _loadingScreen.Hide();

            _root = CreateCanvas();
            var root = _root;

            _lifetime.Listen(() => {
                // Exit from Play destroys the scene objects before the scope ends.
                if (root != null)
                    Object.Destroy(root);
            });

            var border = CreateRect("Panel", _root.transform);
            border.sizeDelta = _style.PanelSize;
            border.gameObject.AddComponent<Image>().color = _style.PanelBorderColor;

            var panel = CreateRect("Body", border);
            Stretch(panel, _style.Border);
            panel.gameObject.AddComponent<Image>().color = _style.PanelColor;

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = _style.Padding;
            layout.spacing = _style.Spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateLabel(panel, _style.Title(), _style.TitleSize, _style.TitleColor);
            CreateLabel(panel, _style.Text(), _style.TextSize, _style.TextColor);
            var button = CreateButton(panel);

            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private GameObject CreateCanvas()
        {
            var gameObject = new GameObject("ClientOutdatedNotice", typeof(RectTransform));
            Object.DontDestroyOnLoad(gameObject);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _style.ReferenceResolution;
            scaler.matchWidthOrHeight = 1f;

            gameObject.AddComponent<GraphicRaycaster>();

            // Takes every click and touch: nothing under the notice reacts any more.
            var shade = CreateRect("Shade", gameObject.transform);
            Stretch(shade, 0f);
            shade.gameObject.AddComponent<Image>().color = _style.ShadeColor;

            return gameObject;
        }

        private void CreateLabel(RectTransform parent, string text, float size, Color color)
        {
            var rect = CreateRect("Label", parent);
            rect.gameObject.AddComponent<LayoutElement>().preferredWidth = _style.PanelSize.x - _style.Padding.horizontal;

            var label = AddText(rect);
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.text = text;
        }

        private Button CreateButton(RectTransform parent)
        {
            var rect = CreateRect("Button", parent);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = _style.ButtonSize.x;
            layout.preferredHeight = _style.ButtonSize.y;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = _style.ButtonColor;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(OnReloadClicked);

            var label = AddText(CreateRect("Label", rect));
            Stretch((RectTransform)label.transform, 0f);
            label.fontSize = _style.ButtonTextSize;
            label.color = _style.ButtonTextColor;
            label.alignment = TextAlignmentOptions.Center;
            label.text = _style.Button();

            return button;
        }

        // TextMeshProUGUI.Awake (run by AddComponent on an active object) loads the font: with none assigned yet it
        // takes TMP_Settings.defaultFontAsset and warns that it is missing. On an inactive object Awake waits for the
        // activation, so the font is assigned first. Other properties go after Awake: its LoadDefaultSettings
        // would replace them.
        private TextMeshProUGUI AddText(RectTransform rect)
        {
            var gameObject = rect.gameObject;
            gameObject.SetActive(false);

            var text = gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _style.Font;

            gameObject.SetActive(true);
            return text;
        }

        private static void OnReloadClicked()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ClientOutdatedReload();
#else
            Debug.LogWarning("[Network] The client is outdated: restart it with the current build");
#endif
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);

            return rect;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // ClientOutdatedNotice.jslib: loads the page again, the new build comes with it.
        [DllImport("__Internal")]
        private static extern void ClientOutdatedReload();
#endif
    }
}
