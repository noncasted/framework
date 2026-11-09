using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Internal
{
    // Computes the index of a built content directory from its manifest and its archive (ContentArchiveWriter): every
    // file of the folder with its span in the archive, in the order of the archive, the root files, and the files of
    // every object and scene a Loadable or a LoadableSceneId may load.
    public static class ContentIndexWriter
    {
        public static ContentIndex Write(ContentManifest manifest, ContentArchiveLayout layout)
        {
            var names = Directory.GetFiles(manifest.Folder)
                .Select(Path.GetFileName)
                .Where(name => name != ContentManifest.HashFile)
                .ToHashSet();

            if (names.SetEquals(layout.Files.Keys) == false)
                throw new InvalidDataException($"The archive of the content directory {manifest.Name} holds other files than its folder {manifest.Folder}");

            var files = layout.Files
                .Select(pair => new ContentFile(pair.Key, pair.Value.Offset, pair.Value.Length))
                .OrderBy(file => file.Offset)
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
                layout.Length,
                layout.HeaderLength,
                files,
                Position(manifest.FileName),
                Positions(closure.Of(manifest.Roots)),
                objects,
                scenes);

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
