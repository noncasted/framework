using Internal;

namespace Global.Resolution
{
    // Hands the resolution of the settings (IResolutionSetting) to the platform and every change of it, at once (the
    // settings have read their save when they were built, so the default Auto is never handed before the saved value):
    // the page changes the render pixel ratio of the running engine (read every frame) and keeps the mode for its next
    // start, so the canvas starts at it before the first frame.
    public class RenderResolution : IScopeSetup
    {
        public RenderResolution(IResolutionSetting setting, IRenderPixelRatio pixelRatio, ILog<RenderResolution> log)
        {
            _setting = setting;
            _pixelRatio = pixelRatio;
            _log = log;
        }

        private readonly IResolutionSetting _setting;
        private readonly IRenderPixelRatio _pixelRatio;
        private readonly ILog _log;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _setting.Resolution.View(lifetime, Apply);
        }

        private void Apply(ResolutionMode mode)
        {
            _pixelRatio.Apply(mode);
            _log.Info("{Mode}", mode);
        }
    }
}
