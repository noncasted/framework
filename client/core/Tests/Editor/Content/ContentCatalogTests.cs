using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Internal.Tests
{
    // The catalog a player reads: the json of the build gives the same entries back; a version of another build, a bundle,
    // a dependency or a target it does not know throw with the name.
    public class ContentCatalogTests
    {
        [Test]
        public void Json_GivesTheEntriesBack_NamesCaseAside()
        {
            var catalog = ContentCatalog.FromJson(Catalog("Shared").ToJson());

            var main = catalog.Bundle("main");
            Assert.AreEqual("Main", main.Name);
            Assert.AreEqual(new[] { "Shared" }, main.Dependencies);
            Assert.AreEqual(new[] { "root-guid" }, main.Roots);
            Assert.AreEqual("Main.abc.bundle", main.Files[0].Name);
            Assert.AreEqual(123, main.Files[0].Size);
            Assert.IsTrue(main.Files[1].IsScenes);
            Assert.IsTrue(main.IsGzip);
            Assert.IsFalse(main.IsPreloaded);
            Assert.AreEqual("Main", catalog.Object("target-guid", 7).Bundle);
            Assert.AreEqual("Icon", catalog.Object("sprite-guid", 9).SubName);
            Assert.IsTrue(catalog.Object("sprite-guid", 9).IsSubAsset);
            Assert.AreEqual("Assets/Level.unity", catalog.Scene("scene-guid").Path);
        }

        [Test]
        public void UnknownEntries_Throw()
        {
            var catalog = ContentCatalog.FromJson(Catalog("Shared").ToJson());

            StringAssert.Contains("No content bundle Missing", Assert.Throws<KeyNotFoundException>(() => catalog.Bundle("Missing")).Message);
            Assert.Throws<KeyNotFoundException>(() => catalog.Object("target-guid", 8));
            Assert.Throws<KeyNotFoundException>(() => catalog.Scene("missing-guid"));
        }

        [Test]
        public void DependencyOnABundleNotInTheCatalog_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => ContentCatalog.FromJson(Catalog("Gone").ToJson()));
        }

        [Test]
        public void OtherVersion_Throws()
        {
            var json = Catalog("Shared").ToJson().Replace($"\"_version\": {ContentCatalog.CurrentVersion}", "\"_version\": 99");

            StringAssert.Contains("version 99", Assert.Throws<InvalidOperationException>(() => ContentCatalog.FromJson(json)).Message);
        }

        private static ContentCatalog Catalog(string dependency)
        {
            var main = new ContentCatalogBundle("Main",
                new[] { new ContentCatalogFile("Main.abc.bundle", 123, false), new ContentCatalogFile("Main.scenes.def.bundle", 45, true) },
                new[] { dependency }, new[] { "root-guid" }, false, true);
            var shared = new ContentCatalogBundle("Shared", new[] { new ContentCatalogFile("Shared.0a.bundle", 10, false) }, new string[0], new string[0], true, false);

            return new ContentCatalog(new[] { main, shared },
                new[] { new ContentCatalogObject("target-guid", 7, "Main", null), new ContentCatalogObject("sprite-guid", 9, "Main", "Icon") },
                new[] { new ContentCatalogScene("scene-guid", "Main", "Assets/Level.unity") });
        }
    }
}
