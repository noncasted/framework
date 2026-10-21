using Global.UI;
using Internal;

namespace Global.Web
{
    // Services of a WebGL build over its template: the loading screen of index.html (window.loadingScreen) and the
    // page shell (window.webShell of TemplateData/web-shell.js: safe area, mobile check). Registered only in a WebGL
    // player (#if UNITY_WEBGL && !UNITY_EDITOR): the .jslib functions do not exist elsewhere.
    public static class WebExtensions
    {
        public static IScopeBuilder AddWebLoadingScreen(this IScopeBuilder builder)
        {
            builder.Register<WebLoadingScreen>()
                   .As<ILoadingScreen>();

            return builder;
        }

        // Needs web-shell.js in the template.
        public static IScopeBuilder AddWebSafeArea(this IScopeBuilder builder)
        {
            builder.Register<WebSafeArea>()
                   .As<ISafeArea>()
                   .As<IScopeBaseSetup>();

            return builder;
        }
    }
}
