using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Internal.EditorTests
{
    // The layout of the project comes from ProjectSettings/Framework.json only: no game paths of our projects
    // behind a missing file or field, and an edited file is read again. Every test puts the file back.
    public class FrameworkSettingsTests
    {
        private static string SettingsFile =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), FrameworkSettings.SettingsPath);

        private string _original;

        [SetUp]
        public void SetUp()
        {
            _original = File.Exists(SettingsFile) ? File.ReadAllText(SettingsFile) : null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_original == null)
                File.Delete(SettingsFile);
            else
                File.WriteAllText(SettingsFile, _original);
        }

        [Test]
        public void EditedSettings_AreReadAgain()
        {
            Write(json => json["ContentFolder"] = "Assets/FrameworkSettingsTests");
            Assert.That(FrameworkSettings.ContentFolder, Is.EqualTo("Assets/FrameworkSettingsTests"));

            Write(json => json["ContentFolder"] = "Assets/FrameworkSettingsTests2");
            Assert.That(FrameworkSettings.ContentFolder, Is.EqualTo("Assets/FrameworkSettingsTests2"));
        }

        [Test]
        public void MissingSettings_Throw()
        {
            File.Delete(SettingsFile);

            Assert.Catch<Exception>(() => _ = FrameworkSettings.ContentFolder);
        }

        [Test]
        public void MissingContentFolder_Throws()
        {
            Write(json => json.Remove("ContentFolder"));

            Assert.Catch<Exception>(() => _ = FrameworkSettings.ContentFolder);
        }

        // Without a field the list is empty, not the folders of our games.
        [Test]
        public void MissingModuleRoots_AreEmpty()
        {
            Write(json => json.Remove("ModuleRoots"));

            Assert.That(FrameworkSettings.ModuleRoots, Is.Empty);
        }

        private void Write(Action<JObject> edit)
        {
            var json = _original == null ? new JObject() : JObject.Parse(_original);
            json["ContentAssembly"] ??= "Internal.Content";
            json["ContentFolder"] ??= "Assets/Common/Internal";
            edit(json);
            File.WriteAllText(SettingsFile, json.ToString());
        }
    }
}
