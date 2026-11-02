using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Internal.EditorTests
{
    // The scanner reads the yaml of prefabs and scenes as text: the asset map of the generator comes from what it finds.
    public class ContainerGraphAssetScannerTests
    {
        private const string SceneFactoryGuid = "acd7e370b6b3474e8b51f677a9edd625";
        private const string UnknownScript = "{fileID: 11500000, guid: 0000000000000000000000000000aaaa, type: 3}";
        private const string HeroGuid = "0123456789abcdef0123456789abcdef";

        private string _project;
        private string _assets;

        [SetUp]
        public void SetUp()
        {
            _project = Path.Combine(Path.GetTempPath(), "ContainerGraphAssetScannerTests_" + Guid.NewGuid().ToString("N"));
            _assets = Path.Combine(_project, "Assets");
            Directory.CreateDirectory(_assets);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_project, true);
        }

        [Test]
        public void MissingFolder_IsEmpty()
        {
            Assert.IsEmpty(ContainerGraphAssetScanner.Scan(Path.Combine(_project, "Nothing")));
            Assert.IsEmpty(ContainerGraphAssetScanner.Scan(null));
        }

        [Test]
        public void Prefab_ListsOfTheHolder_AreResolvedInOrderWithoutRepeats()
        {
            Write("Hero.prefab",
                Component(10, "Game::Game.HeroView",
                    "  _autoDetected:\n  - {fileID: 11}\n  - {fileID: 12}\n  - {fileID: 11}\n  - {fileID: 99}\n" +
                    "  _register:\n  - {fileID: 13}\n  - {fileID: 12}\n"),
                Component(11, "Game::Game.Move"),
                Component(12, "Game::Game.Health"),
                Component(13, "Game.Plain"));

            var asset = ContainerGraphAssetScanner.Scan(_assets).Single();

            Assert.AreEqual("Assets/Hero.prefab", asset.AssetPath);
            Assert.AreEqual("Game.HeroView", asset.HolderType);
            CollectionAssert.AreEqual(new[] { "Game.Move", "Game.Health", "Game.Plain" }, asset.ComponentTypes);
        }

        [Test]
        public void Prefab_InlineList_IsRead()
        {
            Write("Inline.prefab",
                Component(10, "Game::Game.View", "  _autoDetected: []\n  _register: [{fileID: 11}, {fileID: 12}]\n"),
                Component(11, "Game::Game.A"),
                Component(12, "Game::Game.B"));

            var asset = ContainerGraphAssetScanner.Scan(_assets).Single();

            CollectionAssert.AreEqual(new[] { "Game.A", "Game.B" }, asset.ComponentTypes);
        }

        [Test]
        public void Prefab_AnotherField_EndsTheList()
        {
            Write("Ended.prefab",
                Component(10, "Game::Game.View", "  _autoDetected:\n  - {fileID: 11}\n  _other:\n  - {fileID: 12}\n"),
                Component(11, "Game::Game.A"),
                Component(12, "Game::Game.B"));

            var asset = ContainerGraphAssetScanner.Scan(_assets).Single();

            CollectionAssert.AreEqual(new[] { "Game.A" }, asset.ComponentTypes);
        }

        [Test]
        public void Prefab_WithoutLists_IsNoHolder_EvenAnEntity()
        {
            Write("Empty.prefab", Component(10, "Game::Game.ScopeEntityView"), Component(11, "Game::Game.A"));

            Assert.IsEmpty(ContainerGraphAssetScanner.Scan(_assets));
        }

        [Test]
        public void Prefab_InPlugins_IsSkipped()
        {
            Write("Plugins/Foo/Skipped.prefab",
                Component(10, "Game::Game.View", "  _register:\n  - {fileID: 11}\n"),
                Component(11, "Game::Game.A"));

            Assert.IsEmpty(ContainerGraphAssetScanner.Scan(_assets));
        }

        [Test]
        public void Scene_EntityWithoutLists_IsHolder()
        {
            Write("Level.unity", Component(10, "Game::Game.ScopeEntityView"), Component(11, "Game::Game.A"));

            var asset = ContainerGraphAssetScanner.Scan(_assets).Single();

            Assert.AreEqual("Game.ScopeEntityView", asset.HolderType);
            Assert.IsEmpty(asset.ComponentTypes);
        }

        [Test]
        public void Scene_Factory_ServicesAreSorted()
        {
            Write("Level.unity",
                Component(10, "Internal::Internal.SceneServicesFactory", "  _services:\n  - {fileID: 11}\n  - {fileID: 12}\n"),
                Component(11, "Game::Game.Zeta"),
                Component(12, "Game::Game.Alpha"));

            var asset = ContainerGraphAssetScanner.Scan(_assets).Single();

            Assert.AreEqual("Internal.SceneServicesFactory", asset.HolderType);
            CollectionAssert.AreEqual(new[] { "Game.Alpha", "Game.Zeta" }, asset.ComponentTypes);
        }

        // The class identifier of a scene may be empty: the factory is found by the guid of its script.
        [Test]
        public void Scene_FactoryWithoutIdentifier_IsFoundByGuid()
        {
            Write("Level.unity",
                "--- !u!114 &10\nMonoBehaviour:\n" +
                $"  m_Script: {{fileID: 11500000, guid: {SceneFactoryGuid}, type: 3}}\n" +
                "  m_EditorClassIdentifier:\n  _services:\n  - {fileID: 11}\n",
                Component(11, "Game::Game.A"));

            var asset = ContainerGraphAssetScanner.Scan(_assets).Single();

            CollectionAssert.AreEqual(new[] { "Game.A" }, asset.ComponentTypes);
        }

        // A component of a nested prefab is a stripped block: its type lies in the source prefab.
        [Test]
        public void Scene_StrippedComponent_IsResolvedThroughItsPrefab()
        {
            Write("Hero.prefab", Component(11, "Game::Game.Source"));
            File.WriteAllText(Path.Combine(_assets, "Hero.prefab.meta"), $"fileFormatVersion: 2\nguid: {HeroGuid}\n");

            Write("Level.unity",
                Component(10, "Internal::Internal.SceneServicesFactory", "  _services:\n  - {fileID: 30}\n  - {fileID: 31}\n"),
                $"--- !u!114 &30 stripped\nMonoBehaviour:\n  m_CorrespondingSourceObject: {{fileID: 11, guid: {HeroGuid}, type: 3}}\n",
                $"--- !u!114 &31 stripped\nMonoBehaviour:\n  m_CorrespondingSourceObject: {{fileID: 77, guid: {HeroGuid}, type: 3}}\n");

            var asset = ContainerGraphAssetScanner.Scan(_assets).Single();

            CollectionAssert.AreEqual(new[] { "Game.Source" }, asset.ComponentTypes);
        }

        [Test]
        public void TwoHoldersOfOneClass_AreMerged()
        {
            Write("Twice.prefab",
                Component(10, "Game::Game.View", "  _register:\n  - {fileID: 12}\n"),
                Component(11, "Game::Game.View", "  _register:\n  - {fileID: 13}\n  - {fileID: 12}\n"),
                Component(12, "Game::Game.A"),
                Component(13, "Game::Game.B"));

            var asset = ContainerGraphAssetScanner.Scan(_assets).Single();

            CollectionAssert.AreEqual(new[] { "Game.A", "Game.B" }, asset.ComponentTypes);
        }

        [Test]
        public void Results_AreSortedByPathAndHolder()
        {
            Write("B.prefab",
                Component(10, "Game::Game.Second", "  _register:\n  - {fileID: 12}\n"),
                Component(11, "Game::Game.First", "  _register:\n  - {fileID: 12}\n"),
                Component(12, "Game::Game.A"));

            Write("A.unity", Component(10, "Game::Game.ScopeEntity"));

            var assets = ContainerGraphAssetScanner.Scan(_assets);
            var names = new List<string>();

            foreach (var asset in assets)
                names.Add(Path.GetFileName(asset.AssetPath) + ":" + asset.HolderType);

            CollectionAssert.AreEqual(new[] { "A.unity:Game.ScopeEntity", "B.prefab:Game.First", "B.prefab:Game.Second" }, names);
        }

        [Test]
        public void BuildPrefabMap_MapsTheGuidToThePrefab_WithoutAMetaOrAPrefab()
        {
            Write("Hero.prefab", Component(11, "Game::Game.A"));
            File.WriteAllText(Path.Combine(_assets, "Hero.prefab.meta"), $"fileFormatVersion: 2\nguid: {HeroGuid}\n");
            File.WriteAllText(Path.Combine(_assets, "Gone.prefab.meta"), "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcde0\n");
            Write("NoMeta.prefab", Component(11, "Game::Game.A"));

            var map = ContainerGraphAssetScanner.BuildPrefabMap(_assets);

            Assert.AreEqual(1, map.Count);
            Assert.AreEqual(Path.Combine(_assets, "Hero.prefab"), map[HeroGuid.ToUpperInvariant()]);
        }

        private void Write(string relativePath, params string[] blocks)
        {
            var path = Path.Combine(_assets, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "%YAML 1.1\n" + string.Concat(blocks));
        }

        private static string Component(int fileId, string identifier, string fields = "")
        {
            return $"--- !u!114 &{fileId}\nMonoBehaviour:\n  m_Script: {UnknownScript}\n" +
                   $"  m_EditorClassIdentifier: {identifier}\n{fields}";
        }
    }
}
