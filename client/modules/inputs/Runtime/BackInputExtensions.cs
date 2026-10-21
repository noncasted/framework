using System;
using Internal;

namespace Global.Inputs
{
    public static class BackInputExtensions
    {
        // Calls the action on every back (IBackInput.IsBackPressed: Esc, gamepad B / Start) while the lifetime lives,
        // in the update of the updater. For a window that closes on a back and has no update of its own.
        // The window is a back target (IBackInput.AddTarget) for the same lifetime.
        public static void ListenBack(this IBackInput back, IReadOnlyLifetime lifetime, IUpdater updater, Action onBack)
        {
            back.AddTarget(lifetime);
            updater.Add(lifetime, new Listener(back, onBack));
        }

        private class Listener : IUpdatable
        {
            public Listener(IBackInput back, Action onBack)
            {
                _back = back;
                _onBack = onBack;
            }

            private readonly IBackInput _back;
            private readonly Action _onBack;

            public void OnUpdate(float delta)
            {
                if (_back.IsBackPressed == true)
                    _onBack();
            }
        }
    }
}
