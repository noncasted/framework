using System;
using Internal;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Global.Localization.Tests
{
    // The language policy is the game's (LocalizationOptions): the default language and the system languages shown
    // in another one. The table and the language go with the lifetime of the load.
    public class LocalizerTests
    {
        private const string Table = "{\"entries\":[{\"key\":\"Menu.Play\",\"ru\":\"ИГРАТЬ\",\"en\":\"PLAY\"}]}";

        [Test]
        public void Load_LanguageIsTheDefaultOfOptions()
        {
            var options = Options(Language.English);
            var lifetime = new Lifetime();

            try
            {
                Localizer.SetLanguage(Language.Russian);
                Localizer.Load(lifetime, new LocalizationTable(Table), options);

                Assert.AreEqual(Language.English, Localizer.Current.Value);
            }
            finally
            {
                lifetime.Terminate();
                UnityEngine.Object.DestroyImmediate(options);
            }
        }

        // The system language of this machine mapped to Russian by the options: FromSystem follows the options.
        [Test]
        public void FromSystem_FollowsOptions()
        {
            var options = Options(Language.English, Application.systemLanguage, Language.Russian);
            var lifetime = new Lifetime();

            try
            {
                Localizer.Load(lifetime, new LocalizationTable(Table), options);

                Assert.AreEqual(Language.Russian, Localizer.FromSystem());
            }
            finally
            {
                lifetime.Terminate();
                UnityEngine.Object.DestroyImmediate(options);
            }
        }

        [Test]
        public void FromSystem_NotMapped_IsDefault()
        {
            var other = Application.systemLanguage == SystemLanguage.Korean ? SystemLanguage.Thai : SystemLanguage.Korean;
            var options = Options(Language.Russian, other, Language.English);
            var lifetime = new Lifetime();

            try
            {
                Localizer.Load(lifetime, new LocalizationTable(Table), options);

                Assert.AreEqual(Language.Russian, Localizer.FromSystem());
            }
            finally
            {
                lifetime.Terminate();
                UnityEngine.Object.DestroyImmediate(options);
            }
        }

        [Test]
        public void FromSystem_BeforeLoad_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => Localizer.FromSystem());
        }

        private static LocalizationOptions Options(Language defaultLanguage)
        {
            var options = ScriptableObject.CreateInstance<LocalizationOptions>();
            var serialized = new SerializedObject(options);
            serialized.FindProperty("_defaultLanguage").enumValueIndex = (int)defaultLanguage;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return options;
        }

        private static LocalizationOptions Options(Language defaultLanguage, SystemLanguage system, Language language)
        {
            var options = Options(defaultLanguage);
            var serialized = new SerializedObject(options);
            var list = serialized.FindProperty("_systemLanguages");
            list.arraySize = 1;
            var entry = list.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("System").intValue = (int)system;
            entry.FindPropertyRelative("Language").enumValueIndex = (int)language;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return options;
        }
    }
}
