using Internal;
using UnityEngine;

namespace Global.FrameRate
{
    // Application.targetFrameRate of the game, strongest first: ?fps= of the page (IFrameRateRequest), the fixed choice of the
    // settings of the game (IFrameRateSetting), the automatic rate on a phone or a tablet. Auto on a desktop is the rate of
    // the screen: it neither heats up nor throttles like a phone, and a 120 Hz or 144 Hz screen keeps its frames. Unity 7000
    // on the web turns a limit that divides the rate of the screen into every n-th requestAnimationFrame (30 on 60 Hz: every
    // second one, no setTimeout), and takes a new limit on the fly. Time.deltaTime follows the real time, so the game runs
    // at the same speed; settings.target_fps of the telemetry reports the limit.
    public class FrameRateLimit : IScopeSetupCompletion
    {
        public FrameRateLimit(AutoFrameRateOptions options, IFrameRateSetting setting, IFrameRateRequest request,
            IAutoFrameRate auto, PlatformOptions platform)
        {
            options.Validate();

            _options = options;
            _setting = setting;
            _request = request;
            _auto = auto;
            _platform = platform;
        }

        private readonly AutoFrameRateOptions _options;
        private readonly IFrameRateSetting _setting;
        private readonly IFrameRateRequest _request;
        private readonly IAutoFrameRate _auto;
        private readonly PlatformOptions _platform;

        // Application.targetFrameRate without a limit: the rate of the screen.
        public const int Native = -1;

        public void OnSetupCompletion(IReadOnlyLifetime lifetime)
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

        private static void Apply(string source, int limit)
        {
            Application.targetFrameRate = limit;
            Debug.Log($"[FrameRate] {limit} frames per second ({source})");
        }
    }
}
