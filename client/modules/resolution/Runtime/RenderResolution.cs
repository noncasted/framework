using Internal;
using UnityEngine;

namespace Global.Resolution
{
    // Hands the resolution of the settings (IResolutionSetting) to the platform once the settings are loaded (the completion:
    // after the setup of the settings, so the default Auto is never handed before the saved value) and on every change, at
    // once: the page changes the render pixel ratio of the running engine (read every frame) and keeps the mode for its next
    // start, so the canvas starts at it before the first frame.
    public class RenderResolution : IScopeSetupCompletion
    {
        public RenderResolution(IResolutionSetting setting, IRenderPixelRatio pixelRatio)
        {
            _setting = setting;
            _pixelRatio = pixelRatio;
        }

        private readonly IResolutionSetting _setting;
        private readonly IRenderPixelRatio _pixelRatio;

        public void OnSetupCompletion(IReadOnlyLifetime lifetime)
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
