using Internal;

namespace Global.Publisher
{
    // A system "back" button of the platform around the game (the Telegram BackButton). It is shown while
    // something a back closes is open (IBackInput.HasTargets) and presses IBackInput.
    public interface IPlatformBackButton : IScopeSetup
    {
    }
}
