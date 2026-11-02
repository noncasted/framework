using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Internal.EditorTests
{
    // The deferred generators and syncs run half a second after an import, inside whichever test is running then. A
    // project declared without Addressables has nothing to sync; any other project without the settings gets the error.
    public class SharedAddressablesSyncTests
    {
        private const string PrefabPath = "Assets/SharedAddressablesSyncTests.prefab";
        private const double AfterDebounceSeconds = 1;

        private static string SettingsFile =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), FrameworkSettings.SettingsPath);

        private string _original;

        [SetUp]
        public void SetUp()
        {
            _original = File.ReadAllText(SettingsFile);
        }

        // The settings first: deleting the prefab schedules the sync again.
        [TearDown]
        public void TearDown()
        {
            File.WriteAllText(SettingsFile, _original);
            AssetDatabase.DeleteAsset(PrefabPath);
        }

        [UnityTest]
        public IEnumerator PrefabImported_ProjectWithoutAddressables_NothingLogged()
        {
            WriteFlag(true);

            var source = new GameObject("source");
            PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
            Object.DestroyImmediate(source);

            yield return WaitForDebounce();
        }

        [Test]
        public void SharedSync_NotDeclaredAndNoSettings_LogsTheError()
        {
            RequireNoSettings();
            LogAssert.Expect(LogType.Error, "[SharedAddressablesSync] Addressable settings are missing.");

            SharedAddressablesSync.Sync();
        }

        [Test]
        public void CatalogSync_NotDeclaredAndNoSettings_LogsTheError()
        {
            RequireNoSettings();
            LogAssert.Expect(LogType.Error, "[Catalog] Addressable settings are missing.");

            CatalogAddressablesSync.Sync("Catalog", "Catalog_", "Assets/Catalog", Array.Empty<ICatalogGroupDefinition>());
        }

        [Test]
        public void ScenesSync_NotDeclaredAndNoSettings_LogsTheError()
        {
            RequireNoSettings();
            LogAssert.Expect(LogType.Error, "[ScenesAddressablesSync] Addressable settings are missing.");

            ScenesAddressablesSync.Sync(new[] { ("Scene", "00000000000000000000000000000000") });
        }

        // No frame passes with the flag off: a sync scheduled by an earlier test does not run in between.
        private void RequireNoSettings()
        {
            if (FrameworkSettings.WithoutAddressables == false)
                Assert.Ignore("The project has Addressable settings: nothing is missing.");

            WriteFlag(false);
        }

        private void WriteFlag(bool value)
        {
            var json = JObject.Parse(_original);
            json["WithoutAddressables"] = value;
            File.WriteAllText(SettingsFile, json.ToString());
        }

        private static IEnumerator WaitForDebounce()
        {
            var end = EditorApplication.timeSinceStartup + AfterDebounceSeconds;

            while (EditorApplication.timeSinceStartup < end)
                yield return null;
        }
    }
}
