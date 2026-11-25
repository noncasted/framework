using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.EditorTests
{
    // What the sprite catalogue reads from an aseprite file: layers, tags and the pixels of a composed frame.
    public class AsepriteDocumentTests
    {
        private static readonly Color32 Red = new(255, 0, 0, 255);
        private static readonly Color32 Blue = new(0, 0, 255, 255);
        private static readonly Color32 Clear = new(0, 0, 0, 0);

        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), "AsepriteDocumentTests_" + Guid.NewGuid().ToString("N") + ".aseprite");
        }

        [TearDown]
        public void TearDown()
        {
            File.Delete(_path);
        }

        [Test]
        public void Layers_ImageLayersOnly_WithGroupPathVisibilityAndOpacity()
        {
            new AsepriteFileFixture(1, 1, 1)
                .Layer("Bottom", opacity: 51)
                .Group("Body")
                .Layer("Arm", childLevel: 1)
                .Group("Off", AsepriteFileFixture.Hidden, 1)
                .Layer("Hand", childLevel: 2)
                .Layer("Top", AsepriteFileFixture.Hidden)
                .Write(_path);

            var document = AsepriteDocument.Read(_path);

            Assert.That(document.Width, Is.EqualTo(1));
            Assert.That(document.Layers.Count, Is.EqualTo(4));
            AssertLayer(document.Layers[0], 0, "Bottom", true, 0.2f);
            AssertLayer(document.Layers[1], 2, "Body/Arm", true, 1f);
            AssertLayer(document.Layers[2], 4, "Body/Off/Hand", false, 1f);
            AssertLayer(document.Layers[3], 5, "Top", false, 1f);
            Assert.That(document.GetVisibleLayerNames(), Is.EqualTo(new[] { "Bottom", "Body/Arm" }));
        }

        [Test]
        public void Tags_FoundByExactName_WithFrameCount()
        {
            new AsepriteFileFixture(1, 1, 3).Layer("A").Tag("Idle", 1, 2).Write(_path);

            var document = AsepriteDocument.Read(_path);

            Assert.That(document.Tags.Count, Is.EqualTo(1));
            Assert.That(document.FindTag("Idle").From, Is.EqualTo(1));
            Assert.That(document.FindTag("Idle").To, Is.EqualTo(2));
            Assert.That(document.FindTag("Idle").FrameCount, Is.EqualTo(2));
            Assert.That(document.FindTag("idle"), Is.Null);
        }

        // Aseprite counts rows from the top, a Unity texture from the bottom.
        [Test]
        public void Compose_FirstRowOfTheCell_IsTheLastRowOfTheTexture()
        {
            new AsepriteFileFixture(2, 2, 1)
                .Layer("A")
                .Cell(0, 0, 0, 0, 2, 1, new[] { Red, Blue })
                .Write(_path);

            var pixels = AsepriteDocument.Read(_path).Compose(0, new[] { "A" });

            Assert.That(pixels, Is.EqualTo(new[] { Clear, Clear, Red, Blue }));
        }

        [Test]
        public void Compose_CellOutsideTheCanvas_IsClipped()
        {
            new AsepriteFileFixture(2, 2, 1)
                .Layer("A")
                .Cell(0, 0, 1, 1, 2, 2, new[] { Red, Blue, Blue, Blue })
                .Cell(0, 0, -1, -1, 2, 2, new[] { Blue, Blue, Blue, Red })
                .Write(_path);

            // The second cell of the same layer and frame replaces the first.
            Assert.That(AsepriteDocument.Read(_path).Compose(0, new[] { "A" }), Is.EqualTo(new[] { Clear, Clear, Red, Clear }));

            new AsepriteFileFixture(2, 2, 1)
                .Layer("A")
                .Cell(0, 0, 1, 1, 2, 2, new[] { Red, Blue, Blue, Blue })
                .Write(_path);

            Assert.That(AsepriteDocument.Read(_path).Compose(0, new[] { "A" }), Is.EqualTo(new[] { Clear, Red, Clear, Clear }));
        }

        [Test]
        public void Compose_OnlyTheAskedLayers_BottomToTop()
        {
            new AsepriteFileFixture(1, 1, 1)
                .Layer("Bottom")
                .Layer("Top")
                .Cell(0, 0, 0, 0, 1, 1, new[] { Red })
                .Cell(0, 1, 0, 0, 1, 1, new[] { Blue })
                .Write(_path);

            var document = AsepriteDocument.Read(_path);

            Assert.That(document.Compose(0, new[] { "Bottom" }), Is.EqualTo(new[] { Red }));
            Assert.That(document.Compose(0, new[] { "Top", "Bottom" }), Is.EqualTo(new[] { Blue }));
            Assert.That(document.Compose(0, Array.Empty<string>()), Is.EqualTo(new[] { Clear }));
        }

        [Test]
        public void Compose_LayerAndCellOpacity_Multiply()
        {
            new AsepriteFileFixture(1, 1, 1)
                .Layer("Bottom")
                .Layer("Top", opacity: 128)
                .Cell(0, 0, 0, 0, 1, 1, new[] { Red })
                .Cell(0, 1, 0, 0, 1, 1, new[] { Blue }, 128)
                .Write(_path);

            // 128/255 * 128/255 = 0.252 of blue over opaque red.
            Assert.That(
                AsepriteDocument.Read(_path).Compose(0, new[] { "Bottom", "Top" }),
                Is.EqualTo(new[] { new Color32(191, 0, 64, 255) }));
        }

        [Test]
        public void Compose_LinkedCell_TakesThePixelsOfTheFrame()
        {
            new AsepriteFileFixture(1, 1, 3)
                .Layer("A")
                .Cell(0, 0, 0, 0, 1, 1, new[] { Red })
                .LinkedCell(1, 0, 0)
                .Write(_path);

            var document = AsepriteDocument.Read(_path);

            Assert.That(document.Compose(1, new[] { "A" }), Is.EqualTo(new[] { Red }));
            Assert.That(document.Compose(2, new[] { "A" }), Is.EqualTo(new[] { Clear }));
        }

        [Test]
        public void Read_OtherBlendMode_WarnsAndComposesAsNormal()
        {
            new AsepriteFileFixture(1, 1, 1)
                .Layer("A", blendMode: 1)
                .Cell(0, 0, 0, 0, 1, 1, new[] { Red })
                .Write(_path);

            LogAssert.Expect(LogType.Warning, new Regex(@"\[AsepriteDocumentBuilder\] Layer 'A' in .* composed as Normal"));

            Assert.That(AsepriteDocument.Read(_path).Compose(0, new[] { "A" }), Is.EqualTo(new[] { Red }));
        }

        [Test]
        public void ReadCached_RereadsOnlyAChangedFile()
        {
            new AsepriteFileFixture(1, 1, 1).Layer("A").Write(_path);
            File.SetLastWriteTimeUtc(_path, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            var first = AsepriteDocument.ReadCached(_path);

            Assert.That(AsepriteDocument.ReadCached(_path), Is.SameAs(first));

            new AsepriteFileFixture(1, 1, 1).Layer("B").Write(_path);
            File.SetLastWriteTimeUtc(_path, new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.That(AsepriteDocument.ReadCached(_path).Layers[0].Name, Is.EqualTo("B"));
        }

        // The inspector is the boundary: it logs and draws on, the baker gets the exception.
        [Test]
        public void ReadCached_BrokenFile_LogsAndReturnsNull()
        {
            File.WriteAllBytes(_path, new byte[] { 1, 2, 3, 4 });
            LogAssert.ignoreFailingMessages = true;

            try
            {
                Assert.That(AsepriteDocument.ReadCached(_path), Is.Null);
                Assert.Catch<Exception>(() => AsepriteDocument.Read(_path));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        private static void AssertLayer(AsepriteLayer layer, int index, string name, bool visible, float opacity)
        {
            Assert.That(layer.Index, Is.EqualTo(index));
            Assert.That(layer.Name, Is.EqualTo(name));
            Assert.That(layer.Visible, Is.EqualTo(visible));
            Assert.That(layer.Opacity, Is.EqualTo(opacity).Within(0.001f));
        }
    }
}
