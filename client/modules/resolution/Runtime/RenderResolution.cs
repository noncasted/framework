using Internal;
using UnityEngine;

namespace Global.Resolution
{
    // Hands the resolution of the settings (IResolutionSetting) to the platform and every change of it, at once (the
    // settings have read their save when they were built, so the default Auto is never handed before the saved value):
    // the page changes the render pixel ratio of the running engine (read every frame) and keeps the mode for its next
    // start, so the canvas starts at it before the first frame.
    public class RenderResolution : IScopeSetup
    {
        public RenderResolution(IResolutionSetting setting, IRenderPixelRatio pixelRatio)
        {
            _setting = setting;
            _pixelRatio = pixelRatio;
        }

        private readonly IResolutionSetting _setting;
        private readonly IRenderPixelRatio _pixelRatio;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _setting.Resolution.View(lifetime, Apply);
        }

        private void Apply(ResolutionMode mode)
        {
            _pixelRatio.Apply(mode);
            Debug.Log($"[Resolution] {mode}");
        }
    }
}
