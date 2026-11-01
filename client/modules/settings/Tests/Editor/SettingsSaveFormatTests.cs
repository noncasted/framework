using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Global.Settings.Tests
{
    // The saves of the players and the options assets of the games hold the names of the backing fields of
    // SettingsSave: a changed name reads as the default from every existing save.
    public class SettingsSaveFormatTests
    {
        private const string Json = "{"
            + "\"<MasterVolume>k__BackingField\":0.25,"
            + "\"<SoundsVolume>k__BackingField\":0.5,"
            + "\"<MusicVolume>k__BackingField\":0.75,"
            + "\"<WasChanged>k__BackingField\":true"
            + "}";

        private const string DerivedJson = "{"
            + "\"<MasterVolume>k__BackingField\":0.25,"
            + "\"<SoundsVolume>k__BackingField\":0.5,"
            + "\"<MusicVolume>k__BackingField\":0.75,"
            + "\"<WasChanged>k__BackingField\":true,"
            + "\"<IsShakeOff>k__BackingField\":true"
            + "}";

        [Test]
        public void FromJson_OldKeys_ReadsEveryField()
        {
            var save = JsonUtility.FromJson<SettingsSave>(Json);

            Assert.AreEqual(0.25f, save.MasterVolume);
            Assert.AreEqual(0.5f, save.SoundsVolume);
            Assert.AreEqual(0.75f, save.MusicVolume);
            Assert.IsTrue(save.WasChanged);
        }

        [Test]
        public void ToJson_ReadSave_WritesTheSameKeys()
        {
            var save = JsonUtility.FromJson<SettingsSave>(Json);

            Assert.AreEqual(Json, JsonUtility.ToJson(save));
        }

        [Test]
        public void DerivedSave_OldKeys_ReadsAndWritesBaseFieldsInOneObject()
        {
            var save = JsonUtility.FromJson<DerivedSave>(DerivedJson);

            Assert.AreEqual(0.25f, save.MasterVolume);
            Assert.AreEqual(0.5f, save.SoundsVolume);
            Assert.AreEqual(0.75f, save.MusicVolume);
            Assert.IsTrue(save.WasChanged);
            Assert.IsTrue(save.IsShakeOff);
            Assert.AreEqual(DerivedJson, JsonUtility.ToJson(save));
        }

        [Test]
        public void Setters_WriteEachItsOwnField()
        {
            var save = new SettingsSave();

            save.SetMasterVolume(0.25f);
            save.SetSoundsVolume(0.5f);
            save.SetMusicVolume(0.75f);

            Assert.AreEqual(0.25f, save.MasterVolume);
            Assert.AreEqual(0.5f, save.SoundsVolume);
            Assert.AreEqual(0.75f, save.MusicVolume);
            Assert.IsFalse(save.WasChanged);
        }

        // The options assets of the games keep the defaults as YAML under the same names.
        [Test]
        public void OptionsDefaults_FullVolumesUnderOldNames()
        {
            var options = ScriptableObject.CreateInstance<SettingsOptions>();

            try
            {
                var serialized = new SerializedObject(options);

                Assert.AreEqual(1f, serialized.FindProperty("_defaultValues.<MasterVolume>k__BackingField").floatValue);
                Assert.AreEqual(1f, serialized.FindProperty("_defaultValues.<SoundsVolume>k__BackingField").floatValue);
                Assert.AreEqual(1f, serialized.FindProperty("_defaultValues.<MusicVolume>k__BackingField").floatValue);
                Assert.IsFalse(serialized.FindProperty("_defaultValues.<WasChanged>k__BackingField").boolValue);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(options);
            }
        }

        [Serializable]
        private class DerivedSave : SettingsSave
        {
            [field: SerializeField] public bool IsShakeOff { get; private set; }
        }
    }
}
