using System.IO;
using System.Linq;
using UnityEditor;

namespace Internal
{
    // The files a player build gets from the content build for its time only, removed after it, also a failed one: the
    // catalog (ContentCatalog) as a json TextAsset in Resources (an asset folder of its own) and, for the preloaded bundles
    // of a WebGL build, the preload manifest that puts their files into the .data file (no request at the start).
    public static class ContentBuildFiles
    {
        // An asset folder owned by the build alone: removed whole after it, and before it when a killed editor left it.
        public const string CatalogFolder = "Assets/FrameworkContentStamp";

        // Read by the WebGL build program: one path under StreamingAssets a line.
        public const string PreloadManifest = "Library/PlayerDataCache/WebGLPreloadedStreamingAssets/framework-content.manifest";

        public static string CatalogAsset => $"{CatalogFolder}/Resources/{ContentCatalog.Resource}.json";

        public static void WriteCatalog(ContentCatalog catalog)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CatalogAsset) ?? throw new IOException(CatalogAsset));
            File.WriteAllText(CatalogAsset, catalog.ToJson());
            AssetDatabase.ImportAsset(CatalogFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceSynchronousImport);
        }

        // The files of the preloaded bundles of the build; none: no manifest.
        public static void WritePreloadManifest(ContentCatalog catalog)
        {
            var files = catalog.Bundles
                .Where(bundle => bundle.IsPreloaded)
                .SelectMany(bundle => bundle.Files.Select(file => $"{ContentShippedPaths.Folder}/{file.Name}"))
                .ToArray();

            if (files.Length == 0)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(PreloadManifest) ?? throw new IOException(PreloadManifest));
            File.WriteAllLines(PreloadManifest, files);
        }

        public static void Delete()
        {
            if (AssetDatabase.IsValidFolder(CatalogFolder))
            {
                if (AssetDatabase.DeleteAsset(CatalogFolder) == false)
                    throw new IOException($"[ContentBuild] Could not delete {CatalogFolder}");
            }

            // Written but never imported: the editor died between the two.
            if (Directory.Exists(CatalogFolder))
            {
                Directory.Delete(CatalogFolder, true);
                File.Delete($"{CatalogFolder}.meta");
            }

            if (File.Exists(PreloadManifest))
                File.Delete(PreloadManifest);
        }
    }
}
