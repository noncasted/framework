using Internal;

namespace Global.Localization
{
    // Loads the table of the texts for the life of the global scope.
    public class LocalizationLoader : IScopeSetup
    {
        public LocalizationLoader(LocalizationOptions options)
        {
            _options = options;
        }

        private readonly LocalizationOptions _options;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            var table = new LocalizationTable(_options.Table.text);
            Localizer.Load(lifetime, table);
        }
    }
}
