using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
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
        // A game without texts takes the settings without the localization and TextMeshPro.
        [Test]
        public void Assembly_DoesNotReferenceLocalization()
        {
            var references = typeof(SettingsSave).Assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();

            CollectionAssert.DoesNotContain(references, "Internal.Localization");
        }

        [UnityTest]
        public IEnumerator Apply_CallerChangesItsSaveAfter_CurrentStays() => UniTask.ToCoroutine(async () =>
        {
            var options = ScriptableObject.CreateInstance<SettingsOptions>();

            try
            {
                var settings = new Settings(new MemorySaves(), options);

                var save = settings.Copy();
                save.SetMasterVolume(0.5f);
                await settings.Apply(save);

                save.SetMasterVolume(0.1f);

                Assert.AreEqual(0.5f, settings.Copy().MasterVolume);
                Assert.AreEqual(0.5f, settings.MasterVolume.Value);
            }
            finally
            {
                Object.DestroyImmediate(options);
            }
        });

        private class MemorySaves : ISaves
        {
            public UniTask Load()
            {
                return UniTask.CompletedTask;
            }

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
