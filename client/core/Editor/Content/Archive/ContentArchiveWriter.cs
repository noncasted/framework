using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Content;
using UnityEngine;

namespace Internal
{
    // Packs a built content directory into one archive of the engine (UnityFS, the blocks LZ4HC) in the order of first
    // use (ContentArchiveOrder): <output>/content0.archive, the name the registration mounts, next to
    // BuildManifestHash.txt. A WebGL player fetches the spans of the files it loads with Range requests.
    public static class ContentArchiveWriter
    {
        public const string FileName = "content0.archive";

        public static ContentArchiveLayout Write(ContentManifest manifest, string output)
        {
            if (Directory.Exists(output))
                Directory.Delete(output, true);

            Directory.CreateDirectory(output);

            var files = ContentArchiveOrder.Of(manifest)
                .Select(name => new ResourceFile
                {
                    fileName = Path.GetFullPath(Path.Combine(manifest.Folder, name)),
                    fileAlias = name,
                    serializedFile = name.EndsWith(".cf")
                })
                .ToArray();

            var path = Path.Combine(output, FileName);
            ContentBuildInterface.ArchiveAndCompress(files, path, BuildCompression.LZ4);

            if (File.Exists(path) == false)
                throw new BuildFailedException($"[ContentBuild] The archive of the content directory {manifest.Name} was not written to {path}");

            File.Copy(Path.Combine(manifest.Folder, ContentManifest.HashFile), Path.Combine(output, ContentManifest.HashFile));

            var layout = ContentArchiveReader.Read(path);

            if (layout.Files.Count != files.Length)
                throw new BuildFailedException($"[ContentBuild] The archive {path} holds {layout.Files.Count} files, not {files.Length}");

            return layout;
        }
    }
}
