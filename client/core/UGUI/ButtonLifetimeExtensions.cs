using UnityEngine.Events;
using UnityEngine.UI;

namespace Internal
{
    // The click of a uGUI button on a lifetime: the common entry point of the buttons, it attaches the emitter of
    // ButtonSounds.
    public static class ButtonLifetimeExtensions
    {
        public static void ListenClick(this Button button, UnityAction listener)
        {
            var lifetime = button.GetObjectLifetime();
            ButtonSounds.Attach(button);
            button.onClick.AddListener(listener);
            lifetime.Listen(() => button.onClick.RemoveListener(listener));
        }

        public static void ListenClick(this Button button, IReadOnlyLifetime lifetime, UnityAction listener)
        {
            ButtonSounds.Attach(button);
            button.onClick.AddListener(listener);
            lifetime.Listen(() => button.onClick.RemoveListener(listener));
        }
    }
}
