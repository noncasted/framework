using Internal;

namespace Global.FrameRate
{
    public static class FrameRateExtensions
    {
        // After AddUpdater: the limit of the frame rate (FrameRateLimit). The options are a field of an options asset of the
        // game (the button of the game reads the same rates); the scope must have an IFrameRateSetting. On WebGL ?fps= of the
        // page is stronger than the setting (WebFrameRateRequest).
        public static IScopeBuilder AddFrameRate(this IScopeBuilder builder, AutoFrameRateOptions options)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<WebFrameRateRequest>()
                   .As<IFrameRateRequest>();
#else
            builder.Register<NoFrameRateRequest>()
                   .As<IFrameRateRequest>();
#endif

            builder.RegisterInstance(options);
            builder.Register<FrameWorkMeter>();
            builder.Register<AutoFrameRateMemory>();

            builder.Register<AutoFrameRate>()
                   .As<IAutoFrameRate>();

            builder.Register<FrameRateLimit>()
                   .As<IScopeSetup>();

            return builder;
        }
    }
}
