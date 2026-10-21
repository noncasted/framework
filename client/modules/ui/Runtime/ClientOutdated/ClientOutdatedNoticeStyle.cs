using System;
using TMPro;
using UnityEngine;

namespace Global.UI
{
    // The look and the texts of the ClientOutdatedNotice of a game, given to AddClientOutdatedNotice. The texts are read
    // when the notice is shown: a game with localization returns them in the language of that moment.
    public class ClientOutdatedNoticeStyle
    {
        public TMP_FontAsset Font { get; set; }

        public Func<string> Title { get; set; } = () => "Update available";
        public Func<string> Text { get; set; } = () => "The game has been updated. This version can no longer connect.";
        public Func<string> Button { get; set; } = () => "RELOAD";

        // The canvas of the notice is matched by height.
        public Vector2 ReferenceResolution { get; set; } = new(1920f, 1080f);
        public Vector2 PanelSize { get; set; } = new(900f, 420f);
        public Vector2 ButtonSize { get; set; } = new(360f, 100f);
        public RectOffset Padding { get; set; } = new(40, 40, 30, 30);
        public float Spacing { get; set; } = 24f;
        public float Border { get; set; } = 6f;

        public float TitleSize { get; set; } = 84f;
        public float TextSize { get; set; } = 48f;
        public float ButtonTextSize { get; set; } = 64f;

        // A clear panel and border leave only the texts and the button over the shade.
        public Color ShadeColor { get; set; } = new(0f, 0f, 0f, 0.8f);
        public Color PanelBorderColor { get; set; } = Color.gray;
        public Color PanelColor { get; set; } = Color.black;
        public Color TitleColor { get; set; } = Color.white;
        public Color TextColor { get; set; } = Color.white;
        public Color ButtonColor { get; set; } = Color.white;
        public Color ButtonTextColor { get; set; } = Color.black;
    }
}
