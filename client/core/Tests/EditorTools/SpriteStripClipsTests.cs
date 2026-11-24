using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Internal.EditorTests
{
    // The clips of a PNG strip: frames by runs up and down and lists, the sprites of the strip in that order, the markup
    // in the userData.
    public class SpriteStripClipsTests
    {
        private readonly List<Sprite> _strip = new();
        private readonly List<Sprite> _close = new();
        private Texture2D _texture;

        [SetUp]
        public void SetUp()
        {
            _texture = new Texture2D(6, 1);

            for (var i = 0; i < 4; i++)
                _strip.Add(CreateSprite($"Strip_{i}", i));

            for (var i = 0; i < 2; i++)
                _close.Add(CreateSprite($"Close_{i}", 4 + i));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var sprite in _strip.Concat(_close))
                UnityEngine.Object.DestroyImmediate(sprite);

            UnityEngine.Object.DestroyImmediate(_texture);
            _strip.Clear();
            _close.Clear();
        }

        [TestCase("", new[] { 0, 1, 2, 3 })]
        [TestCase("1-3", new[] { 1, 2, 3 })]
        [TestCase("3-0", new[] { 3, 2, 1, 0 })]
        [TestCase("1,0,1,2", new[] { 1, 0, 1, 2 })]
        [TestCase("0-1, 3", new[] { 0, 1, 3 })]
        [TestCase("2", new[] { 2 })]
        public void Parse_Frames_InPlayOrder(string frames, int[] expected)
        {
            CollectionAssert.AreEqual(expected, SpriteClipFrames.Parse(frames, Count).Select(frame => frame.Index));
        }

        [Test]
        public void Parse_FramesOfAnotherStrip_NameTheirFile()
        {
            var frames = SpriteClipFrames.Parse("Close:1-0, 2", Count);

            Assert.AreEqual(new[] { "Close", "Close", null }, frames.Select(frame => frame.File).ToArray());
            Assert.AreEqual(new[] { 1, 0, 2 }, frames.Select(frame => frame.Index).ToArray());
        }

        // A frame past the strip would take a sprite of another clip or none: a broken markup.
        [TestCase("4")]
        [TestCase("0-4")]
        [TestCase("a")]
        [TestCase("1,,2")]
        [TestCase("-1")]
        [TestCase("Close:2")]
        [TestCase("Open:0")]
        [TestCase(":0")]
        public void Parse_BrokenFrames_Throws(string frames)
        {
            Assert.Throws<InvalidOperationException>(() => SpriteClipFrames.Parse(frames, Count));
        }

        [Test]
        public void Cut_Clips_TakeTheSpritesOfTheStrip()
        {
            var clips = new[]
            {
                new SpriteCatalogClip("Windup", "0-1", 0.3f),
                new SpriteCatalogClip("Back", "3-0", 0.5f)
            };

            var animations = SpriteStripClips.Cut("Strip.png", Strip, clips);

            Assert.AreEqual(new[] { "Windup", "Back" }, animations.Select(animation => animation.Name).ToArray());
            Assert.AreEqual(new[] { "Strip_0", "Strip_1" }, animations[0].Sprites.Select(sprite => sprite.name).ToArray());
            Assert.AreEqual(new[] { "Strip_3", "Strip_2", "Strip_1", "Strip_0" },
                animations[1].Sprites.Select(sprite => sprite.name).ToArray());
            Assert.AreEqual(0.5f, animations[1].Time);
        }

        [Test]
        public void Cut_ClipOverTwoStrips_TakesTheSpritesOfBoth()
        {
            var clips = new[] { new SpriteCatalogClip("Beam", "Close:1-0, 0-3, Close:0-1", 2f) };

            var sprites = SpriteStripClips.Cut("Strip.png", Strip, clips)[0].Sprites.Select(sprite => sprite.name);

            Assert.AreEqual(new[] { "Close_1", "Close_0", "Strip_0", "Strip_1", "Strip_2", "Strip_3", "Close_0", "Close_1" },
                sprites.ToArray());
        }

        [Test]
        public void SiblingPath_SameFolderAndExtension()
        {
            Assert.AreEqual("Assets/Art/Watcher/Close_Side.png",
                SpriteStripClips.SiblingPath("Assets/Art/Watcher/Attack_Side.png", "Close_Side"));
        }

        // Without clips the strip gives the group nothing, silently: the markup is broken.
        [Test]
        public void Cut_NoClips_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SpriteStripClips.Cut("Strip.png", Strip, Array.Empty<SpriteCatalogClip>()));
        }

        // Two clips of one name are one property of the group: the second would be lost.
        [Test]
        public void Cut_TwoClipsOfOneName_Throws()
        {
            var clips = new[] { new SpriteCatalogClip("Run", "", 0.5f), new SpriteCatalogClip("Run", "0", 0.5f) };

            Assert.Throws<InvalidOperationException>(() => SpriteStripClips.Cut("Strip.png", Strip, clips));
        }

        [Test]
        public void Cut_ZeroTime_Throws()
        {
            var clips = new[] { new SpriteCatalogClip("Run", "", 0f) };

            Assert.Throws<InvalidOperationException>(() => SpriteStripClips.Cut("Strip.png", Strip, clips));
        }

        private Sprite CreateSprite(string name, int x)
        {
            var sprite = Sprite.Create(_texture, new Rect(x, 0, 1, 1), Vector2.zero);
            sprite.name = name;
            return sprite;
        }

        private IReadOnlyList<Sprite> Strip(string file)
        {
            return file switch
            {
                null => _strip,
                "Close" => _close,
                _ => Array.Empty<Sprite>()
            };
        }

        private int Count(string file)
        {
            return Strip(file).Count;
        }

        [Test]
        public void Json_Clips_RoundTrip()
        {
            var metadata = new SpriteCatalogMetadata();
            metadata.SetKind(SpriteCatalogKind.MultipleAnimations);
            metadata.AddClip(new SpriteCatalogClip("Windup", "0-1", 0.3f));
            metadata.AddClip(new SpriteCatalogClip("Whole", "", 0.8f));

            var read = SpriteCatalogMetadataJson.Read(SpriteCatalogMetadataJson.Write(metadata));

            Assert.AreEqual(2, read.Clips.Count);
            Assert.AreEqual("Windup", read.Clips[0].Name);
            Assert.AreEqual("0-1", read.Clips[0].Frames);
            Assert.AreEqual(0.3f, read.Clips[0].Time);
            Assert.AreEqual(string.Empty, read.Clips[1].Frames);
        }

        // A clip without its frames or time would play the whole strip or nothing and be written back for good.
        [TestCase(@"{ ""clips"": [ { ""frames"": ""0"", ""time"": 1 } ] }")]
        [TestCase(@"{ ""clips"": [ { ""name"": ""Run"", ""time"": 1 } ] }")]
        [TestCase(@"{ ""clips"": [ { ""name"": ""Run"", ""frames"": ""0"" } ] }")]
        public void Json_BrokenClip_Throws(string json)
        {
            Assert.Throws<InvalidOperationException>(() => SpriteCatalogMetadataJson.Read(JObject.Parse(json)));
        }
    }
}
