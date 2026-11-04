using Internal;

namespace Global.Resolution
{
    public static class ResolutionExtensions
    {
        // After the settings of the game, which implement IResolutionSetting: the render resolution (RenderResolution). On
        // WebGL the page takes it through window.renderResolution (WebTemplate~/render-resolution.js, put into the built
        // index.html by RenderResolutionInjection); elsewhere the setting changes nothing.
        public static IScopeBuilder AddResolution(this IScopeBuilder builder)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<WebRenderPixelRatio>()
                   .As<IRenderPixelRatio>();
#else
            builder.Register<NoRenderPixelRatio>()
                   .As<IRenderPixelRatio>();
#endif

            builder.Register<RenderResolution>()
                   .As<IScopeSetupCompletion>();

            return builder;
        }
    }
}
