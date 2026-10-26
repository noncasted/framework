using Global.Inputs;
using Internal;

namespace Global.UI
{
    public static class UIExtensions
    {
        // The state machine of the UI and the gamepad navigation (AddUIStateMachine + AddUINavigation). The loading
        // screen and the safe area are added by the platform: AddLoadingScreen / AddScreenSafeArea here, the web ones
        // in the web platform.
        public static IScopeBuilder AddUI(this IScopeBuilder builder)
        {
            builder.AddUIStateMachine();
            builder.AddUINavigation();

            return builder;
        }

        // The state machine of the UI and the input constraints, without the gamepad navigation: for a game whose
        // event system is not driven by a StandaloneInputModule (InputSystemUIInputModule of the Input System) and
        // that is not played with a gamepad. IUINavigation is not registered then.
        public static IScopeBuilder AddUIStateMachine(this IScopeBuilder builder)
        {
            builder.Register<InputConstraintsStorage>()
                   .As<IInputConstraintsStorage>();

            builder.Register<UIStateMachine>()
                   .WithScopeLifetime()
                   .As<IUIStateMachine>();

            return builder;
        }

        // The selection of the uGUI buttons by the gamepad (IUINavigation). Needs the inputs module (AddInput) in the
        // scope, and its event system prefab must carry a StandaloneInputModule (UINavigation drives it).
        public static IScopeBuilder AddUINavigation(this IScopeBuilder builder)
        {
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

        // The notice of an outdated client (IClientOutdatedNotice of the network): needs a loading screen in the scope.
        public static IScopeBuilder AddClientOutdatedNotice(this IScopeBuilder builder, ClientOutdatedNoticeStyle style)
        {
            builder.RegisterInstance(style);

            builder.Register<ClientOutdatedNotice>()
                   .WithScopeLifetime()
                   .As<IClientOutdatedNotice>();

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
