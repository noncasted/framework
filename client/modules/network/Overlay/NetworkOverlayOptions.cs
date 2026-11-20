using TMPro;
using UnityEngine;

namespace Internal
{
    // The options of the latency overlay, given to AddNetworkLatencyOverlay: a separate asset, so the options of the network
    // (NetworkOptions) stay without TMP and a game without uGUI builds the network without the overlay.
    public class NetworkOverlayOptions : EnvAsset
    {
        // The font of the text of NetworkLatencyOverlay, required: the framework has no font of its own, and
        // TMP_Settings.defaultFontAsset of a game may be missing.
        [SerializeField] private TMP_FontAsset _font;

        public TMP_FontAsset Font => _font;
    }
}
