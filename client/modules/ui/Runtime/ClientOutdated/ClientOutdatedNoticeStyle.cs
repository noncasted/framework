using System;
using TMPro;
using UnityEngine;

namespace Global.UI
{
    // The look and the texts of the ClientOutdatedNotice of a game, given to AddClientOutdatedNotice. The texts are read
    // when the notice is shown: a game with localization returns them in the language of that moment.
    // A game names the values it changes: new ClientOutdatedNoticeStyle(font, panelColor: Color.clear). An argument that is
    // not given keeps the default of its property (Unity has no init accessors: IsExternalInit is not in its profile).
    public class ClientOutdatedNoticeStyle
    {
        public ClientOutdatedNoticeStyle(
            TMP_FontAsset font,
            Func<string> title = null,
            Func<string> text = null,
            Func<string> button = null,
            Vector2? referenceResolution = null,
            Vector2? panelSize = null,
            Vector2? buttonSize = null,
            RectOffset padding = null,
            float? spacing = null,
            float? border = null,
            float? titleSize = null,
            float? textSize = null,
            float? buttonTextSize = null,
            Color? shadeColor = null,
            Color? panelBorderColor = null,
            Color? panelColor = null,
            Color? titleColor = null,
            Color? textColor = null,
            Color? buttonColor = null,
            Color? buttonTextColor = null)
        {
            Font = font;
            Title = title ?? Title;
            Text = text ?? Text;
            Button = button ?? Button;
            ReferenceResolution = referenceResolution ?? ReferenceResolution;
            PanelSize = panelSize ?? PanelSize;
            ButtonSize = buttonSize ?? ButtonSize;
            Padding = padding ?? Padding;
            Spacing = spacing ?? Spacing;
            Border = border ?? Border;
            TitleSize = titleSize ?? TitleSize;
            TextSize = textSize ?? TextSize;
            ButtonTextSize = buttonTextSize ?? ButtonTextSize;
            ShadeColor = shadeColor ?? ShadeColor;
            PanelBorderColor = panelBorderColor ?? PanelBorderColor;
            PanelColor = panelColor ?? PanelColor;
            TitleColor = titleColor ?? TitleColor;
            TextColor = textColor ?? TextColor;
            ButtonColor = buttonColor ?? ButtonColor;
            ButtonTextColor = buttonTextColor ?? ButtonTextColor;
        }

        public TMP_FontAsset Font { get; }

        public Func<string> Title { get; } = () => "Update available";
        public Func<string> Text { get; } = () => "The game has been updated. This version can no longer connect.";
        public Func<string> Button { get; } = () => "RELOAD";

        // The canvas of the notice is matched by height.
        public Vector2 ReferenceResolution { get; } = new(1920f, 1080f);
        public Vector2 PanelSize { get; } = new(900f, 420f);
        public Vector2 ButtonSize { get; } = new(360f, 100f);
        public RectOffset Padding { get; } = new(40, 40, 30, 30);
        public float Spacing { get; } = 24f;
        public float Border { get; } = 6f;

        public float TitleSize { get; } = 84f;
        public float TextSize { get; } = 48f;
        public float ButtonTextSize { get; } = 64f;

        // A clear panel and border leave only the texts and the button over the shade.
        public Color ShadeColor { get; } = new(0f, 0f, 0f, 0.8f);
        public Color PanelBorderColor { get; } = Color.gray;
        public Color PanelColor { get; } = Color.black;
        public Color TitleColor { get; } = Color.white;
        public Color TextColor { get; } = Color.white;
        public Color ButtonColor { get; } = Color.white;
        public Color ButtonTextColor { get; } = Color.black;
    }
}
