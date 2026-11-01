using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Internal.EditorTests
{
    // The options have no setters: the tab writes the serialized fields by their paths, and every path must reach the
    // field its property reads.
    public class OptionsTabTests
    {
        private OptionsContainer _options;

        [SetUp]
        public void SetUp()
        {
            _options = ScriptableObject.CreateInstance<OptionsContainer>();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearUndo(_options);
            Object.DestroyImmediate(_options);
        }

        [Test]
        public void Write_Debug_ReachesTheOptions()
        {
            OptionsTab.Write(_options, OptionsTab.EnableGizmosPath, p => p.boolValue = true);
            OptionsTab.Write(_options, OptionsTab.EnableLogsPath, p => p.boolValue = true);

            Assert.IsTrue(_options.DebugOptions.EnableGizmos);
            Assert.IsTrue(_options.DebugOptions.EnableLogs);
        }

        [Test]
        public void Write_Version_ReachesTheOptions()
        {
            OptionsTab.Write(_options, OptionsTab.VersionPath, p => p.stringValue = "2.3.4");

            Assert.AreEqual("2.3.4", _options.VersionOptions.Value);
        }

        [Test]
        public void Write_Backend_ReachesTheOptions()
        {
            OptionsTab.Write(_options, OptionsTab.EnvironmentPath, p => p.intValue = (int)BackendEnvironment.Production);
            OptionsTab.Write(_options, OptionsTab.ProductionApiUrlPath, p => p.stringValue = "https://production");
            OptionsTab.Write(_options, OptionsTab.LocalApiUrlPath, p => p.stringValue = "http://local");
            OptionsTab.Write(_options, OptionsTab.TimeoutSecondsPath, p => p.intValue = 7);

            var backend = _options.BackendOptions;

            Assert.AreEqual(BackendEnvironment.Production, backend.Environment);
            Assert.AreEqual("https://production", backend.ProductionApiUrl);
            Assert.AreEqual("http://local", backend.LocalApiUrl);
            Assert.AreEqual(7, backend.TimeoutSeconds);
        }

        // The values of the enum are not sequential: the value is written, not the index.
        [Test]
        public void Write_Platform_ReachesTheOptions()
        {
            OptionsTab.Write(_options, OptionsTab.PlatformPath, p => p.intValue = (int)PlatformType.ItchIO);

            Assert.AreEqual(PlatformType.ItchIO, _options.PlatformOptions.PlatformType);
        }

        [Test]
        public void Write_MarksTheAssetDirtyWithAnUndoStep()
        {
            EditorUtility.ClearDirty(_options);

            OptionsTab.Write(_options, OptionsTab.VersionPath, p => p.stringValue = "2.3.4");

            Assert.IsTrue(EditorUtility.IsDirty(_options));

            Undo.PerformUndo();

            Assert.AreEqual("1.0.0", _options.VersionOptions.Value);
        }
    }
}
