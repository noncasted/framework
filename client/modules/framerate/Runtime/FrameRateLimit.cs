using Internal;
using UnityEngine;

namespace Global.FrameRate
{
    // The frame rate of the game, strongest first: ?fps= of the page (IFrameRateRequest), the fixed choice of the settings of
    // the game (IFrameRateSetting), the automatic rate on a phone or a tablet. Auto on a desktop is the rate of the screen: it
    // neither heats up nor throttles like a phone, and a 120 Hz or 144 Hz screen keeps its frames. The module owns
    // QualitySettings.vSyncCount: Unity ignores Application.targetFrameRate while vSyncCount > 0, so a limit sets
    // vSyncCount 0 and the rate of the screen sets vSyncCount 1 with no target (desktop and web: one frame a refresh, on the
    // web one frame a requestAnimationFrame; phones ignore vSyncCount). The quality asset of a game does not decide it, and
    // nothing switches quality levels at runtime (SetQualityLevel would bring the vSyncCount of the level back). Unity 7000
    // on the web turns a limit that divides the rate of the screen into every n-th requestAnimationFrame (30 on 60 Hz: every
    // second one, no setTimeout), and takes a new limit on the fly. Time.deltaTime follows the real time, so the game runs
    // at the same speed; settings.target_fps and settings.vsync of the telemetry report the choice.
    public class FrameRateLimit : IScopeSetup
    {
        public FrameRateLimit(AutoFrameRateOptions options, IFrameRateSetting setting, IFrameRateRequest request,
            IAutoFrameRate auto, PlatformOptions platform, ILog<FrameRateLimit> log)
        {
            options.Validate();

            _options = options;
            _setting = setting;
            _request = request;
            _auto = auto;
            _platform = platform;
            _log = log;
        }

        private readonly AutoFrameRateOptions _options;
        private readonly IFrameRateSetting _setting;
        private readonly IFrameRateRequest _request;
        private readonly IAutoFrameRate _auto;
        private readonly PlatformOptions _platform;
        private readonly ILog _log;

        // Application.targetFrameRate without a limit: the rate of the screen.
        public const int Native = -1;

        // QualitySettings.vSyncCount of the rate of the screen and of a limit.
        public const int ScreenSync = 1;
        public const int NoSync = 0;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            if (_request.TryGet(out var limit) == true)
            {
                Apply("?fps of the page", limit);
                return;
            }

            _setting.FrameRate.View(lifetime, Choose);
        }

        private void Choose(IReadOnlyLifetime lifetime, FrameRateMode mode)
        {
            if (mode == FrameRateMode.Low)
            {
                Apply("the settings", _options.Low);
                return;
            }

            if (mode == FrameRateMode.High)
            {
                Apply("the settings", _options.High);
                return;
            }

            if (_platform.IsMobile == false)
            {
                Apply("auto on a desktop", Native);
                return;
            }

            _auto.Run(lifetime);
            _auto.Target.View(lifetime, target => Apply("auto", target));
        }

        private void Apply(string source, int limit)
        {
            QualitySettings.vSyncCount = limit == Native ? ScreenSync : NoSync;
            Application.targetFrameRate = limit;
            _log.Info("{Limit} frames per second, vSyncCount {VSync} ({Source})", limit, QualitySettings.vSyncCount, source);
        }
    }
}
