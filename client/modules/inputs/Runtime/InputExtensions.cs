using Internal;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Global.Inputs
{
    public static class InputExtensions
    {
        // The event system of the scope is instantiated from the prefab; with the ui module it must carry a
        // StandaloneInputModule (UINavigation drives it).
        public static IScopeBuilder AddInput(this IScopeBuilder builder, InputOptions options, EventSystem eventSystemPrefab)
        {
            builder.RegisterInstance(options);

            if (GamepadLayout.HasLayout(Application.platform) == true)
            {
                builder.Register<GamepadReader>()
                       .As<IGamepadReader>();
            }
            else
            {
                builder.Register<NoGamepadReader>()
                       .As<IGamepadReader>();
            }

            builder.Register<InputScheme>()
                   .As<IInputScheme>()
                   .As<IScopeSetup>();

            builder.Register<BackInput>()
                   .As<IBackInput>()
                   .As<IScopeSetup>();

            builder.Instantiate(eventSystemPrefab);

            return builder;
        }
    }
}
