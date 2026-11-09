using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Internal
{
    // The order of the files of a content directory in its archive: the order of first use, so that what one load needs
    // lies together and comes in a few ranges. The manifest, the closure of the roots (the registration), the closure of
    // every scene, of every object in the order of the manifest, then the files no load names. Every file of the folder
    // but BuildManifestHash.txt, once.
    public static class ContentArchiveOrder
    {
        public static IReadOnlyList<string> Of(ContentManifest manifest)
        {
            var order = new List<string>();
            var placed = new HashSet<string>();
            var closure = new ContentFileClosure(manifest);

            Add(new[] { manifest.FileName });
            Add(closure.Of(manifest.Roots));

            foreach (var scene in manifest.Scenes)
                Add(closure.Of(scene.SerializedFile));

            foreach (var file in manifest.SerializedFiles)
                Add(closure.Of(file.Index));

            var folder = Directory.GetFiles(manifest.Folder)
                .Select(Path.GetFileName)
                .Where(name => name != ContentManifest.HashFile)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            var unknown = order.Where(name => Array.BinarySearch(folder, name, StringComparer.Ordinal) < 0).ToArray();

            if (unknown.Length > 0)
                throw new FileNotFoundException($"The content directory {manifest.Name} misses the files {string.Join(", ", unknown)}");

            Add(folder);
            return order;

            void Add(IEnumerable<string> files)
            {
                foreach (var file in files)
                {
                    if (placed.Add(file))
                        order.Add(file);
                }
            }
        }
    }
}
