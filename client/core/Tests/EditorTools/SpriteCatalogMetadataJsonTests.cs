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
    }
}
