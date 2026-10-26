using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Internal.EditorTests
{
    // An open generic step is a base for closed steps of a project, not a step itself: it cannot be created.
    public class GenericPlayerBuildStep<T> : IPlayerBuildStep
    {
        public void BeforeBuild(BuildTarget target)
        {
        }

        public void AfterBuild(BuildTarget target, bool succeeded)
        {
        }
    }

    public class EditorToolsTests
    {
        private static string ScannerFolder => Path.GetFullPath("Temp/ContainerGraphScannerTests");

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(ScannerFolder))
                Directory.Delete(ScannerFolder, true);
        }

        // FixturesReference holds an .asmref to the fixtures assembly: bindings of a prefab there belong to that
        // assembly, not to the asmdef of the folder above.
        [Test]
        public void AssetUnderAsmref_BelongsToTheReferencedAssembly()
        {
            EditorTestPaths.RequireAssets();

            Assert.That(HierarchyBindingsPaths.FindAsmdefFolder(EditorTestPaths.Root + "/FixturesReference/Shop.prefab"),
                Is.EqualTo(EditorTestPaths.Root + "/Fixtures"));
        }

        [Test]
        public void OpenGenericBuildStep_IsNotCreated()
        {
            var steps = PlayerBuildSteps.Find();

            Assert.That(steps.Select(step => step.GetType()), Has.None.Matches<Type>(type => type.IsGenericType));
        }

        // A prefab GUID that cannot be read leaves the asset map without the entities of that prefab, silently.
        [Test]
        public void UnreadablePrefabMeta_Throws()
        {
            Directory.CreateDirectory(ScannerFolder);
            File.WriteAllText(Path.Combine(ScannerFolder, "Hero.prefab"), string.Empty);

            var meta = Path.Combine(ScannerFolder, "Hero.prefab.meta");
            File.WriteAllText(meta, "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\n");

            using (new FileStream(meta, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.Catch<Exception>(() => ContainerGraphAssetScanner.BuildPrefabMap(ScannerFolder));
        }
    }
}
