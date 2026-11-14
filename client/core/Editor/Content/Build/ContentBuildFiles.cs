using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Internal
{
    // The files a player build gets from the content build for its time only, removed after it, also a failed one:
    // the stamp (ContentStamp) of each directory as a json TextAsset in Resources (an asset folder of its own) and, for the
    // preloaded directories of a WebGL build, the index as a json TextAsset next to the stamp and the preload manifest that puts
    // their archives into the .data file (no request for either at the start).
    public static class ContentBuildFiles
    {
        // An asset folder owned by the build alone: removed whole after it, and before it when a killed editor left it.
        public const string StampFolder = "Assets/FrameworkContentStamp";

        // Read by the WebGL build program: one path under StreamingAssets a line.
        public const string PreloadManifest = "Library/PlayerDataCache/WebGLPreloadedStreamingAssets/framework-content.manifest";

        public static string StampAsset(string name)
        {
            return $"{StampFolder}/Resources/{ContentPaths.StampResource(name)}.json";
        }

        public static string IndexAsset(string name)
        {
            return $"{StampFolder}/Resources/{ContentPaths.IndexResource(name)}.json";
        }

        // Before WriteStamp, which imports the folder.
        public static void WriteIndex(ContentIndex index)
        {
            var path = IndexAsset(index.Name);

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new IOException(path));
            File.WriteAllText(path, index.ToJson());
        }

        public static void WriteStamp(string name, ContentStamp stamp)
        {
            var path = StampAsset(name);

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new IOException(path));
            File.WriteAllText(path, stamp.ToJson());
            AssetDatabase.ImportAsset(StampFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
        }

        // The archives of the preloaded directories of the build.
        public static void WritePreloadManifest(IReadOnlyList<ContentIndex> indexes)
        {
            var files = indexes
                .Select(index => ContentPaths.Directory(index.Name))
                .SelectMany(folder => new[] { ContentPaths.ArchiveFile, ContentPaths.ManifestHashFile }.Select(file => $"{folder}/{file}"));

            Directory.CreateDirectory(Path.GetDirectoryName(PreloadManifest) ?? throw new IOException(PreloadManifest));
            File.WriteAllLines(PreloadManifest, files);
        }

        public static void Delete()
        {
            if (AssetDatabase.IsValidFolder(StampFolder))
            {
                if (AssetDatabase.DeleteAsset(StampFolder) == false)
                    throw new IOException($"[ContentBuild] Could not delete {StampFolder}");
            }

            // Written but never imported: the editor died between the two.
            if (Directory.Exists(StampFolder))
            {
                Directory.Delete(StampFolder, true);
                File.Delete($"{StampFolder}.meta");
            }

            if (File.Exists(PreloadManifest))
                File.Delete(PreloadManifest);
        }
    }
}
