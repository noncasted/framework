using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Global.Localization
{
    // The YAML of the games may hold the old keys: FormerlySerializedAs stays until every game has re-saved its assets.
    // The keys are for SerializedProperty access from the tests.
    [Serializable]
    public struct SystemLanguageMapping
    {
        public const string SystemKey = nameof(_system);
        public const string LanguageKey = nameof(_language);

        [SerializeField] [FormerlySerializedAs("System")] private SystemLanguage _system;
        [SerializeField] [FormerlySerializedAs("Language")] private Language _language;

        public SystemLanguage System => _system;
        public Language Language => _language;
    }
}
