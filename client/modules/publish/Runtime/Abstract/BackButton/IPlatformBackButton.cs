using Internal;

namespace Global.Publisher
{
    // A system "back" button of the platform around the game (the Telegram BackButton). It is shown while
    // something a back closes is open (IBackInput.HasTargets) and presses IBackInput.
    public interface IPlatformBackButton : IScopeSetup
    {
    }

    // Web and itch: the page has no back button of its own (the browser one leaves the page).
    public class NoPlatformBackButton : IPlatformBackButton
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
        }
    }
}
