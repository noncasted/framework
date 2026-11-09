using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Internal
{
    // The files a load of a serialized file of a content directory needs: the file, the closure of its dependencies in
    // the manifest, and the resource files (texture pixels, audio) each of them names inside as cah:/<hash>. The files
    // of the targets of Loadable and LoadableSceneId are not dependencies: they load when asked.
    public sealed class ContentFileClosure
    {
        public ContentFileClosure(ContentManifest manifest)
        {
            _manifest = manifest;
        }

        private static readonly Regex Resource = new("cah:/([0-9a-f]{32})", RegexOptions.Compiled);

        private readonly ContentManifest _manifest;
        private readonly Dictionary<int, IReadOnlyList<string>> _resources = new();

        // The file itself first, then its dependencies depth first; the resources of a file follow it.
        public IReadOnlyList<string> Of(int serializedFile)
        {
            return Of(new[] { serializedFile });
        }

        public IReadOnlyList<string> Of(IReadOnlyList<int> serializedFiles)
        {
            var files = new List<string>();
            var added = new HashSet<string>();
            var seen = new HashSet<int>();

            foreach (var serializedFile in serializedFiles)
                Visit(serializedFile);

            return files;

            void Visit(int index)
            {
                if (seen.Add(index) == false)
                    return;

                var entry = _manifest.SerializedFiles[index];

                // The default resources come with the player.
                if (entry.IsInDirectory)
                {
                    Add(entry.FileName);

                    foreach (var resource in Resources(entry))
                        Add(resource);
                }

                foreach (var dependency in entry.Dependencies)
                    Visit(dependency);
            }

            void Add(string file)
            {
                if (added.Add(file))
                    files.Add(file);
            }
        }

        private IReadOnlyList<string> Resources(ContentManifestFile entry)
        {
            if (_resources.TryGetValue(entry.Index, out var resources))
                return resources;

            var path = Path.Combine(_manifest.Folder, entry.FileName);

            if (File.Exists(path) == false)
                throw new FileNotFoundException($"The serialized file {entry.Index} of the content directory {_manifest.Name} is missing", path);

            resources = Resource.Matches(Encoding.ASCII.GetString(File.ReadAllBytes(path)))
                .Select(match => ResourceFile(match.Groups[1].Value, entry))
                .Distinct()
                .ToArray();

            _resources.Add(entry.Index, resources);
            return resources;
        }

        private string ResourceFile(string hash, ContentManifestFile owner)
        {
            var files = Directory.GetFiles(_manifest.Folder, $"{hash}.*");

            if (files.Length != 1)
                throw new FileNotFoundException(
                    $"The resource cah:/{hash} of {owner.FileName} has {files.Length} files in the content directory {_manifest.Name}, expected one");

            return Path.GetFileName(files[0]);
        }
    }
}
