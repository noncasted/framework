using System;
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

            var exception = Assert.Catch<Exception>(() => Loaded());
            StringAssert.Contains(CorruptKey, exception.Message);

            Assert.DoesNotThrow(() => Loaded(), "the same corrupt save fails every start");
            Assert.AreEqual(blob, PlayerPrefs.GetString(CorruptKey));
        }

        // Only Load reads the storage: a broken save does not fail the construction.
        [Test]
        public void Construct_CorruptSave_NotRead()
        {
            PlayerPrefs.SetString(Key, "{not json");

            Assert.DoesNotThrow(() => new ItchSaves());
        }

        [Test]
        public void GetBeforeLoad_Throws()
        {
            Assert.Catch<InvalidOperationException>(() => new ItchSaves().Get<SavedData>());
        }

        // A save before the load would write the stored entries over with the new one alone.
        [Test]
        public void SaveBeforeLoad_Throws()
        {
            Assert.Catch<InvalidOperationException>(() => new ItchSaves().Save(new SavedData()));
        }

        [Test]
        public void LoadTwice_Throws()
        {
            var saves = Loaded();

            Assert.Catch<InvalidOperationException>(() => saves.Load());
        }

        // A null is not a save: JsonUtility gives an empty string, and every next start fails on it in Get.
        [Test]
        public void SaveNull_Throws()
        {
            Assert.Catch<ArgumentNullException>(() => Loaded().Save<SavedData>(null));
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

            var exception = Assert.Catch<Exception>(() => Loaded());
            StringAssert.Contains(CorruptKey, exception.Message);

            var next = Loaded();
            Assert.DoesNotThrow(() => next.Get<SavedData>());
            Assert.AreEqual(blob, PlayerPrefs.GetString(CorruptKey));
        }

        [Test]
        public void GoodEntry_IsLoaded()
        {
            Loaded().Save(new SavedData { Value = 5 });

            var next = Loaded();

            Assert.AreEqual(5, next.Get<SavedData>().Value);
        }

        // PlayerPrefs are read at once: the load completes in the call.
        private static ItchSaves Loaded()
        {
            var saves = new ItchSaves();
            saves.Load().GetAwaiter().GetResult();
            return saves;
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
