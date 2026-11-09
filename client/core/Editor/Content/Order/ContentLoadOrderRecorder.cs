using System.Collections.Generic;
using System.IO;

namespace Internal
{
    // Appends to the load order of a content directory every key asked for the first time: the keys of earlier runs
    // stay where they are, so several runs (the menu, a level, another level) add up to one order of first use.
    public sealed class ContentLoadOrderRecorder
    {
        public ContentLoadOrderRecorder(string path, IEnumerable<ContentLoadKey> recorded)
        {
            _path = path;
            _recorded = new HashSet<ContentLoadKey>(recorded);
        }

        private readonly string _path;
        private readonly HashSet<ContentLoadKey> _recorded;

        public void Record(ContentLoadKey key)
        {
            if (_recorded.Add(key) == false)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.AppendAllText(_path, key + "\n");
        }
    }
}
