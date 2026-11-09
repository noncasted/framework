using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Internal
{
    // The order of the files of a content directory in its archive: the order of first use, so that what one load needs
    // lies together and comes in a few ranges. The manifest, the closure of the roots (the registration), the closure of
    // every key of the recorded load order (ContentLoadOrder) in its order, of every scene, of every object in the order
    // of the manifest, then the files no load names. Every file of the folder but BuildManifestHash.txt, once.
    public static class ContentArchiveOrder
    {
        // loads: the recorded load order of the game, empty without one; a key the directory does not have is skipped.
        public static IReadOnlyList<string> Of(ContentManifest manifest, IReadOnlyList<ContentLoadKey> loads)
        {
            var order = new List<string>();
            var placed = new HashSet<string>();
            var closure = new ContentFileClosure(manifest);

            Add(new[] { manifest.FileName });
            Add(closure.Of(manifest.Roots));

            foreach (var file in Files(manifest, loads))
                Add(closure.Of(file));

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

        // The keys of the load order the directory has.
        public static int Known(ContentManifest manifest, IReadOnlyList<ContentLoadKey> loads)
        {
            return Files(manifest, loads).Count();
        }

        // The serialized file of every key of the load order the directory has, in its order.
        private static IEnumerable<int> Files(ContentManifest manifest, IReadOnlyList<ContentLoadKey> loads)
        {
            var objects = new Dictionary<(string, long), int>();
            var scenes = new Dictionary<string, int>();

            foreach (var file in manifest.SerializedFiles)
            {
                if (file.IsInDirectory && file.Kind != ContentFileKind.MonoScript)
                    objects[(file.Guid, file.LocalId)] = file.Index;
            }

            foreach (var scene in manifest.Scenes)
                scenes[scene.Guid] = scene.SerializedFile;

            foreach (var key in loads)
            {
                // As ContentIndex.ObjectFiles: an object without a file of its own is in the file of its whole asset.
                if (key.IsScene ? scenes.TryGetValue(key.Guid, out var file)
                        : objects.TryGetValue((key.Guid, key.LocalId), out file) || objects.TryGetValue((key.Guid, 0), out file))
                    yield return file;
            }
        }
    }
}
