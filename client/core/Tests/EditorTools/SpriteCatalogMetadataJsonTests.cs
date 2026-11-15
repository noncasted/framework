using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Internal.EditorTests
{
    public class SpriteCatalogMetadataJsonTests
    {
        // A speed of no tag is applied to nothing and is written back for good: a broken markup, not a default.
        [Test]
        public void Read_TagSpeedWithoutTag_Throws()
        {
            var catalog = JObject.Parse(@"{ ""tagSpeeds"": [ { ""fps"": 6 } ] }");

            Assert.Throws<InvalidOperationException>(() => SpriteCatalogMetadataJson.Read(catalog));
        }

        // A speed without its fps is baked as a zero duration and written back for good.
        [Test]
        public void Read_TagSpeedWithoutFps_Throws()
        {
            var catalog = JObject.Parse(@"{ ""tagSpeeds"": [ { ""tag"": ""Idle"" } ] }");

            Assert.Throws<InvalidOperationException>(() => SpriteCatalogMetadataJson.Read(catalog));
        }

        [Test]
        public void Read_TagSpeed_KeepsFps()
        {
            var catalog = JObject.Parse(@"{ ""tagSpeeds"": [ { ""tag"": ""Idle"", ""fps"": 6 } ] }");

            Assert.AreEqual(6f, SpriteCatalogMetadataJson.Read(catalog).TagSpeeds[0].FramesPerSecond);
        }

        // An animation of no tag is never found by the tag it was meant for and is written back for good.
        [Test]
        public void Read_AnimationWithoutTag_Throws()
        {
            var catalog = JObject.Parse(@"{ ""animations"": [ { ""layers"": [ ""A"" ] } ] }");

            Assert.Throws<InvalidOperationException>(() => SpriteCatalogMetadataJson.Read(catalog));
        }

        [Test]
        public void Read_CustomAnimationWithoutName_Throws()
        {
            var catalog = JObject.Parse(@"{ ""customAnimations"": [ { ""source"": ""Idle"", ""layers"": [ ""A"" ] } ] }");

            Assert.Throws<InvalidOperationException>(() => SpriteCatalogMetadataJson.Read(catalog));
        }

        [Test]
        public void Read_CustomAnimationWithoutSource_Throws()
        {
            var catalog = JObject.Parse(@"{ ""customAnimations"": [ { ""name"": ""Run"", ""layers"": [ ""A"" ] } ] }");

            Assert.Throws<InvalidOperationException>(() => SpriteCatalogMetadataJson.Read(catalog));
        }

        // A kind nobody knows (a typo, a kind renamed in the code) read as a sheet is written back as a sheet for good.
        [TestCase("animation")]
        [TestCase("Unknown")]
        public void Read_UnknownKind_Throws(string kind)
        {
            var catalog = JObject.Parse($@"{{ ""kind"": ""{kind}"" }}");

            Assert.Throws<InvalidOperationException>(() => SpriteCatalogMetadataJson.Read(catalog));
        }

        [Test]
        public void Read_NoKind_IsSheet()
        {
            Assert.That(SpriteCatalogMetadataJson.Read(new JObject()).Kind, Is.EqualTo(SpriteCatalogKind.Sheet));
        }

        // A sprite without a group is a normal state: the collector takes the group of its folder or reports it.
        [Test]
        public void Read_NoGroup_IsEmpty()
        {
            Assert.That(SpriteCatalogMetadataJson.Read(new JObject()).Group, Is.Empty);
        }
    }
}
