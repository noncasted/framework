using Global.Inputs;
using Internal;

namespace Global.UI
{
    public static class UIExtensions
    {
        // The state machine of the UI and the gamepad navigation. Needs the inputs module (AddInput) in the scope; the
        // loading screen and the safe area are added by the platform: AddLoadingScreen / AddScreenSafeArea here, the
        // web ones in the web platform.
        public static IScopeBuilder AddUI(this IScopeBuilder builder)
        {
            builder.Register<InputConstraintsStorage>()
                   .As<IInputConstraintsStorage>();

            builder.Register<UIStateMachine>()
                   .WithScopeLifetime()
                   .As<IUIStateMachine>();

            builder.Register<UINavigation>()
                   .As<IUINavigation>()
                   .As<IScopeSetup>();

            return builder;
        }

        public static IScopeBuilder AddLoadingScreen(this IScopeBuilder builder, LoadingScreen prefab)
        {
            var loadingScreen = builder.Instantiate(prefab);

            builder.Inject(loadingScreen);

            builder.RegisterInstance(loadingScreen)
                   .As<ILoadingScreen>()
                   .As<IScopeSetup>();

            return builder;
        }

        // Screen.safeArea: the editor (Device Simulator) and native builds.
        public static IScopeBuilder AddScreenSafeArea(this IScopeBuilder builder)
        {
            builder.Register<ScreenSafeArea>()
                   .As<ISafeArea>()
                   .As<IScopeBaseSetup>();

            return builder;
        }
    }
}
