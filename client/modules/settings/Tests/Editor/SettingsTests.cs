using System.Collections;
using Cysharp.Threading.Tasks;
using Global.Localization;
using Global.Publisher;
using Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Global.Settings.Tests
{
    // Settings.Apply keeps its own copy of the applied save: a caller that changes its instance afterwards (an open
    // settings window editing the same object) does not change the settings in force behind their back.
    public class SettingsTests
    {
        private const string Table = "{\"entries\":[{\"key\":\"Menu.Play\",\"ru\":\"ИГРАТЬ\",\"en\":\"PLAY\"}]}";

        [UnityTest]
        public IEnumerator Apply_CallerChangesItsSaveAfter_CurrentStays() => UniTask.ToCoroutine(async () =>
        {
            var localization = ScriptableObject.CreateInstance<LocalizationOptions>();
            var options = ScriptableObject.CreateInstance<SettingsOptions>();
            var lifetime = new Lifetime();

            try
            {
                Localizer.Load(lifetime, new LocalizationTable(Table), localization);

                var settings = new Settings(new MemorySaves(), options);
                await settings.OnSetupAsync(lifetime);

                var save = settings.Copy();
                save.MasterVolume = 0.5f;
                await settings.Apply(save);

                save.MasterVolume = 0.1f;

                Assert.AreEqual(0.5f, settings.Copy().MasterVolume);
                Assert.AreEqual(0.5f, settings.MasterVolume.Value);
            }
            finally
            {
                lifetime.Terminate();
                Object.DestroyImmediate(options);
                Object.DestroyImmediate(localization);
            }
        });

        private class MemorySaves : ISaves
        {
            public T Get<T>() where T : class, new()
            {
                return new T();
            }

            public UniTask Save<T>(T data)
            {
                return UniTask.CompletedTask;
            }
        }
    }
}
