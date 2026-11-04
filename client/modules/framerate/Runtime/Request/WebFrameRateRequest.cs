using System.Runtime.InteropServices;
using Internal;

namespace Global.FrameRate
{
    // ?fps= of the page (WebFrameRate.jslib): ?fps=native lifts the limit, ?fps=<n> sets it on any device, stronger than the
    // settings of the player.
    public class WebFrameRateRequest : IFrameRateRequest
    {
        // What WebFrameRate.jslib returns when the page asks for nothing; -1 is ?fps=native.
        private const int NoRequest = 0;

        // The functions of the .jslib exist only in a WebGL player: an IL2CPP standalone build fails to link them.
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int WebFrameRateQuery();
#else
        private static int WebFrameRateQuery() =>
            throw JsLibStub.Missing();
#endif

        public bool TryGet(out int limit)
        {
            limit = WebFrameRateQuery();
            return limit != NoRequest;
        }
    }
}
