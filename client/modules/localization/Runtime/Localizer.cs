using System;
using Internal;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace Global.Localization
{
    // The current language and the texts in it. Static, as the sprite and asset catalogs: texts are asked by static
    // helpers (the *Texts classes of the game) and views made with new. The table is loaded by the global scope
    // (LocalizationLoader), the language is pushed by the settings (Settings.Push). A label that shows a text keeps it
    // in the current language through LocalizedLabel.
    [NoAutoStaticsCleanup]
    public static class Localizer
    {
        private static readonly ViewableProperty<Language> _language = new(Language.Russian);

        private static LocalizationTable _table;

        public static IViewableProperty<Language> Current => _language;

        public static int LanguagesCount => Enum.GetValues(typeof(Language)).Length;

        public static void Load(IReadOnlyLifetime lifetime, LocalizationTable table)
        {
            if (_table != null)
                throw new InvalidOperationException("[Localization] The table is loaded already");

            _table = table;
            lifetime.Listen(() => _table = null);
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

        // The language of a player who has not picked one in the settings: Russian for a Russian, Ukrainian or
        // Belarusian system, English for the rest.
        public static Language FromSystem()
        {
            return Application.systemLanguage switch
            {
                SystemLanguage.Russian => Language.Russian,
                SystemLanguage.Ukrainian => Language.Russian,
                SystemLanguage.Belarusian => Language.Russian,
                _ => Language.English
            };
        }
    }
}
