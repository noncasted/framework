using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.EditorTests
{
    // The baked texture of a Multiple Animations file: an equal frame is one sprite, a speed comes from the source tag.
    public class AsepriteAnimationBakerTests
    {
        private static readonly Color32 Red = new(255, 0, 0, 255);

        private const string Content = "Assets/AsepriteBakerTestsContent";

        private static string SettingsFile =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), FrameworkSettings.SettingsPath);

        private string _path;
        private string _settings;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "BakerTests_" + Guid.NewGuid().ToString("N") + ".aseprite");
            _settings = File.ReadAllText(SettingsFile);

            // The content of the test project stays untouched.
            var json = JObject.Parse(_settings);
            json["ContentFolder"] = Content;
            File.WriteAllText(SettingsFile, json.ToString());

            new AsepriteFileFixture(2, 1, 2)
                .Layer("A")
                .Layer("B")
                .Cell(0, 0, 0, 0, 1, 1, new[] { Red })
                .Cell(1, 1, 1, 0, 1, 1, new[] { Red })
                .Tag("Idle", 0, 1)
                .Tag("One", 1, 1)
                .Write(_path);
        }

        [TearDown]
        public void TearDown()
        {
            File.Delete(_path);
            File.WriteAllText(SettingsFile, _settings);

            // The folders stay for the next test: the first bake of a run creates them.
            EditorTestAssets.DeleteFiles(Content);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            EditorTestAssets.DeleteFolder(Content);
        }

        [Test]
        public void Bake_AnimationsOfTheFile_TagsThenCustom()
        {
            var metadata = new SpriteCatalogMetadata();
            metadata.SetFramesPerSecond("One", 5f);
            metadata.AddCustomAnimation(new SpriteCatalogCustomAnimation("OnlyB", "Idle", new List<string> { "B" }));

            var written = new HashSet<string>();
            var animations = AsepriteAnimationBaker.Bake(_path, "Game", "Hero", metadata, written);

            Assert.That(animations.Select(animation => animation.Name), Is.EqualTo(new[] { "Idle", "One", "OnlyB" }));
            Assert.That(animations[0].Sprites.Length, Is.EqualTo(2));
            Assert.That(animations[0].Time, Is.EqualTo(2f / 12f).Within(0.0001f));
            Assert.That(animations[1].Time, Is.EqualTo(1f / 5f).Within(0.0001f));
            Assert.That(animations[2].Time, Is.EqualTo(2f / 12f).Within(0.0001f));
            Assert.That(written, Is.EqualTo(new[] { AsepriteAnimationBaker.BakedFolder + "/Game_Hero.asset" }));
        }

        [Test]
        public void Bake_EqualFrameAndLayers_AreOneSprite()
        {
            var animations = AsepriteAnimationBaker.Bake(
                _path, "Game", "Hero", new SpriteCatalogMetadata(), new HashSet<string>());

            Assert.That(animations[1].Sprites[0], Is.SameAs(animations[0].Sprites[1]));
        }

        // One empty pixel of padding in the texture and one of border in the sprite, the first frame in the top-left.
        [Test]
        public void Bake_SpritesHaveTheBorderAndThePivotOfTheCanvas()
        {
            var sprite = AsepriteAnimationBaker.Bake(
                _path, "Game", "Hero", new SpriteCatalogMetadata(), new HashSet<string>())[0].Sprites[0];

            Assert.That(sprite.rect.width, Is.EqualTo(4f));
            Assert.That(sprite.rect.height, Is.EqualTo(3f));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(2f, 1f)));
            Assert.That(sprite.texture.GetPixel((int)sprite.rect.x + 1, (int)sprite.rect.y + 1), Is.EqualTo((Color)Red));
            Assert.That(sprite.texture.GetPixel((int)sprite.rect.x, (int)sprite.rect.y).a, Is.EqualTo(0f));
        }

        [Test]
        public void Bake_SameInputTwice_LoadsTheBakedSprites()
        {
            var first = AsepriteAnimationBaker.Bake(
                _path, "Game", "Hero", new SpriteCatalogMetadata(), new HashSet<string>());
            AssetDatabase.SaveAssets();

            var second = AsepriteAnimationBaker.Bake(
                _path, "Game", "Hero", new SpriteCatalogMetadata(), new HashSet<string>());

            Assert.That(second[0].Sprites[0], Is.EqualTo(first[0].Sprites[0]));
        }

        [Test]
        public void Bake_CustomOfAMissingTag_LogsAndSkips()
        {
            var metadata = new SpriteCatalogMetadata();
            metadata.AddCustomAnimation(new SpriteCatalogCustomAnimation("Lost", "Nope", new List<string> { "A" }));
            LogAssert.Expect(LogType.Error, new Regex("Custom animation 'Lost' .* refers to missing tag 'Nope'"));

            var animations = AsepriteAnimationBaker.Bake(_path, "Game", "Hero", metadata, new HashSet<string>());

            Assert.That(animations.Count, Is.EqualTo(2));
        }

        // An empty result would be taken for a file without animations and delete its textures.
        [Test]
        public void Bake_NoAnimations_Throws()
        {
            new AsepriteFileFixture(1, 1, 1).Layer("A").Write(_path);

            Assert.Throws<InvalidOperationException>(() => AsepriteAnimationBaker.Bake(
                _path, "Game", "Hero", new SpriteCatalogMetadata(), new HashSet<string>()));
        }

        [Test]
        public void DeleteStale_RemovesOnlyTheNotWritten()
        {
            var written = new HashSet<string>();
            AsepriteAnimationBaker.Bake(_path, "Game", "Hero", new SpriteCatalogMetadata(), written);
            AsepriteAnimationBaker.Bake(_path, "Game", "Stale", new SpriteCatalogMetadata(), new HashSet<string>());
            AssetDatabase.SaveAssets();

            AsepriteAnimationBaker.DeleteStale(written);

            var left = AssetDatabase.FindAssets("t:Texture2D", new[] { AsepriteAnimationBaker.BakedFolder })
                .Select(AssetDatabase.GUIDToAssetPath);
            Assert.That(left, Is.EqualTo(written));
        }
    }
}
