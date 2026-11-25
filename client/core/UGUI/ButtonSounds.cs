using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine.UI;

namespace Internal
{
    // Internal cannot see the Global audio player, so buttons report clicks and hovers through hooks
    // that the player sets. The emitter is attached to the button in ListenClick, the common entry point of all buttons.
    [AutoStaticsCleanup]
    public static partial class ButtonSounds
    {
        public static Action Clicked { get; private set; }
        public static Action Hovered { get; private set; }

        public static void Bind(IReadOnlyLifetime lifetime, Action clicked, Action hovered)
        {
            Clicked = clicked;
            Hovered = hovered;

            lifetime.Listen(() =>
            {
                Clicked = null;
                Hovered = null;
            });
        }

        public static void Attach(Button button)
        {
            if (button.TryGetComponent(out ButtonSoundEmitter _) == false)
                button.gameObject.AddComponent<ButtonSoundEmitter>();
        }
    }
}
