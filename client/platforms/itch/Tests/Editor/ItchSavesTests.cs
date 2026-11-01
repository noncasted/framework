using System;
using Internal;
using NUnit.Framework;
using UnityEngine;

namespace Global.Publisher.Itch.Tests
{
    // A corrupt save fails the start once and loudly, and the next start is not failed by the same blob again: the
    // blob is kept under its own key for the report, the saves start empty.
    public class ItchSavesTests
    {
        private const string Key = "save_v2";
        private const string CorruptKey = "save_v2_corrupt";

        private string _saved;
        private string _savedCorrupt;

        [SetUp]
        public void Keep()
        {
            _saved = PlayerPrefs.HasKey(Key) == true ? PlayerPrefs.GetString(Key) : null;
            _savedCorrupt = PlayerPrefs.HasKey(CorruptKey) == true ? PlayerPrefs.GetString(CorruptKey) : null;
        }

        [TearDown]
        public void Restore()
        {
            Restore(Key, _saved);
            Restore(CorruptKey, _savedCorrupt);
            PlayerPrefs.Save();
        }

        [Test]
        public void CorruptSave_FailsOnce_KeptUnderOwnKey()
        {
            const string blob = "{not json";
            PlayerPrefs.SetString(Key, blob);

            var lifetime = new Lifetime();

            try
            {
                var exception = Assert.Catch<Exception>(() => new ItchSaves().OnBaseSetup(lifetime));
                StringAssert.Contains(CorruptKey, exception.Message);

                Assert.DoesNotThrow(() => new ItchSaves().OnBaseSetup(lifetime), "the same corrupt save fails every start");
                Assert.AreEqual(blob, PlayerPrefs.GetString(CorruptKey));
            }
            finally
            {
                lifetime.Terminate();
            }
        }

        // A null is not a save: JsonUtility gives an empty string, and every next start fails on it in Get.
        [Test]
        public void SaveNull_Throws()
        {
            Assert.Catch<ArgumentNullException>(() => new ItchSaves().Save<SavedData>(null));
        }

        // A broken value of one entry is handled as a broken blob: at the start, once, kept for the report.
        [TestCase("")]
        [TestCase("{not json")]
        public void BrokenEntry_FailsOnce_KeptUnderOwnKey(string value)
        {
            var blob = JsonUtility.ToJson(new SaveEntriesProbe
            {
                Entries = new[] { new SaveEntryProbe { Key = typeof(SavedData).FullName, Value = value } }
            });
            PlayerPrefs.SetString(Key, blob);

            var lifetime = new Lifetime();

            try
            {
                var exception = Assert.Catch<Exception>(() => new ItchSaves().OnBaseSetup(lifetime));
                StringAssert.Contains(CorruptKey, exception.Message);

                var next = new ItchSaves();
                Assert.DoesNotThrow(() => next.OnBaseSetup(lifetime));
                Assert.DoesNotThrow(() => next.Get<SavedData>());
                Assert.AreEqual(blob, PlayerPrefs.GetString(CorruptKey));
            }
            finally
            {
                lifetime.Terminate();
            }
        }

        [Test]
        public void GoodEntry_IsLoaded()
        {
            var saves = new ItchSaves();
            saves.Save(new SavedData { Value = 5 });

            var lifetime = new Lifetime();
            var next = new ItchSaves();
            next.OnBaseSetup(lifetime);

            Assert.AreEqual(5, next.Get<SavedData>().Value);
            lifetime.Terminate();
        }

        [Serializable]
        public class SavedData
        {
            public int Value;
        }

        // The format of the blob, written by hand as a broken save is.
        [Serializable]
        private class SaveEntriesProbe
        {
            public SaveEntryProbe[] Entries;
        }

        [Serializable]
        private class SaveEntryProbe
        {
            public string Key;
            public string Value;
        }

        private static void Restore(string key, string value)
        {
            if (value == null)
                PlayerPrefs.DeleteKey(key);
            else
                PlayerPrefs.SetString(key, value);
        }
    }
}
