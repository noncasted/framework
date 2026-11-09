using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Internal
{
    // The log of a built content directory: the number of files, the raw and the brotli size, the files no load needs
    // and the built-in resources the build copied into the directory (a warning with their names: they come with every
    // scene or material that references them and are better cut at the source, see m_LightingDataAsset).
    public static class ContentBuildLog
    {
        // The GUIDs of the built-in resource files of the editor.
        private static readonly IReadOnlyDictionary<string, string> BuiltIn = new Dictionary<string, string>
        {
            ["0000000000000000e000000000000000"] = "unity default resources",
            ["0000000000000000f000000000000000"] = "unity_builtin_extra"
        };

        // The editor runs on .NET, where BrotliStream lives in System.IO.Compression.Brotli, while the scripts compile
        // against netstandard, which puts it in System.IO.Compression: a direct use does not load. SmallestSize
        // (quality 11, as the brotli of a WebGL build) is missing from netstandard too.
        private static readonly Lazy<Type> BrotliStream = new(() =>
            Type.GetType("System.IO.Compression.BrotliStream, System.IO.Compression.Brotli", true));

        private const System.IO.Compression.CompressionLevel SmallestSize = (System.IO.Compression.CompressionLevel)3;

        public static void Write(ContentManifest manifest, ContentIndex index)
        {
            var raw = index.Files.Sum(file => file.Size);
            var brotli = index.Files.Sum(file => BrotliSize(Path.Combine(manifest.Folder, file.Name)));

            Debug.Log($"[ContentBuild] {index.Name}: {index.Files.Count} files, {raw} bytes raw, {brotli} bytes brotli; " +
                      $"root files {index.RootFiles.Count} ({index.RootFiles.Sum(file => file.Size)} bytes), " +
                      $"{index.Objects.Count} objects, {index.Scenes.Count} scenes");

            var unused = Unused(index);

            if (unused.Count > 0)
                Debug.LogWarning($"[ContentBuild] {index.Name}: {unused.Count} files no load needs: {string.Join(", ", unused)}");

            var builtIn = manifest.SerializedFiles
                .Where(file => file.IsInDirectory && BuiltIn.ContainsKey(file.Guid))
                .Select(file => $"{BuiltIn[file.Guid]} ({file.FileName}, {new FileInfo(Path.Combine(manifest.Folder, file.FileName)).Length} bytes)")
                .ToArray();

            if (builtIn.Length > 0)
                Debug.LogWarning($"[ContentBuild] {index.Name}: built-in resources in the directory: {string.Join(", ", builtIn)}");
        }

        public static IReadOnlyList<ContentFile> Unused(ContentIndex index)
        {
            var used = new HashSet<ContentFile> { index.Manifest };
            used.UnionWith(index.RootFiles);

            foreach (var entry in index.Objects)
                used.UnionWith(index.ObjectFiles(entry.Guid, entry.LocalId));

            foreach (var entry in index.Scenes)
                used.UnionWith(index.SceneFiles(entry.Guid));

            return index.Files.Where(file => used.Contains(file) == false).ToArray();
        }

        private static long BrotliSize(string path)
        {
            using var target = new MemoryStream();

            using (var brotli = (Stream)Activator.CreateInstance(BrotliStream.Value, target, SmallestSize, true))
            using (var source = File.OpenRead(path))
                source.CopyTo(brotli);

            return target.Length;
        }
    }
}
