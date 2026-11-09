using System.IO;
using System.Linq;
using UnityEditor;

namespace Internal
{
    // The files a player build gets from the content build for its time only, removed after it, also a failed one:
    // the index as a json TextAsset in Resources (an asset folder of its own) and, for a preloaded directory of a WebGL
    // build, the preload manifest that puts the files of StreamingAssets/Content/<name> into the .data file.
    public static class ContentBuildFiles
    {
        // An asset folder owned by the build alone: removed whole after it, and before it when a killed editor left it.
        public const string IndexFolder = "Assets/FrameworkContentIndex";

        // Read by the WebGL build program: one path under StreamingAssets a line (the folder of the manifest of
        // Addressables too).
        public const string PreloadManifest = "Library/PlayerDataCache/WebGLPreloadedStreamingAssets/framework-content.manifest";

        public static string IndexAsset(string name)
        {
            return $"{IndexFolder}/Resources/{ContentPaths.IndexResource(name)}.json";
        }

        public static void WriteIndex(ContentIndex index)
        {
            var path = IndexAsset(index.Name);

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? throw new IOException(path));
            File.WriteAllText(path, index.ToJson());
            AssetDatabase.ImportAsset(IndexFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
        }

        public static void WritePreloadManifest(ContentIndex index)
        {
            var folder = ContentPaths.Directory(index.Name);
            var files = index.Files
                .Select(file => file.Name)
                .Append(ContentManifest.HashFile)
                .Select(file => $"{folder}/{file}");

            Directory.CreateDirectory(Path.GetDirectoryName(PreloadManifest) ?? throw new IOException(PreloadManifest));
            File.WriteAllLines(PreloadManifest, files);
        }

        public static void Delete()
        {
            if (AssetDatabase.IsValidFolder(IndexFolder))
            {
                if (AssetDatabase.DeleteAsset(IndexFolder) == false)
                    throw new IOException($"[ContentBuild] Could not delete {IndexFolder}");
            }

            // Written but never imported: the editor died between the two.
            if (Directory.Exists(IndexFolder))
            {
                Directory.Delete(IndexFolder, true);
                File.Delete($"{IndexFolder}.meta");
            }

            if (File.Exists(PreloadManifest))
                File.Delete(PreloadManifest);
        }
    }
}
