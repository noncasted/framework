using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Global.Localization.Tests
{
    // The options asset of a game holds the keys the mapping had as public fields (System, Language). This reads a
    // real .asset file with those keys through the importer, saves it and reads it again.
    public class SystemLanguageMappingOldYamlTests
    {
        private const string FolderName = "SystemLanguageMappingOldYamlTests_Temp";
        private const string Folder = "Assets/" + FolderName;
        private const string AssetPath = Folder + "/Options.asset";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets", FolderName);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void LocalizationOptions_OldKeys_ReadsEveryField()
        {
            WriteAsset();

            var options = AssetDatabase.LoadAssetAtPath<LocalizationOptions>(AssetPath);
            Assert.IsTrue(options != null);
            AssertMappings(options);

            EditorUtility.SetDirty(options);
            AssetDatabase.SaveAssetIfDirty(options);
            AssetDatabase.ImportAsset(AssetPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssertMappings(AssetDatabase.LoadAssetAtPath<LocalizationOptions>(AssetPath));
        }

        private static void AssertMappings(LocalizationOptions options)
        {
            Assert.AreEqual(2, options.SystemLanguages.Count);
            Assert.AreEqual(SystemLanguage.Russian, options.SystemLanguages[0].System);
            Assert.AreEqual(Language.Russian, options.SystemLanguages[0].Language);
            Assert.AreEqual(SystemLanguage.French, options.SystemLanguages[1].System);
            Assert.AreEqual(Language.English, options.SystemLanguages[1].Language);
        }

        private static void WriteAsset()
        {
            var instance = ScriptableObject.CreateInstance<LocalizationOptions>();
            var script = MonoScript.FromScriptableObject(instance);
            Object.DestroyImmediate(instance);

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out var guid, out long _);
            Assert.IsNotEmpty(guid);

            var text = "%YAML 1.1\n"
                + "%TAG !u! tag:unity3d.com,2011:\n"
                + "--- !u!114 &11400000\n"
                + "MonoBehaviour:\n"
                + "  m_ObjectHideFlags: 0\n"
                + "  m_CorrespondingSourceObject: {fileID: 0}\n"
                + "  m_PrefabInstance: {fileID: 0}\n"
                + "  m_PrefabAsset: {fileID: 0}\n"
                + "  m_GameObject: {fileID: 0}\n"
                + "  m_Enabled: 1\n"
                + "  m_EditorHideFlags: 0\n"
                + $"  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}\n"
                + "  m_Name: Options\n"
                + "  m_EditorClassIdentifier: Internal.Localization::Global.Localization.LocalizationOptions\n"
                + "  _table: {fileID: 0}\n"
                + "  _defaultLanguage: 1\n"
                + "  _systemLanguages:\n"
                + $"  - System: {(int)SystemLanguage.Russian}\n"
                + $"    Language: {(int)Language.Russian}\n"
                + $"  - System: {(int)SystemLanguage.French}\n"
                + $"    Language: {(int)Language.English}\n";

            File.WriteAllText(AssetPath, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
