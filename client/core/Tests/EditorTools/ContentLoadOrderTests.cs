using System.IO;
using NUnit.Framework;

namespace Internal.EditorTests
{
    // The load order of a content directory: its lines and the recorder that adds the keys of a run to it.
    public class ContentLoadOrderTests
    {
        private const string Path = "Temp/ContentLoadOrderTests/Game.txt";

        [SetUp]
        [TearDown]
        public void Clean()
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }

        [Test]
        public void Key_IsReadBackFromItsLine()
        {
            var keys = new[] { ContentLoadKey.Object("63cc0ae416a62df21bb5e517b993ffbc", -4521877384011240000), ContentLoadKey.Scene("81550fd60cd0a9c72af159779b8ea1f4") };

            foreach (var key in keys)
                Assert.That(ContentLoadKey.Parse(key.ToString()), Is.EqualTo(key));

            Assert.That(keys[1].ToString(), Is.EqualTo("scene 81550fd60cd0a9c72af159779b8ea1f4"));
        }

        [TestCase("object 63cc0ae416a62df21bb5e517b993ffbc")]
        [TestCase("scene")]
        [TestCase("asset 63cc0ae416a62df21bb5e517b993ffbc 1")]
        public void Key_OfAWrongLineThrows(string line)
        {
            Assert.Throws<System.FormatException>(() => ContentLoadKey.Parse(line));
        }

        [Test]
        public void Recorder_AppendsOnlyTheKeysAskedForTheFirstTime()
        {
            var menu = ContentLoadKey.Scene("aa");
            var icon = ContentLoadKey.Object("bb", 2);
            var level = ContentLoadKey.Scene("cc");

            new ContentLoadOrderRecorder(Path, new ContentLoadKey[0]).Record(menu);

            var run = new ContentLoadOrderRecorder(Path, new[] { menu });
            run.Record(icon);
            run.Record(menu);
            run.Record(level);
            run.Record(icon);

            Assert.That(File.ReadAllLines(Path), Is.EqualTo(new[] { "scene aa", "object bb 2", "scene cc" }));
        }
    }
}
