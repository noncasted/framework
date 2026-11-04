using System.Runtime.InteropServices;
using Internal;

namespace Global.Resolution
{
    // The resolution setting for the page (window.renderResolution.set of WebTemplate~/render-resolution.js): it picks the
    // render pixel ratio of a phone or a tablet; ?dpr= of the page stays stronger.
    public class WebRenderPixelRatio : IRenderPixelRatio
    {
        // The functions of the .jslib exist only in a WebGL player: an IL2CPP standalone build fails to link them.
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WebRenderResolutionSet(int mode);
#else
        private static void WebRenderResolutionSet(int mode) =>
            throw JsLibStub.Missing();
#endif

        public void Apply(ResolutionMode mode)
        {
            WebRenderResolutionSet((int)mode);
        }
    }
}
