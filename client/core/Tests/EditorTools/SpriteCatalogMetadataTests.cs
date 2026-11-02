using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Internal.EditorTests
{
    // The sprite markup in the userData of an importer: the defaults of what is absent and what is written back.
    public class SpriteCatalogMetadataTests
    {
        private const string TempFolder = "Assets/SpriteCatalogMetadataTemp";
        private const string HolderPath = TempFolder + "/Holder.txt";

        private static string SettingsFile =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), FrameworkSettings.SettingsPath);

        private string _settings;

        // One asset for the class: the tests read and write the userData of its importer in memory, nothing imports it.
        // A text asset: importing a prefab schedules the shared addressables sync, which fires in a later test.
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(TempFolder));
            File.WriteAllText(CatalogPaths.ToFullPath(HolderPath), "holder");
            AssetDatabase.ImportAsset(HolderPath, ImportAssetOptions.ForceSynchronousImport);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            EditorTestAssets.DeleteFolder(TempFolder);
        }

        [SetUp]
        public void SetUp()
        {
            _settings = File.ReadAllText(SettingsFile);
        }

        [TearDown]
        public void TearDown()
        {
            File.WriteAllText(SettingsFile, _settings);
        }

        [Test]
        public void TryRead_NoMarkup_False()
        {
            var importer = Importer("{}");

            Assert.That(SpriteCatalogMetadata.TryRead(importer, out var metadata), Is.False);
            Assert.That(metadata, Is.Null);
        }

        [Test]
        public void TryRead_EmptyMarkup_Defaults()
        {
            Assert.That(SpriteCatalogMetadata.TryRead(Importer(@"{ ""spriteCatalog"": {} }"), out var metadata), Is.True);

            Assert.That(metadata.Included, Is.False);
            Assert.That(metadata.Group, Is.Empty);
            Assert.That(metadata.Kind, Is.EqualTo(SpriteCatalogKind.Sheet));
            Assert.That(metadata.Time, Is.EqualTo(SpriteCatalogMetadata.DefaultTime));
            Assert.That(metadata.Color, Is.EqualTo(SpriteCatalogMetadata.DefaultColor));
            Assert.That(metadata.FramesPerSecond, Is.EqualTo(SpriteCatalogMetadata.DefaultFramesPerSecond));
            Assert.That(metadata.Animations, Is.Empty);
            Assert.That(metadata.CustomAnimations, Is.Empty);
            Assert.That(metadata.TagSpeeds, Is.Empty);
        }

        [TestCase("Animation", SpriteCatalogKind.Animation)]
        [TestCase("MultipleAnimations", SpriteCatalogKind.MultipleAnimations)]
        [TestCase("Sheet", SpriteCatalogKind.Sheet)]
        [TestCase("animation", SpriteCatalogKind.Sheet)]
        [TestCase("Unknown", SpriteCatalogKind.Sheet)]
        public void TryRead_Kind(string kind, SpriteCatalogKind expected)
        {
            SpriteCatalogMetadata.TryRead(Importer($@"{{ ""spriteCatalog"": {{ ""kind"": ""{kind}"" }} }}"), out var metadata);

            Assert.That(metadata.Kind, Is.EqualTo(expected));
        }

        [Test]
        public void TryRead_PartOfTheColor_RestIsOne()
        {
            SpriteCatalogMetadata.TryRead(
                Importer(@"{ ""spriteCatalog"": { ""color"": { ""g"": 0.5 } } }"), out var metadata);

            Assert.That(metadata.Color, Is.EqualTo(new Color(1f, 0.5f, 1f, 1f)));
        }

        [Test]
        public void TryRead_ListsOfAnotherShape_AreSkipped()
        {
            SpriteCatalogMetadata.TryRead(Importer(@"{ ""spriteCatalog"": {
                ""animations"": [ 1, { ""layers"": [ ""A"", 2 ] } ],
                ""customAnimations"": 5,
                ""tagSpeeds"": [ ""x"", { ""tag"": ""Idle"", ""fps"": 6 } ]
            } }"), out var metadata);

            Assert.That(metadata.Animations.Count, Is.EqualTo(1));
            Assert.That(metadata.Animations[0].Tag, Is.Empty);
            Assert.That(metadata.Animations[0].Layers, Is.EqualTo(new[] { "A" }));
            Assert.That(metadata.CustomAnimations, Is.Empty);
            Assert.That(metadata.TagSpeeds.Count, Is.EqualTo(1));
            Assert.That(metadata.TagSpeeds[0].Tag, Is.EqualTo("Idle"));
            Assert.That(metadata.TagSpeeds[0].FramesPerSecond, Is.EqualTo(6f));
        }

        [Test]
        public void Stage_Sheet_WritesNoAnimationFields()
        {
            var importer = Importer("{}");
            var metadata = new SpriteCatalogMetadata();
            metadata.SetFramesPerSecond(30f);

            SpriteCatalogMetadata.Stage(importer, metadata);

            Assert.That(JObject.Parse(importer.userData)["spriteCatalog"].ToString(Formatting.None), Is.EqualTo(
                @"{""included"":false,""group"":"""",""kind"":""Sheet"",""time"":0.8," +
                @"""color"":{""r"":1.0,""g"":1.0,""b"":1.0,""a"":1.0}}"));
        }

        [Test]
        public void Stage_NoTagSpeeds_WritesNoSpeedList()
        {
            var importer = Importer("{}");
            var metadata = new SpriteCatalogMetadata();
            metadata.SetKind(SpriteCatalogKind.MultipleAnimations);

            SpriteCatalogMetadata.Stage(importer, metadata);

            var catalog = (JObject)JObject.Parse(importer.userData)["spriteCatalog"];
            Assert.That(catalog["fps"].Value<float>(), Is.EqualTo(12f));
            Assert.That(catalog["animations"], Is.Empty);
            Assert.That(catalog["customAnimations"], Is.Empty);
            Assert.That(catalog.ContainsKey("tagSpeeds"), Is.False);
        }

        [Test]
        public void Stage_NullMetadata_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => SpriteCatalogMetadata.Stage(Importer("{}"), null));
        }

        [Test]
        public void SetFramesPerSecond_SpeedOfTheFile_RemovesTheEntry()
        {
            var metadata = new SpriteCatalogMetadata();
            metadata.SetFramesPerSecond("Idle", 6f);
            metadata.SetFramesPerSecond("Idle", 8f);

            Assert.That(metadata.TagSpeeds.Count, Is.EqualTo(1));
            Assert.That(metadata.GetFramesPerSecond("Idle"), Is.EqualTo(8f));
            Assert.That(metadata.GetFramesPerSecond("idle"), Is.EqualTo(12f));

            metadata.SetFramesPerSecond("Idle", 12f);

            Assert.That(metadata.TagSpeeds, Is.Empty);
        }

        [Test]
        public void SwitchToAnimation_ResetsTimeAndColorOfAnotherKindOnly()
        {
            var metadata = new SpriteCatalogMetadata();
            metadata.SetTime(3f);
            metadata.SetColor(Color.red);

            metadata.SwitchToAnimation();

            Assert.That(metadata.Kind, Is.EqualTo(SpriteCatalogKind.Animation));
            Assert.That(metadata.Time, Is.EqualTo(SpriteCatalogMetadata.DefaultTime));
            Assert.That(metadata.Color, Is.EqualTo(SpriteCatalogMetadata.DefaultColor));

            metadata.SetTime(3f);
            metadata.SwitchToAnimation();

            Assert.That(metadata.Time, Is.EqualTo(3f));
        }

        [Test]
        public void GetDefaultGroup_ThePathInsideTheArtFolder()
        {
            var json = JObject.Parse(_settings);
            json["ArtFolder"] = "Assets/Art";
            File.WriteAllText(SettingsFile, json.ToString());

            Assert.That(SpriteCatalogMetadata.GetDefaultGroup("Assets/Art/Game/Players/Hero.png"), Is.EqualTo("Game.Players"));
            Assert.That(SpriteCatalogMetadata.GetDefaultGroup("assets/art/UI/Hero.png"), Is.EqualTo("UI"));
            Assert.That(SpriteCatalogMetadata.GetDefaultGroup("Assets/Art/Hero.png"), Is.Empty);
            Assert.That(SpriteCatalogMetadata.GetDefaultGroup("Assets/Other/Hero.png"), Is.Empty);
            Assert.That(SpriteCatalogMetadata.GetDefaultGroup(null), Is.Empty);
        }

        [Test]
        public void Importers_NullThrowsWhereAnImporterIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => SpriteCatalogMetadata.CreateDefault(null));
            Assert.Throws<ArgumentNullException>(() => SpriteCatalogMetadata.GetDefaultKind(null));
            Assert.That(SpriteCatalogMetadata.IsAsepriteImporter(null), Is.False);
        }

        [Test]
        public void ReadOrDefault_NoMarkup_IsASheetWithDefaults()
        {
            var importer = Importer("{}");

            Assert.That(SpriteCatalogMetadata.IsAsepriteImporter(importer), Is.False);
            Assert.That(SpriteCatalogMetadata.GetDefaultKind(importer), Is.EqualTo(SpriteCatalogKind.Sheet));

            var metadata = SpriteCatalogMetadata.ReadOrDefault(importer);

            Assert.That(metadata.Included, Is.False);
            Assert.That(metadata.Kind, Is.EqualTo(SpriteCatalogKind.Sheet));
            Assert.That(metadata.Time, Is.EqualTo(SpriteCatalogMetadata.DefaultTime));
        }

        private static AssetImporter Importer(string userData)
        {
            var importer = AssetImporter.GetAtPath(HolderPath);
            importer.userData = JObject.Parse(userData).ToString(Formatting.Indented);
            return importer;
        }
    }
}
