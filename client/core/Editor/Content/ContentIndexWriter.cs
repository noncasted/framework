using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Internal
{
    // Computes the index of a built content directory from its manifest: every file of the folder with its size, the
    // root files, and the files of every object and scene a Loadable or a LoadableSceneId may load. isPreloaded: the
    // files go into the .data file of a WebGL build; isBrotli: they are shipped compressed as <name>.br.
    public static class ContentIndexWriter
    {
        public static ContentIndex Write(ContentManifest manifest, bool isPreloaded, bool isBrotli)
        {
            var files = Directory.GetFiles(manifest.Folder)
                .Select(path => new FileInfo(path))
                .Where(file => file.Name != ContentManifest.HashFile)
                .OrderBy(file => file.Name, StringComparer.Ordinal)
                .Select(file => new ContentFile(file.Name, file.Length))
                .ToArray();

            var positions = new Dictionary<string, int>();

            for (var i = 0; i < files.Length; i++)
                positions.Add(files[i].Name, i);

            var closure = new ContentFileClosure(manifest);

            var objects = manifest.SerializedFiles
                .Where(file => file.IsInDirectory && file.Kind != ContentFileKind.MonoScript)
                .Select(file => new ContentObjectFiles(file.Guid, file.LocalId, Positions(closure.Of(file.Index))))
                .ToArray();

            var scenes = manifest.Scenes
                .Select(scene => new ContentSceneFiles(scene.Guid, scene.Path, Positions(closure.Of(scene.SerializedFile))))
                .ToArray();

            return new ContentIndex(
                manifest.Name,
                manifest.Hash,
                files,
                Position(manifest.FileName),
                Positions(closure.Of(manifest.Roots)),
                objects,
                scenes,
                isPreloaded,
                isBrotli);

            int[] Positions(IReadOnlyList<string> names)
            {
                return names.Select(Position).ToArray();
            }

            int Position(string name)
            {
                if (positions.TryGetValue(name, out var position))
                    return position;

                throw new FileNotFoundException($"The content directory {manifest.Name} has no file {name}", Path.Combine(manifest.Folder, name));
            }
        }
    }
}
