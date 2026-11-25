using Internal;

namespace Global.Publisher
{
    // Web and itch: the page has no back button of its own (the browser one leaves the page).
    public class NoPlatformBackButton : IPlatformBackButton
    {
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
        }
    }
}
