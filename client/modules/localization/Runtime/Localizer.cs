using System;
using Internal;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Global.Localization
{
    // The current language and the texts in it. Static, as the sprite and asset catalogs: texts are asked by static
    // helpers (the *Texts classes of the game) and views made with new. The table is loaded by the global scope
    // (LocalizationLoader), the language is pushed by the settings (Settings.Push). A label that shows a text keeps it
    // in the current language through LocalizedLabel. The default language and the system languages shown in another
    // one are the policy of the game (LocalizationOptions).
    // The statics are cleaned on entering Play: with the domain reload off, a table left by the previous session
    // failed the next load ("loaded already"), and its language and listeners stayed.
    [AutoStaticsCleanup]
    public static partial class Localizer
    {
        // Not readonly: the cleanup on entering Play replaces it, the listeners of the previous session go with it.
        private static ViewableProperty<Language> _language = new();

        private static LocalizationTable _table;
        private static LocalizationOptions _options;

        public static IViewableProperty<Language> Current => _language;

        public static int LanguagesCount => Enum.GetValues(typeof(Language)).Length;

        public static void Load(IReadOnlyLifetime lifetime, LocalizationTable table, LocalizationOptions options)
        {
            if (_table != null)
                throw new InvalidOperationException("[Localization] The table is loaded already");

            _table = table;
            _options = options;
            _language.Set(options.DefaultLanguage);

            lifetime.Listen(() => {
                _table = null;
                _options = null;
            });
        }

        public static void SetLanguage(Language language)
        {
            _language.Set(language);
        }

        public static string Get(TextKey key)
        {
            if (_table == null)
                throw new InvalidOperationException($"[Localization] {key.Id} is asked before the table is loaded");

            return _table.Get(key, _language.Value);
        }

        public static Language Next(Language language)
        {
            return (Language)(((int)language + 1) % LanguagesCount);
        }

        // The language of a player who has not picked one in the settings: the one the options map the system
        // language to, the default one for the rest.
        public static Language FromSystem()
        {
            if (_options == null)
                throw new InvalidOperationException("[Localization] The system language is asked before the table is loaded");

            var system = Application.systemLanguage;

            foreach (var mapping in _options.SystemLanguages)
            {
                if (mapping.System == system)
                    return mapping.Language;
            }

            return _options.DefaultLanguage;
        }
    }
}
