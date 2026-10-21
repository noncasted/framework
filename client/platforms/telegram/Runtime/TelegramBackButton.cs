using Global.Inputs;
using Internal;

namespace Global.Publisher.Telegram
{
    // The Telegram BackButton: shown while something a back closes is open (IBackInput.HasTargets: a window, the
    // pause, a panel), a press is a back (IBackInput.Press), like Esc or gamepad B.
    public class TelegramBackButton : IPlatformBackButton, IUpdatable
    {
        public TelegramBackButton(IUpdater updater, IBackInput back, ITelegramAPI api)
        {
            _updater = updater;
            _back = back;
            _api = api;
        }

        private readonly IUpdater _updater;
        private readonly IBackInput _back;
        private readonly ITelegramAPI _api;

        private int _presses;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _presses = _api.GetBackButtonPresses();

            _back.HasTargets.View(lifetime, isVisible => _api.SetBackButtonVisible(isVisible));
            lifetime.Listen(() => _api.SetBackButtonVisible(false));

            _updater.Add(lifetime, this);
        }

        public void OnUpdate(float delta)
        {
            var presses = _api.GetBackButtonPresses();

            if (presses == _presses)
                return;

            _presses = presses;
            _back.Press(BackInputKind.Back);
        }
    }
}
