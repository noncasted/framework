using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Internal
{
    [NoAutoStaticsCleanup]
    internal static class AudioSourceCollector
    {
        // Clips named Name_0, Name_1 are variations of one sound and are collected into a list.
        private static readonly Regex VariationPattern = new(@"^(.+?)_(\d+)$", RegexOptions.CultureInvariant);

        // Tagged clips are needed by both the generator and the group registry, so the traversal is shared.
        internal static IEnumerable<MarkedSource> EnumerateMarked()
        {
            var assetsRoot = Application.dataPath;

            if (Directory.Exists(assetsRoot) == false)
                yield break;

            // A failure to read throws: taken for an empty result, it would delete what the generator did not see.
            var files = Directory.GetFiles(assetsRoot, "*.*", SearchOption.AllDirectories);

            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            foreach (var file in files)
            {
                if (AudioSourcePaths.IsSourcePath(file) == false)
                    continue;

                var path = CatalogPaths.ToAssetPath(projectRoot, file);
                var importer = CatalogPaths.SourceImporter(path);

                if (importer == null)
                    continue;

                if (AudioCatalogMetadata.TryRead(importer, out var metadata) == false)
                    continue;

                yield return new MarkedSource(path, metadata);
            }
        }

        internal static List<ClipSource> CollectSources()
        {
            var sources = new List<ClipSource>();

            foreach (var marked in EnumerateMarked())
            {
                var path = marked.Path;
                var metadata = marked.Metadata;

                if (metadata.Included == false)
                    continue;

                var groupName = CatalogNaming.ToGroupName(metadata.Group);

                if (string.IsNullOrEmpty(groupName))
                    throw new InvalidOperationException($"Included audio clip has empty group: {path}");

                var fileName = AudioSourcePaths.GetSourceName(path);
                var propertyName = CatalogNaming.ToGroupName(fileName);

                if (string.IsNullOrEmpty(propertyName))
                    throw new InvalidOperationException($"Identifier of the audio clip is empty: {path}");

                var isEncoded = AudioSourcePaths.IsEncodedPath(path);
                var clip = isEncoded ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                var encoded = isEncoded ? AssetDatabase.LoadAssetAtPath<TextAsset>(path) : null;

                if (clip == null && encoded == null)
                    throw new InvalidOperationException($"Failed to load the audio clip of the catalogue: {path}");

                sources.Add(new ClipSource(
                    path,
                    groupName,
                    propertyName,
                    ParseVariation(fileName),
                    clip,
                    encoded,
                    Mathf.Clamp01(metadata.Volume)));
            }

            return sources;
        }

        private static VariationKey ParseVariation(string fileName)
        {
            var match = VariationPattern.Match(fileName);

            if (match.Success == false)
                return null;

            var name = CatalogNaming.ToGroupName(match.Groups[1].Value);

            if (string.IsNullOrEmpty(name) || int.TryParse(match.Groups[2].Value, out var index) == false)
                return null;

            return new VariationKey(name, index);
        }
    }
}
