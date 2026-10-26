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

        private static void Restore(string key, string value)
        {
            if (value == null)
                PlayerPrefs.DeleteKey(key);
            else
                PlayerPrefs.SetString(key, value);
        }
    }
}
