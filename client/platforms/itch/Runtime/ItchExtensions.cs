using Global.Publisher;
using Internal;

namespace Global.Publisher.Itch
{
    public static class ItchExtensions
    {
        // Saves in PlayerPrefs (IndexedDB on the web, a file on the desktop): itch, the own website, any platform
        // without saves of its own.
        public static IScopeBuilder AddItchSaves(this IScopeBuilder builder)
        {
            builder.Register<ItchSaves>()
                   .As<ISaves>()
                   .As<IScopeBaseSetup>();

            return builder;
        }
    }
}
