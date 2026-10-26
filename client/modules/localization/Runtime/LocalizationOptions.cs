using System;
using System.Collections.Generic;
using Internal;
using UnityEngine;

namespace Global.Localization
{
    // The asset of the game (its GlobalAssets catalogue), passed to AddLocalization.
    public class LocalizationOptions : EnvAsset
    {
        // Localization.json: every text of the game in every language (LocalizationTable).
        [SerializeField] private TextAsset _table;

        // The language of a player whose system language is not in the list below, and the language before the
        // settings push the one of the player.
        [SerializeField] private Language _defaultLanguage = Language.English;

        // The system languages the game shows in another language than the default one (Russian for a Russian,
        // Ukrainian or Belarusian system).
        [SerializeField] private List<SystemLanguageMapping> _systemLanguages = new();

        public TextAsset Table => _table;
        public Language DefaultLanguage => _defaultLanguage;
        public IReadOnlyList<SystemLanguageMapping> SystemLanguages => _systemLanguages;
    }

    [Serializable]
    public struct SystemLanguageMapping
    {
        public SystemLanguage System;
        public Language Language;
    }
}
